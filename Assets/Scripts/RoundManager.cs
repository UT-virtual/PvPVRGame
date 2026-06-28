using Fusion;
using SlimUI.ModernMenu;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }
    public enum TeamColor
    {
        Red,
        Blue,
        Green,
        Yellow
    }

    private enum GamePhase
    {
        WaitingForReady,
        RoundPlaying,
        RoundEnding,
        MatchFinished
    }

    [Header("Ready")]
    [SerializeField] private int minPlayersToStart = 2;

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
    [SerializeField] private WaitingRoomUI waitingRoomUI;

    private readonly List<PlayerHealth> players = new();
    private readonly Dictionary<PlayerHealth, int> points = new();
    private readonly Dictionary<PlayerHealth, bool> readyStates = new();

    private int currentRound = 1;
    private GamePhase phase = GamePhase.WaitingForReady;

    public bool CanUseWeapons => phase == GamePhase.RoundPlaying;
    public bool IsWaitingForReady => phase == GamePhase.WaitingForReady;
    public bool IsRoundPlaying => phase == GamePhase.RoundPlaying;
    public bool IsMatchFinished => phase == GamePhase.MatchFinished;

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

    private void Start()
    {
        phase = GamePhase.WaitingForReady;

        Debug.Log("[RoundManager] Waiting for players to ready.");
        Debug.Log("[RoundManager] Press Enter or ZL to ready.");

        LogSpawnPointSettings();
    }

    private readonly TeamColor[] teamOrder =
    {
        TeamColor.Red,
        TeamColor.Blue,
        TeamColor.Green,
        TeamColor.Yellow
    };

    private void AssignTeamColor(PlayerHealth player)
    {
        PlayerTeam playerTeam = player.GetComponent<PlayerTeam>();

        if (playerTeam == null)
        {
            playerTeam = player.gameObject.AddComponent<PlayerTeam>();
        }

        int index = players.Count - 1;

        TeamColor assignedColor =
            teamOrder[index % teamOrder.Length];

        playerTeam.SetTeam(assignedColor);

        Debug.Log(
            $"{player.gameObject.name} joined as {assignedColor}"
        );
    }
    public void RegisterPlayer(PlayerHealth player)
    {
        if (player == null)
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
        AssignTeamColor(player);

        player.OnDied += HandlePlayerDied;

        Debug.Log(
            $"[RoundManager] Registered: {player.gameObject.name}, " +
            $"Count={players.Count}, Ready=False"
        );
    }

    public IReadOnlyList<PlayerHealth> Players => players;

    public bool IsPlayerReady(PlayerHealth player)
    {
        return readyStates.TryGetValue(player, out bool ready) && ready;
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

        Debug.Log($"[RoundManager] Unregistered: {player.gameObject.name}, Count={players.Count}");

        TryStartFirstRound();
    }

    public void SetPlayerReady(PlayerHealth player)
    {
        if (player == null)
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

        List<PlayerHealth> validPlayers = players
            .Where(player => player != null)
            .ToList();

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


        StartFirstRound();
    }

    private void StartFirstRound()
    {
        waitingRoomUI.HideRoomUI();
        currentRound = 1;
        phase = GamePhase.RoundPlaying;

        DespawnProjectiles();
        RespawnAllPlayersWithoutOverlap();
        SetupHealthItemsForCurrentPlayers();

        Debug.Log($"Round {currentRound} Start");
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

        RespawnAllPlayersWithoutOverlap();

        phase = GamePhase.RoundPlaying;

        SetupHealthItemsForCurrentPlayers();

        Debug.Log($"Round {currentRound} Start");
    }

    private IEnumerator FinishMatchAndReturnToWaitingCoroutine()
    {
        phase = GamePhase.MatchFinished;

        ClearHealthItems();
        DespawnProjectiles();

        LogMatchResult();

        Debug.Log($"[RoundManager] Match finished. Returning to waiting state in {returnToWaitingDelay} seconds.");

        yield return new WaitForSeconds(returnToWaitingDelay);

        ResetMatchStateToWaiting();
    }

    private void ResetMatchStateToWaiting()
    {
        ClearHealthItems();
        DespawnProjectiles();

        currentRound = 1;

        List<PlayerHealth> validPlayers = players
            .Where(player => player != null)
            .ToList();

        foreach (PlayerHealth player in validPlayers)
        {
            points[player] = 0;
            readyStates[player] = false;
        }

        RespawnAllPlayersWithoutOverlap();

        phase = GamePhase.WaitingForReady;

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
        List<PlayerHealth> validPlayers = players
            .Where(player => player != null)
            .ToList();

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
        List<PlayerHealth> validPlayers = players
            .Where(player => player != null)
            .ToList();

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
}