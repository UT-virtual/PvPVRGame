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
        PlayerSkillType.DamageReduction,
        PlayerSkillType.XRayVision,
        PlayerSkillType.MoveSpeedUp,
        PlayerSkillType.SlowFall,
        PlayerSkillType.Shrink,
        PlayerSkillType.DelayedDamageInvincible,
        PlayerSkillType.InstantReload,

        PlayerSkillType.GravityBurstReload,
        PlayerSkillType.HeavyBulletReload,
        PlayerSkillType.NextShotDamageBoost
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
    [SerializeField] private RoundEndUI roundEndUI;

    [Header("Skill Selection UI")]
    [SerializeField] private int skillOptionSlotCount = 4;

    [Header("Debug Spectator Dummy")]
    [SerializeField] private bool countDebugSpectatorDummiesAsAlive = false;

    private readonly PlayerSkillType[] currentSkillOptions = new PlayerSkillType[4];
    private readonly PlayerSkillType[] secondSkillOptions = new PlayerSkillType[4];

    public int SkillOptionSlotCount => currentSkillOptions.Length;
    public int RequiredSkillSelectionCount => 2;
    public float SkillSelectionDuration => skillSelectionDuration;

    public PlayerSkillType GetSkillOption(int slotIndex)
    {
        return GetSkillOption(slotIndex, 0);
    }

    public PlayerSkillType GetSkillOption(int slotIndex, int selectionIndex)
    {
        if (slotIndex < 0 || slotIndex >= currentSkillOptions.Length)
        {
            return PlayerSkillType.None;
        }

        PlayerSkillType[] targetOptions = selectionIndex <= 0
            ? currentSkillOptions
            : secondSkillOptions;

        return targetOptions[slotIndex];
    }

    private readonly List<PlayerHealth> players = new();
    private readonly Dictionary<PlayerHealth, int> points = new();
    private readonly Dictionary<PlayerHealth, bool> readyStates = new();
    private readonly Dictionary<PlayerHealth, bool> skillSelectedStates = new();
    private readonly Dictionary<PlayerHealth, int> skillSelectionCounts = new();
    private readonly Dictionary<PlayerHealth, PlayerSkillType> selectedSkills = new();
    private readonly Dictionary<PlayerHealth, PlayerSkillType> selectedSecondSkills = new();

    public int currentRound = 1;
    [Networked]
    private GamePhase phase { get; set; }

    private bool uiPhaseInitialized;
    private GamePhase lastAppliedUIPhase;

    private Coroutine skillSelectionCoroutine;
    private Coroutine startRoundCoroutine;

    private bool isSpawned;

    private bool CanReadNetworkedPhase =>
        isSpawned && Object != null;

    public bool CanUseWeapons =>
        CanReadNetworkedPhase &&
        phase == GamePhase.RoundPlaying;

    public bool CanControlPlayers =>
        CanReadNetworkedPhase &&
        (
            phase == GamePhase.WaitingForReady ||
            phase == GamePhase.RoundPlaying
        );

    public bool IsWaitingForReady =>
        CanReadNetworkedPhase &&
        phase == GamePhase.WaitingForReady;

    public bool IsSkillSelecting =>
        CanReadNetworkedPhase &&
        phase == GamePhase.SkillSelecting;

    public bool IsRoundPlaying =>
        CanReadNetworkedPhase &&
        phase == GamePhase.RoundPlaying;

    public bool IsMatchFinished =>
        CanReadNetworkedPhase &&
        phase == GamePhase.MatchFinished;

    public bool CanShowWaitingRoomUI =>
        CanReadNetworkedPhase &&
        phase == GamePhase.WaitingForReady;

    public bool IsNetworkReady =>
        CanReadNetworkedPhase;

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

        if (roundEndUI == null)
        {
            roundEndUI = FindFirstObjectByType<RoundEndUI>();
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
        skillSelectionCounts[player] = 0;
        selectedSkills[player] = PlayerSkillType.None;
        selectedSecondSkills[player] = PlayerSkillType.None;

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
        skillSelectionCounts.Remove(player);
        selectedSkills.Remove(player);
        selectedSecondSkills.Remove(player);

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

        BuildSkillOptionsForRound();

        phase = GamePhase.SkillSelecting;

        ClearHealthItems();
        DespawnProjectiles();

        RespawnAllPlayersWithoutOverlap();

        ResetSkillSelectionStates();

        Debug.Log($"[RoundManager] Round {currentRound} Skill Selection Start.");
        Debug.Log($"[RoundManager] Select skill within {skillSelectionDuration} seconds.");
        Debug.Log("[RoundManager] During skill selection, players cannot move.");

        skillSelectionCoroutine = StartCoroutine(SkillSelectionTimeoutCoroutine());
    }

    private void ResetSkillSelectionStates()
    {
        List<PlayerHealth> validPlayers = GetValidPlayers();

        foreach (PlayerHealth player in validPlayers)
        {
            skillSelectedStates[player] = false;
            skillSelectionCounts[player] = 0;
            selectedSkills[player] = PlayerSkillType.None;
            selectedSecondSkills[player] = PlayerSkillType.None;
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

        int selectionIndex = GetSkillSelectionCount(player);

        if (selectionIndex >= RequiredSkillSelectionCount)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= currentSkillOptions.Length)
        {
            Debug.LogWarning($"[RoundManager] Invalid skill slot: {slotIndex + 1}");
            return;
        }

        PlayerSkillType skill = GetSkillOption(slotIndex, selectionIndex);

        if (skill == PlayerSkillType.None)
        {
            Debug.LogWarning($"[RoundManager] Empty skill slot: {slotIndex + 1}");
            return;
        }

        SelectSkill(player, skill, false);
    }

    private int GetSkillSelectionCount(PlayerHealth player)
    {
        if (player == null)
        {
            return 0;
        }

        return skillSelectionCounts.TryGetValue(player, out int count)
            ? count
            : 0;
    }

    private void SelectSkill(PlayerHealth player, PlayerSkillType skill, bool isAutoSelect)
    {
        if (player == null)
        {
            return;
        }

        int selectionIndex = GetSkillSelectionCount(player);

        if (selectionIndex >= RequiredSkillSelectionCount)
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

        if (selectionIndex > 0 &&
            skill != PlayerSkillType.None &&
            selectedSkills.TryGetValue(player, out PlayerSkillType firstSkill) &&
            firstSkill == skill)
        {
            Debug.LogWarning($"[RoundManager] Same skill selected twice in same round: {skill}");
            return;
        }

        if (selectionIndex == 0)
        {
            selectedSkills[player] = skill;
        }
        else
        {
            selectedSecondSkills[player] = skill;
        }

        int newSelectionCount = selectionIndex + 1;
        skillSelectionCounts[player] = newSelectionCount;
        skillSelectedStates[player] = newSelectionCount >= RequiredSkillSelectionCount;

        Debug.Log(
            $"[RoundManager] Skill Selected: {player.gameObject.name}, " +
            $"Index={newSelectionCount}/{RequiredSkillSelectionCount}, " +
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
            int selectedCount = GetSkillSelectionCount(player);

            if (selectedCount < RequiredSkillSelectionCount)
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

    private void AutoSelectMissingSkills()
    {
        List<PlayerHealth> validPlayers = GetValidPlayers();

        foreach (PlayerHealth player in validPlayers)
        {
            while (GetSkillSelectionCount(player) < RequiredSkillSelectionCount)
            {
                int selectionIndex = GetSkillSelectionCount(player);
                PlayerSkillType autoSkill = GetAutoSelectableSkill(player, selectionIndex);

                SelectSkill(player, autoSkill, true);
            }
        }
    }

    private PlayerSkillType GetAutoSelectableSkill(PlayerHealth player, int selectionIndex)
    {
        if (player == null)
        {
            return PlayerSkillType.None;
        }

        PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

        for (int i = 0; i < SkillOptionSlotCount; i++)
        {
            PlayerSkillType skill = GetSkillOption(i, selectionIndex);

            if (skill == PlayerSkillType.None)
            {
                continue;
            }

            if (selectionIndex > 0 &&
                selectedSkills.TryGetValue(player, out PlayerSkillType firstSkill) &&
                firstSkill == skill)
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
            PlayerSkillType selectedFirstSkill = PlayerSkillType.None;
            PlayerSkillType selectedSecondSkill = PlayerSkillType.None;

            if (selectedSkills.TryGetValue(player, out PlayerSkillType firstSkill))
            {
                selectedFirstSkill = firstSkill;
            }

            if (selectedSecondSkills.TryGetValue(player, out PlayerSkillType secondSkill))
            {
                selectedSecondSkill = secondSkill;
            }

            PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

            if (skillController == null)
            {
                Debug.LogWarning($"[RoundManager] PlayerSkillController was not found: {player.gameObject.name}");
                continue;
            }

            skillController.PrepareForRound(selectedFirstSkill, selectedSecondSkill);
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

        int debugAliveDummyCount = countDebugSpectatorDummiesAsAlive
            ? CountAliveDebugSpectatorDummies()
            : 0;

        int aliveCountForRoundEnd = alivePlayers.Count + debugAliveDummyCount;

        Debug.Log(
            $"[RoundManager] Alive Count: {alivePlayers.Count} / Registered Count: {players.Count}, " +
            $"DebugAliveDummyCount={debugAliveDummyCount}, " +
            $"AliveCountForRoundEnd={aliveCountForRoundEnd}"
        );

        foreach (PlayerHealth player in alivePlayers)
        {
            Debug.Log($"[RoundManager] Alive: {player.gameObject.name}");
        }

        if (aliveCountForRoundEnd > 1)
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

        if (roundEndUI == null)
        {
            roundEndUI = FindFirstObjectByType<RoundEndUI>();
        }

        if (roundEndUI != null)
        {
            roundEndUI.Show(currentRound, roundWinner);
        }

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

        if (roundEndUI != null)
        {
            roundEndUI.Hide();
        }

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
            skillSelectionCounts[player] = 0;
            selectedSkills[player] = PlayerSkillType.None;
            selectedSecondSkills[player] = PlayerSkillType.None;

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

        int completedCount = 0;

        foreach (PlayerHealth player in validPlayers)
        {
            int selectedCount = GetSkillSelectionCount(player);

            PlayerSkillType firstSkill = selectedSkills.TryGetValue(player, out PlayerSkillType selectedFirstSkill)
                ? selectedFirstSkill
                : PlayerSkillType.None;

            PlayerSkillType secondSkill = selectedSecondSkills.TryGetValue(player, out PlayerSkillType selectedSecondSkill)
                ? selectedSecondSkill
                : PlayerSkillType.None;

            bool completed = selectedCount >= RequiredSkillSelectionCount;

            if (completed)
            {
                completedCount++;
            }

            Debug.Log(
                $"[RoundManager] SkillState: " +
                $"{player.gameObject.name}, " +
                $"Count={selectedCount}/{RequiredSkillSelectionCount}, " +
                $"Completed={completed}, " +
                $"First={firstSkill}, Second={secondSkill}"
            );
        }

        Debug.Log(
            $"[RoundManager] Skill Selected Count: " +
            $"{completedCount}/{validPlayers.Count}"
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

        bool enteredRoundStarting =
            lastAppliedUIPhase != GamePhase.RoundStarting &&
            phase == GamePhase.RoundStarting;

        ApplyUIForPhase(phase, enteredRoundStarting);

        lastAppliedUIPhase = phase;
    }

    private void ApplyUIForPhase(GamePhase targetPhase, bool playBattleStartUI)
    {
        Debug.Log(
            $"[RoundManager] ApplyUIForPhase: " +
            $"Phase={targetPhase}, " +
            $"PlayBattleStartUI={playBattleStartUI}, " +
            $"HasStateAuthority={Object.HasStateAuthority}"
        );

        bool shouldShowWaitingRoom =
            targetPhase == GamePhase.WaitingForReady;

        if (waitingRoomUI == null)
        {
            waitingRoomUI = FindFirstObjectByType<WaitingRoomUI>();
        }

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
        else
        {
            Debug.LogWarning("[RoundManager] WaitingRoomUI was not found.");
        }

        if (playBattleStartUI && battleStartUI != null)
        {
            battleStartUI.StopAllCoroutines();
            battleStartUI.StartCoroutine(battleStartUI.PlaySequence());
        }
        else if (playBattleStartUI && battleStartUI == null)
        {
            Debug.LogWarning("[RoundManager] BattleStartUI was not found.");
        }
    }

    private void BuildSkillOptionsForRound()
    {
        List<PlayerSkillType> candidates = availableRoundSkills
            .Where(skill => skill != PlayerSkillType.None)
            .Distinct()
            .ToList();

        FillSkillOptionArray(currentSkillOptions, candidates);

        HashSet<PlayerSkillType> firstOptionSet = currentSkillOptions
            .Where(skill => skill != PlayerSkillType.None)
            .ToHashSet();

        List<PlayerSkillType> secondCandidates = candidates
            .Where(skill => !firstOptionSet.Contains(skill))
            .ToList();

        FillSkillOptionArray(secondSkillOptions, secondCandidates);

        RPC_SetSkillOptions(
            currentSkillOptions[0],
            currentSkillOptions[1],
            currentSkillOptions[2],
            currentSkillOptions[3],
            secondSkillOptions[0],
            secondSkillOptions[1],
            secondSkillOptions[2],
            secondSkillOptions[3]
        );
    }

    private void FillSkillOptionArray(PlayerSkillType[] targetOptions, List<PlayerSkillType> candidates)
    {
        if (targetOptions == null)
        {
            return;
        }

        for (int i = 0; i < targetOptions.Length; i++)
        {
            targetOptions[i] = PlayerSkillType.None;
        }

        if (candidates == null || candidates.Count == 0)
        {
            return;
        }

        List<PlayerSkillType> shuffledCandidates = candidates
            .Where(skill => skill != PlayerSkillType.None)
            .Distinct()
            .ToList();

        ShuffleSkillList(shuffledCandidates);

        for (int i = 0; i < targetOptions.Length; i++)
        {
            targetOptions[i] = i < shuffledCandidates.Count
                ? shuffledCandidates[i]
                : PlayerSkillType.None;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetSkillOptions(
        PlayerSkillType slot0,
        PlayerSkillType slot1,
        PlayerSkillType slot2,
        PlayerSkillType slot3,
        PlayerSkillType secondSlot0,
        PlayerSkillType secondSlot1,
        PlayerSkillType secondSlot2,
        PlayerSkillType secondSlot3
    )
    {
        currentSkillOptions[0] = slot0;
        currentSkillOptions[1] = slot1;
        currentSkillOptions[2] = slot2;
        currentSkillOptions[3] = slot3;

        secondSkillOptions[0] = secondSlot0;
        secondSkillOptions[1] = secondSlot1;
        secondSkillOptions[2] = secondSlot2;
        secondSkillOptions[3] = secondSlot3;

        Debug.Log(
            $"[RoundManager] First Skill Options: " +
            $"1={slot0}, 2={slot1}, 3={slot2}, 4={slot3}"
        );

        Debug.Log(
            $"[RoundManager] Second Skill Options: " +
            $"1={secondSlot0}, 2={secondSlot1}, 3={secondSlot2}, 4={secondSlot3}"
        );
    }

    private void ShuffleSkillList(List<PlayerSkillType> list)
    {
        if (list == null)
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            PlayerSkillType temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
    private int CountAliveDebugSpectatorDummies()
    {
        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        int count = 0;

        foreach (PlayerHealth player in allPlayers)
        {
            if (player == null)
            {
                continue;
            }

            if (players.Contains(player))
            {
                continue;
            }

            if (player.IsDead)
            {
                continue;
            }

            if (!player.gameObject.name.StartsWith("DebugSpectatorDummy"))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}