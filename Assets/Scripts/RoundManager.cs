using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    public enum TeamColor
    {
        Red,
        Blue,
        Green,
        Yellow
    }

    public enum GamePhase
    {
        WaitingForReady,
        SkillSelecting,
        RoundStarting,
        RoundPlaying,
        RoundEnding,
        MatchFinished
    }

    [Header("Ready")]
    [SerializeField] private int minPlayersToStart = 2;

    [Header("Skill Selection")]
    [SerializeField] private float skillSelectionDuration = 10.0f;
    [SerializeField] private float startAfterAllSkillsSelectedDelay = 2.0f;
    [SerializeField]
    private List<PlayerSkillType> availableRoundSkills = new()
    {
        PlayerSkillType.DoubleJump,
        PlayerSkillType.RapidFire,
        PlayerSkillType.BulletSpeedUp,
        PlayerSkillType.DamageReduction
    };

    [Header("Round")]
    [SerializeField] private int maxRoundCount = 3;
    [SerializeField] private float nextRoundDelay = 2.0f;

    [Header("Match Reset")]
    [SerializeField] private float returnToWaitingDelay = 3.0f;

    [Header("Respawn")]
    [SerializeField] private Transform spawnPointsRoot;
    [SerializeField] private List<Transform> spawnPoints = new();
    [SerializeField] private bool shuffleSpawnPointsEachRound = true;

    [Header("Health Items")]
    [SerializeField] private HealthItemSpawner healthItemSpawner;

    [Header("UI")]
    [SerializeField] private WaitingRoomUI waitingRoomUI;
    [SerializeField] private BattleStartUI battleStartUI;

    private readonly List<PlayerHealth> players = new();
    private readonly Dictionary<PlayerHealth, int> points = new();
    private readonly Dictionary<PlayerHealth, bool> readyStates = new();
    private readonly Dictionary<PlayerHealth, bool> skillSelectedStates = new();
    private readonly Dictionary<PlayerHealth, PlayerSkillType> selectedSkills = new();

    public int currentRound = 1;
    [Networked]
    private GamePhase phase { get; set; }

    private bool uiPhaseInitialized;
    private GamePhase lastAppliedUIPhase;

    private Coroutine skillSelectionCoroutine;
    private Coroutine startRoundCoroutine;

    public bool CanUseWeapons => phase == GamePhase.RoundPlaying;
    public bool CanControlPlayers => phase == GamePhase.WaitingForReady || phase == GamePhase.RoundPlaying;
    public bool IsWaitingForReady => phase == GamePhase.WaitingForReady;
    public bool IsSkillSelecting => phase == GamePhase.SkillSelecting;
    public bool IsRoundPlaying => phase == GamePhase.RoundPlaying;
    public bool IsMatchFinished => phase == GamePhase.MatchFinished;
    private bool isSpawned;

    public class PlayerTeam : MonoBehaviour
    {
        public TeamColor Team { get; private set; }

        public void SetTeam(TeamColor team)
        {
            Team = team;
        }
    }

    public int RegisteredPlayerCount
    {
        get
        {
            return players.Count(player => player != null);
        }
    }

    public IReadOnlyList<PlayerHealth> Players => players;

    private readonly TeamColor[] teamOrder =
    {
        TeamColor.Red,
        TeamColor.Blue,
        TeamColor.Green,
        TeamColor.Yellow
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Debug.Log("[RoundManager] Awake");
    }

    public override void Spawned()
    {
        isSpawned = true;

        if (Object.HasStateAuthority)
        {
            phase = GamePhase.WaitingForReady;
        }

        ApplyUIForPhase(phase, false);

        lastAppliedUIPhase = phase;
        uiPhaseInitialized = true;

        Debug.Log("[RoundManager] Waiting for players to ready.");
        Debug.Log("[RoundManager] Press Enter or ZL to ready.");

        LogSpawnPointSettings();
        LogSkillSlots();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        isSpawned = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        isSpawned = false;
    }

    private void AssignTeamColor(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        PlayerTeam playerTeam = player.GetComponent<PlayerTeam>();

        if (playerTeam == null)
        {
            playerTeam = player.gameObject.AddComponent<PlayerTeam>();
        }

        int index = players.Count - 1;

        TeamColor assignedColor = teamOrder[index % teamOrder.Length];

        playerTeam.SetTeam(assignedColor);

        player.SetTeam(assignedColor);

        Debug.Log($"{player.gameObject.name} joined as {assignedColor}");
    }

    public void RegisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (players.Contains(player))
        {
            return;
        }

        players.Add(player);
        points[player] = 0;
        readyStates[player] = false;
        skillSelectedStates[player] = false;
        selectedSkills[player] = PlayerSkillType.None;

        player.SetReadyState(false);

        AssignTeamColor(player);

        player.OnDied += HandlePlayerDied;

        Debug.Log(
            $"[RoundManager] Registered: {player.gameObject.name}, " +
            $"Count={players.Count}, Ready=False"
        );
    }

    public void UnregisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        player.OnDied -= HandlePlayerDied;

        players.Remove(player);
        points.Remove(player);
        readyStates.Remove(player);
        skillSelectedStates.Remove(player);
        selectedSkills.Remove(player);

        Debug.Log($"[RoundManager] Unregistered: {player.gameObject.name}, Count={players.Count}");

        if (!isSpawned)
        {
            return;
        }

        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (phase == GamePhase.WaitingForReady)
        {
            TryStartFirstRound();
        }
        else if (phase == GamePhase.SkillSelecting)
        {
            TryFinishSkillSelection();
        }
    }

    public bool IsPlayerReady(PlayerHealth player)
    {
        return readyStates.TryGetValue(player, out bool ready) && ready;
    }

    public void SetPlayerReady(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (phase != GamePhase.WaitingForReady)
        {
            return;
        }

        if (!players.Contains(player))
        {
            Debug.LogWarning($"[RoundManager] Ready ignored. Player is not registered: {player.gameObject.name}");
            return;
        }

        if (readyStates.TryGetValue(player, out bool alreadyReady) && alreadyReady)
        {
            return;
        }

        readyStates[player] = true;
        player.SetReadyState(true);

        Debug.Log($"[RoundManager] Ready: {player.gameObject.name}");

        LogReadyStates();
        TryStartFirstRound();
    }

    private void TryStartFirstRound()
    {
        if (phase != GamePhase.WaitingForReady)
        {
            return;
        }

        List<PlayerHealth> validPlayers = GetValidPlayers();

        if (validPlayers.Count < minPlayersToStart)
        {
            Debug.Log(
                $"[RoundManager] Waiting for players. " +
                $"Players={validPlayers.Count}, Required={minPlayersToStart}"
            );
            return;
        }

        foreach (PlayerHealth player in validPlayers)
        {
            if (!readyStates.TryGetValue(player, out bool ready) || !ready)
            {
                Debug.Log("[RoundManager] Waiting for all players to ready.");
                return;
            }
        }

        currentRound = 1;
        BeginSkillSelectionForRound(currentRound);
    }

    private void BeginSkillSelectionForRound(int roundNumber)
    {
        StopSkillSelectionCoroutines();

        if (waitingRoomUI != null)
        {
            waitingRoomUI.HideRoomUI();
        }

        currentRound = roundNumber;
        phase = GamePhase.SkillSelecting;

        ClearHealthItems();
        DespawnProjectiles();

        RespawnAllPlayersWithoutOverlap();

        ResetSkillSelectionStates();

        Debug.Log($"[RoundManager] Round {currentRound} Skill Selection Start.");
        Debug.Log($"[RoundManager] Select skill within {skillSelectionDuration} seconds.");
        Debug.Log("[RoundManager] During skill selection, players cannot move.");
        LogSkillSlots();

        skillSelectionCoroutine = StartCoroutine(SkillSelectionTimeoutCoroutine());
    }

    private void ResetSkillSelectionStates()
    {
        List<PlayerHealth> validPlayers = GetValidPlayers();

        foreach (PlayerHealth player in validPlayers)
        {
            skillSelectedStates[player] = false;
            selectedSkills[player] = PlayerSkillType.None;
        }
    }

    public void SelectSkillBySlot(PlayerHealth player, int slotIndex)
    {
        if (phase != GamePhase.SkillSelecting)
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        if (!players.Contains(player))
        {
            Debug.LogWarning($"[RoundManager] Skill selection ignored. Player is not registered: {player.gameObject.name}");
            return;
        }

        if (skillSelectedStates.TryGetValue(player, out bool alreadySelected) && alreadySelected)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= availableRoundSkills.Count)
        {
            Debug.LogWarning($"[RoundManager] Invalid skill slot: {slotIndex + 1}");
            return;
        }

        PlayerSkillType skill = availableRoundSkills[slotIndex];

        SelectSkill(player, skill, false);
    }

    private void SelectSkill(PlayerHealth player, PlayerSkillType skill, bool isAutoSelect)
    {
        if (player == null)
        {
            return;
        }

        PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

        if (skillController == null)
        {
            Debug.LogWarning($"[RoundManager] PlayerSkillController was not found: {player.gameObject.name}");
            skill = PlayerSkillType.None;
        }
        else if (!skillController.CanSelectSkill(skill))
        {
            Debug.Log(
                $"[RoundManager] {player.gameObject.name} cannot select {skill} because it was used last round."
            );

            if (!isAutoSelect)
            {
                return;
            }

            skill = PlayerSkillType.None;
        }

        selectedSkills[player] = skill;
        skillSelectedStates[player] = true;

        Debug.Log(
            $"[RoundManager] Skill Selected: {player.gameObject.name}, " +
            $"Skill={skill}, Auto={isAutoSelect}"
        );

        LogSkillSelectionStates();
        TryFinishSkillSelection();
    }

    private void TryFinishSkillSelection()
    {
        if (phase != GamePhase.SkillSelecting)
        {
            return;
        }

        List<PlayerHealth> validPlayers = GetValidPlayers();

        if (validPlayers.Count == 0)
        {
            return;
        }

        foreach (PlayerHealth player in validPlayers)
        {
            if (!skillSelectedStates.TryGetValue(player, out bool selected) || !selected)
            {
                return;
            }
        }

        StopSkillSelectionCoroutineOnly();

        if (startRoundCoroutine != null)
        {
            return;
        }

        startRoundCoroutine = StartCoroutine(StartRoundAfterSkillSelectionCoroutine());
    }

    private IEnumerator SkillSelectionTimeoutCoroutine()
    {
        yield return new WaitForSeconds(skillSelectionDuration);

        if (phase != GamePhase.SkillSelecting)
        {
            yield break;
        }

        Debug.Log("[RoundManager] Skill selection timeout. Auto selecting missing skills.");

        AutoSelectMissingSkills();
        TryFinishSkillSelection();
    }

    private void AutoSelectMissingSkills()
    {
        List<PlayerHealth> validPlayers = GetValidPlayers();

        foreach (PlayerHealth player in validPlayers)
        {
            if (skillSelectedStates.TryGetValue(player, out bool selected) && selected)
            {
                continue;
            }

            PlayerSkillType autoSkill = GetAutoSelectableSkill(player);
            SelectSkill(player, autoSkill, true);
        }
    }

    private PlayerSkillType GetAutoSelectableSkill(PlayerHealth player)
    {
        if (player == null)
        {
            return PlayerSkillType.None;
        }

        PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

        foreach (PlayerSkillType skill in availableRoundSkills)
        {
            if (skill == PlayerSkillType.None)
            {
                continue;
            }

            if (skillController == null || skillController.CanSelectSkill(skill))
            {
                return skill;
            }
        }

        return PlayerSkillType.None;
    }

    private IEnumerator StartRoundAfterSkillSelectionCoroutine()
    {
        phase = GamePhase.RoundStarting;

        ApplySelectedSkillsForCurrentRound();

        Debug.Log(
            $"[RoundManager] All players selected skills. " +
            $"Round {currentRound} starts in {startAfterAllSkillsSelectedDelay} seconds."
        );

        yield return new WaitForSeconds(startAfterAllSkillsSelectedDelay);

        phase = GamePhase.RoundPlaying;
        startRoundCoroutine = null;

        SetupHealthItemsForCurrentPlayers();

        Debug.Log($"Round {currentRound} Start");
    }

    private void ApplySelectedSkillsForCurrentRound()
    {
        List<PlayerHealth> validPlayers = GetValidPlayers();

        foreach (PlayerHealth player in validPlayers)
        {
            PlayerSkillType selectedSkill = PlayerSkillType.None;

            if (selectedSkills.TryGetValue(player, out PlayerSkillType skill))
            {
                selectedSkill = skill;
            }

            PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

            if (skillController == null)
            {
                Debug.LogWarning($"[RoundManager] PlayerSkillController was not found: {player.gameObject.name}");
                continue;
            }

            skillController.PrepareForRound(selectedSkill);
        }
    }

    private void StopSkillSelectionCoroutineOnly()
    {
        if (skillSelectionCoroutine != null)
        {
            StopCoroutine(skillSelectionCoroutine);
            skillSelectionCoroutine = null;
        }
    }

    private void StopSkillSelectionCoroutines()
    {
        if (skillSelectionCoroutine != null)
        {
            StopCoroutine(skillSelectionCoroutine);
            skillSelectionCoroutine = null;
        }

        if (startRoundCoroutine != null)
        {
            StopCoroutine(startRoundCoroutine);
            startRoundCoroutine = null;
        }
    }

    private void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        if (deadPlayer == null)
        {
            return;
        }

        Debug.Log($"[RoundManager] HandlePlayerDied: {deadPlayer.gameObject.name}");

        if (phase != GamePhase.RoundPlaying)
        {
            Debug.Log($"[RoundManager] Death ignored. Current phase={phase}");
            return;
        }

        List<PlayerHealth> alivePlayers = players
            .Where(player => player != null && !player.IsDead)
            .ToList();

        Debug.Log($"[RoundManager] Alive Count: {alivePlayers.Count} / Registered Count: {players.Count}");

        foreach (PlayerHealth player in alivePlayers)
        {
            Debug.Log($"[RoundManager] Alive: {player.gameObject.name}");
        }

        if (alivePlayers.Count > 1)
        {
            return;
        }

        PlayerHealth roundWinner = alivePlayers.Count == 1 ? alivePlayers[0] : null;

        StartCoroutine(EndRoundCoroutine(roundWinner));
    }

    private IEnumerator EndRoundCoroutine(PlayerHealth roundWinner)
    {
        Debug.Log("[RoundManager] EndRoundCoroutine started");

        phase = GamePhase.RoundEnding;

        ClearHealthItems();

        if (roundWinner != null)
        {
            points[roundWinner]++;

            Debug.Log(
                $"{roundWinner.gameObject.name} wins Round {currentRound}. " +
                $"Point: {points[roundWinner]}"
            );
        }
        else
        {
            Debug.Log($"Round {currentRound} ended with no winner.");
        }

        yield return new WaitForSeconds(nextRoundDelay);

        DespawnProjectiles();

        if (currentRound >= maxRoundCount)
        {
            yield return StartCoroutine(FinishMatchAndReturnToWaitingCoroutine());
            yield break;
        }

        currentRound++;

        BeginSkillSelectionForRound(currentRound);
    }

    private IEnumerator FinishMatchAndReturnToWaitingCoroutine()
    {
        phase = GamePhase.MatchFinished;

        ClearHealthItems();
        DespawnProjectiles();
        StopSkillSelectionCoroutines();

        LogMatchResult();

        Debug.Log($"[RoundManager] Match finished. Returning to waiting state in {returnToWaitingDelay} seconds.");

        yield return new WaitForSeconds(returnToWaitingDelay);

        ResetMatchStateToWaiting();
    }

    private void ResetMatchStateToWaiting()
    {
        ClearHealthItems();
        DespawnProjectiles();
        StopSkillSelectionCoroutines();

        currentRound = 1;

        List<PlayerHealth> validPlayers = GetValidPlayers();

        foreach (PlayerHealth player in validPlayers)
        {
            points[player] = 0;
            readyStates[player] = false;
            skillSelectedStates[player] = false;
            selectedSkills[player] = PlayerSkillType.None;

            player.SetReadyState(false);

            PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

            if (skillController != null)
            {
                skillController.ResetSkillHistory();
            }
        }

        RespawnAllPlayersWithoutOverlap();

        phase = GamePhase.WaitingForReady;

        if (waitingRoomUI != null)
        {
            waitingRoomUI.ShowRoomUI();
        }

        Debug.Log("[RoundManager] Returned to waiting state.");
        Debug.Log("[RoundManager] Press Enter or ZL to ready.");
        LogReadyStates();
    }

    private void LogMatchResult()
    {
        if (points.Count == 0)
        {
            Debug.Log("Match finished, but no players were registered.");
            return;
        }

        int highestPoint = points.Values.Max();

        List<PlayerHealth> winners = points
            .Where(pair => pair.Value == highestPoint)
            .Select(pair => pair.Key)
            .Where(player => player != null)
            .ToList();

        if (winners.Count == 1)
        {
            Debug.Log($"Match Winner: {winners[0].gameObject.name}, Point: {highestPoint}");
        }
        else
        {
            string winnerNames = string.Join(", ", winners.Select(player => player.gameObject.name));
            Debug.Log($"Match Draw: {winnerNames}, Point: {highestPoint}");
        }
    }

    private void RespawnAllPlayersWithoutOverlap()
    {
        List<PlayerHealth> validPlayers = GetValidPlayers();
        List<Transform> availableSpawnPoints = GetAvailableSpawnPoints();

        if (availableSpawnPoints.Count == 0)
        {
            Debug.LogWarning(
                "[RoundManager] No spawn points assigned. " +
                "All players will respawn at RoundManager position."
            );
        }

        if (availableSpawnPoints.Count < validPlayers.Count)
        {
            Debug.LogWarning(
                $"[RoundManager] SpawnPoint count is less than player count. " +
                $"Players={validPlayers.Count}, SpawnPoints={availableSpawnPoints.Count}. " +
                $"Some players may share spawn points."
            );
        }

        if (shuffleSpawnPointsEachRound)
        {
            Shuffle(availableSpawnPoints);
        }

        for (int i = 0; i < validPlayers.Count; i++)
        {
            PlayerHealth player = validPlayers[i];
            Transform spawnPoint = GetSpawnPoint(availableSpawnPoints, i);

            Vector3 spawnPosition = spawnPoint != null
                ? spawnPoint.position
                : transform.position;

            Debug.Log(
                $"[RoundManager] Respawn {player.gameObject.name} " +
                $"Index={i}, " +
                $"SpawnPoint={(spawnPoint != null ? spawnPoint.name : "RoundManager")}, " +
                $"Position={spawnPosition}"
            );

            player.RespawnForRound(spawnPoint);
        }
    }

    private List<PlayerHealth> GetValidPlayers()
    {
        return players
            .Where(player => player != null)
            .ToList();
    }

    private List<Transform> GetAvailableSpawnPoints()
    {
        List<Transform> result = spawnPoints
            .Where(spawnPoint => spawnPoint != null)
            .Distinct()
            .ToList();

        if (result.Count > 0)
        {
            WarnIfSpawnPointsOverlap(result);
            return result;
        }

        if (spawnPointsRoot != null)
        {
            result = new List<Transform>();

            for (int i = 0; i < spawnPointsRoot.childCount; i++)
            {
                Transform child = spawnPointsRoot.GetChild(i);

                if (child != null)
                {
                    result.Add(child);
                }
            }

            result = result
                .Where(spawnPoint => spawnPoint != null)
                .Distinct()
                .ToList();

            WarnIfSpawnPointsOverlap(result);
            return result;
        }

        return result;
    }

    private Transform GetSpawnPoint(List<Transform> availableSpawnPoints, int playerIndex)
    {
        if (availableSpawnPoints == null || availableSpawnPoints.Count == 0)
        {
            return transform;
        }

        return availableSpawnPoints[playerIndex % availableSpawnPoints.Count];
    }

    private void Shuffle(List<Transform> list)
    {
        if (list == null)
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            Transform temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private void SetupHealthItemsForCurrentPlayers()
    {
        HealthItemSpawner itemSpawner = GetHealthItemSpawner();

        if (itemSpawner == null)
        {
            Debug.LogWarning("[RoundManager] HealthItemSpawner was not found.");
            return;
        }

        itemSpawner.SetupItemsForPlayerCount(RegisteredPlayerCount);
    }

    private void ClearHealthItems()
    {
        HealthItemSpawner itemSpawner = GetHealthItemSpawner();

        if (itemSpawner == null)
        {
            return;
        }

        itemSpawner.ClearAllItems();
    }

    private HealthItemSpawner GetHealthItemSpawner()
    {
        if (healthItemSpawner != null)
        {
            return healthItemSpawner;
        }

        healthItemSpawner = FindFirstObjectByType<HealthItemSpawner>();
        return healthItemSpawner;
    }

    private void LogReadyStates()
    {
        List<PlayerHealth> validPlayers = GetValidPlayers();

        int readyCount = 0;

        foreach (PlayerHealth player in validPlayers)
        {
            bool ready = readyStates.TryGetValue(player, out bool value) && value;

            if (ready)
            {
                readyCount++;
            }

            Debug.Log(
                $"[RoundManager] ReadyState: " +
                $"{player.gameObject.name}, Ready={ready}"
            );
        }

        Debug.Log(
            $"[RoundManager] Ready Count: " +
            $"{readyCount}/{validPlayers.Count}, RequiredPlayers={minPlayersToStart}"
        );
    }

    private void LogSkillSelectionStates()
    {
        List<PlayerHealth> validPlayers = GetValidPlayers();

        int selectedCount = 0;

        foreach (PlayerHealth player in validPlayers)
        {
            bool selected = skillSelectedStates.TryGetValue(player, out bool value) && value;
            PlayerSkillType skill = selectedSkills.TryGetValue(player, out PlayerSkillType selectedSkill)
                ? selectedSkill
                : PlayerSkillType.None;

            if (selected)
            {
                selectedCount++;
            }

            Debug.Log(
                $"[RoundManager] SkillState: " +
                $"{player.gameObject.name}, Selected={selected}, Skill={skill}"
            );
        }

        Debug.Log(
            $"[RoundManager] Skill Selected Count: " +
            $"{selectedCount}/{validPlayers.Count}"
        );
    }

    private void LogSkillSlots()
    {
        Debug.Log("[RoundManager] Skill Slots:");

        for (int i = 0; i < availableRoundSkills.Count; i++)
        {
            Debug.Log($"[RoundManager] Key {i + 1}: {availableRoundSkills[i]}");
        }
    }

    private void LogSpawnPointSettings()
    {
        List<Transform> availableSpawnPoints = GetAvailableSpawnPoints();

        Debug.Log($"[RoundManager] Available SpawnPoint Count: {availableSpawnPoints.Count}");

        for (int i = 0; i < availableSpawnPoints.Count; i++)
        {
            Transform spawnPoint = availableSpawnPoints[i];

            Debug.Log(
                $"[RoundManager] SpawnPoint[{i}] " +
                $"Name={spawnPoint.name}, " +
                $"Position={spawnPoint.position}"
            );
        }
    }

    private void WarnIfSpawnPointsOverlap(List<Transform> targetSpawnPoints)
    {
        for (int i = 0; i < targetSpawnPoints.Count; i++)
        {
            for (int j = i + 1; j < targetSpawnPoints.Count; j++)
            {
                Transform a = targetSpawnPoints[i];
                Transform b = targetSpawnPoints[j];

                if (a == null || b == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(a.position, b.position);

                if (distance < 0.1f)
                {
                    Debug.LogWarning(
                        $"[RoundManager] SpawnPoints are too close. " +
                        $"{a.name} and {b.name}, Distance={distance}"
                    );
                }
            }
        }
    }

    private void DespawnProjectiles()
    {
        Projectile[] projectiles = FindObjectsByType<Projectile>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Projectile projectile in projectiles)
        {
            if (projectile == null)
            {
                continue;
            }

            NetworkObject networkObject = projectile.Object;

            if (networkObject == null)
            {
                continue;
            }

            if (!networkObject.HasStateAuthority)
            {
                continue;
            }

            projectile.Runner.Despawn(networkObject);
        }
    }
    
    public override void Render()
    {
        if (!uiPhaseInitialized)
        {
            ApplyUIForPhase(phase, false);

            lastAppliedUIPhase = phase;
            uiPhaseInitialized = true;
            return;
        }

        if (lastAppliedUIPhase == phase)
        {
            return;
        }

        bool enteredRoundPlaying =
            lastAppliedUIPhase != GamePhase.RoundPlaying &&
            phase == GamePhase.RoundPlaying;

        ApplyUIForPhase(phase, enteredRoundPlaying);

        lastAppliedUIPhase = phase;
    }

    private void ApplyUIForPhase(GamePhase targetPhase, bool playBattleStartUI)
    {
        bool shouldShowWaitingRoom =
            targetPhase == GamePhase.WaitingForReady;

        if (waitingRoomUI != null)
        {
            if (shouldShowWaitingRoom)
            {
                waitingRoomUI.ShowRoomUI();
            }
            else
            {
                waitingRoomUI.HideRoomUI();
            }
        }

        if (playBattleStartUI && battleStartUI != null)
        {
            battleStartUI.StopAllCoroutines();
            battleStartUI.StartCoroutine(battleStartUI.PlaySequence());
        }
    }
}