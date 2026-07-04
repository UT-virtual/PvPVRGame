using Fusion;
using System;
using System.Collections.Generic;
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

    private const int SkillOptionArrayLength = 4;

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
    [SerializeField] private FinalResultUI finalResultUI;

    [Header("Skill Selection UI")]
    [SerializeField] private int skillOptionSlotCount = SkillOptionArrayLength;

    [Header("Debug Spectator Dummy")]
    [SerializeField] private bool countDebugSpectatorDummiesAsAlive = false;

    public int currentRound = 1;

    [Networked]
    private GamePhase phase { get; set; }

    private bool uiPhaseInitialized;
    private GamePhase lastAppliedUIPhase;

    private bool isSpawned;

    private RoundPlayerRegistry playerRegistry;
    private RoundReadyController readyController;
    private RoundScoreCalculator scoreCalculator;
    private RoundSkillSelectionController skillSelectionController;
    private RoundFlowController flowController;
    private RoundSpawnController spawnController;
    private RoundHealthItemController healthItemController;
    private RoundUIController uiController;
    private RoundDebugLogger debugLogger;
    private RoundProjectileCleaner projectileCleaner;

    private readonly TeamColor[] teamOrder =
    {
        TeamColor.Red,
        TeamColor.Blue,
        TeamColor.Green,
        TeamColor.Yellow
    };

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

    public int RegisteredPlayerCount =>
        playerRegistry != null ? playerRegistry.RegisteredPlayerCount : 0;

    public IReadOnlyList<PlayerHealth> Players =>
        playerRegistry != null ? playerRegistry.Players : Array.Empty<PlayerHealth>();

    public int SkillOptionSlotCount =>
        skillSelectionController != null
            ? skillSelectionController.SkillOptionSlotCount
            : SkillOptionArrayLength;

    public int RequiredSkillSelectionCount => 2;
    public float SkillSelectionDuration => skillSelectionDuration;

    public class PlayerTeam : MonoBehaviour
    {
        public TeamColor Team { get; private set; }

        public void SetTeam(TeamColor team)
        {
            Team = team;
        }
    }

    public PlayerSkillType GetSkillOption(int slotIndex)
    {
        return GetSkillOption(slotIndex, 0);
    }

    public PlayerSkillType GetSkillOption(int slotIndex, int selectionIndex)
    {
        EnsureControllers();
        return skillSelectionController.GetSkillOption(slotIndex, selectionIndex);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureControllers();

        Instance = this;

        Debug.Log("[RoundManager] Awake");
    }

    public override void Spawned()
    {
        EnsureControllers();

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

    public override void Render()
    {
        EnsureControllers();

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

    private void EnsureControllers()
    {
        if (playerRegistry != null)
        {
            return;
        }

        playerRegistry = new RoundPlayerRegistry(teamOrder, HandlePlayerDied);
        readyController = new RoundReadyController();
        scoreCalculator = new RoundScoreCalculator();

        spawnController = new RoundSpawnController(
            transform,
            spawnPointsRoot,
            spawnPoints,
            shuffleSpawnPointsEachRound
        );

        healthItemController = new RoundHealthItemController(
            healthItemSpawner,
            () => RegisteredPlayerCount
        );

        uiController = new RoundUIController(
            waitingRoomUI,
            battleStartUI,
            roundEndUI,
            finalResultUI,
            () => Object != null && Object.HasStateAuthority
        );

        debugLogger = new RoundDebugLogger();
        projectileCleaner = new RoundProjectileCleaner();

        skillSelectionController = new RoundSkillSelectionController(
            this,
            () => phase,
            value => phase = value,
            () => currentRound,
            value => currentRound = value,
            GetValidPlayers,
            player => playerRegistry.Contains(player),
            availableRoundSkills,
            SkillOptionArrayLength,
            RequiredSkillSelectionCount,
            () => skillSelectionDuration,
            () => startAfterAllSkillsSelectedDelay,
            new RoundSkillOptionBuilder(),
            spawnController,
            healthItemController,
            projectileCleaner,
            debugLogger,
            HideWaitingRoomUI,
            RPC_NotifySkillSelectionProgress,
            RPC_SetSkillOptions
        );

        flowController = new RoundFlowController(
            this,
            () => phase,
            value => phase = value,
            () => currentRound,
            value => currentRound = value,
            GetValidPlayers,
            () => RegisteredPlayerCount,
            () => countDebugSpectatorDummiesAsAlive,
            () => maxRoundCount,
            () => nextRoundDelay,
            () => returnToWaitingDelay,
            scoreCalculator,
            readyController,
            skillSelectionController,
            spawnController,
            healthItemController,
            projectileCleaner,
            ShowRoundEndUIRpc,
            HideRoundEndUIRpc,
            ShowFinalResultUIRpc,
            HideFinalResultUIRpc,
            ShowWaitingRoomUI,
            LogReadyStates
        );
    }

    public void RegisterPlayer(PlayerHealth player)
    {
        EnsureControllers();

        if (player == null)
        {
            return;
        }

        if (!Object.HasStateAuthority)
        {
            return;
        }

        bool registered = playerRegistry.RegisterPlayer(player);

        if (!registered)
        {
            return;
        }

        readyController.RegisterPlayer(player);
        scoreCalculator.RegisterPlayer(player);
        skillSelectionController.RegisterPlayer(player);

        Debug.Log(
            $"[RoundManager] Registered: {player.gameObject.name}, " +
            $"Count={RegisteredPlayerCount}, Ready=False"
        );
    }

    public void UnregisterPlayer(PlayerHealth player)
    {
        EnsureControllers();

        if (player == null)
        {
            return;
        }

        bool unregistered = playerRegistry.UnregisterPlayer(player);

        if (!unregistered)
        {
            return;
        }

        readyController.UnregisterPlayer(player);
        scoreCalculator.UnregisterPlayer(player);
        skillSelectionController.UnregisterPlayer(player);

        Debug.Log(
            $"[RoundManager] Unregistered: {player.gameObject.name}, " +
            $"Count={RegisteredPlayerCount}"
        );

        if (!IsNetworkReady)
        {
            return;
        }

        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (IsWaitingForReady)
        {
            TryStartFirstRound();
        }
        else if (IsSkillSelecting)
        {
            skillSelectionController.TryFinishSkillSelection();
        }
    }

    public bool IsPlayerReady(PlayerHealth player)
    {
        EnsureControllers();
        return readyController.IsPlayerReady(player);
    }

    public void SetPlayerReady(PlayerHealth player)
    {
        EnsureControllers();

        if (player == null)
        {
            return;
        }

        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (!IsWaitingForReady)
        {
            return;
        }

        if (!playerRegistry.Contains(player))
        {
            Debug.LogWarning($"[RoundManager] Ready ignored. Player is not registered: {player.gameObject.name}");
            return;
        }

        bool changed = readyController.SetPlayerReady(player);

        if (!changed)
        {
            return;
        }

        LogReadyStates();
        TryStartFirstRound();
    }

    public void SelectSkillBySlot(PlayerHealth player, int slotIndex)
    {
        EnsureControllers();
        skillSelectionController.SelectSkillBySlot(player, slotIndex);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSelectSkillBySlot(PlayerRef playerRef, int slotIndex)
    {
        EnsureControllers();
        skillSelectionController.HandleRequestSelectSkillBySlot(playerRef, slotIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifySkillSelectionProgress(PlayerRef playerRef, int selectedCount)
    {
        NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();

        if (launcher != null)
        {
            launcher.OnServerSkillSelectionProgress(playerRef, selectedCount);
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
        EnsureControllers();

        skillSelectionController.ApplySkillOptionsFromNetwork(
            slot0,
            slot1,
            slot2,
            slot3,
            secondSlot0,
            secondSlot1,
            secondSlot2,
            secondSlot3
        );
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowRoundEndUI(
        int roundNumber,
        int winnerTeamIndex,
        int teamMask,
        int redWins,
        int blueWins,
        int greenWins,
        int yellowWins
    )
    {
        EnsureControllers();

        uiController.ShowRoundEndUI(
            roundNumber,
            winnerTeamIndex,
            teamMask,
            redWins,
            blueWins,
            greenWins,
            yellowWins
        );
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcHideRoundEndUI()
    {
        EnsureControllers();
        uiController.HideRoundEndUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowFinalResultUI(int winnerTeamMask)
    {
        EnsureControllers();
        uiController.ShowFinalResultUI(winnerTeamMask);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcHideFinalResultUI()
    {
        EnsureControllers();
        uiController.HideFinalResultUI();
    }

    private void TryStartFirstRound()
    {
        EnsureControllers();

        if (!IsWaitingForReady)
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

        if (!readyController.AreAllPlayersReady(validPlayers, minPlayersToStart))
        {
            Debug.Log("[RoundManager] Waiting for all players to ready.");
            return;
        }

        currentRound = 1;
        skillSelectionController.BeginSkillSelectionForRound(currentRound);
    }

    private List<PlayerHealth> GetValidPlayers()
    {
        EnsureControllers();
        return playerRegistry.GetValidPlayers();
    }

    private void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        EnsureControllers();
        flowController.HandlePlayerDied(deadPlayer);
    }

    private void ShowRoundEndUIRpc(
        int roundNumber,
        int winnerTeamIndex,
        int teamMask,
        int redWins,
        int blueWins,
        int greenWins,
        int yellowWins
    )
    {
        RpcShowRoundEndUI(
            roundNumber,
            winnerTeamIndex,
            teamMask,
            redWins,
            blueWins,
            greenWins,
            yellowWins
        );
    }

    private void HideRoundEndUIRpc()
    {
        RpcHideRoundEndUI();
    }

    private void ShowFinalResultUIRpc(int winnerTeamMask)
    {
        RpcShowFinalResultUI(winnerTeamMask);
    }

    private void HideFinalResultUIRpc()
    {
        RpcHideFinalResultUI();
    }

    private void ShowWaitingRoomUI()
    {
        EnsureControllers();
        uiController.ShowWaitingRoomUI();
    }

    private void HideWaitingRoomUI()
    {
        EnsureControllers();
        uiController.HideWaitingRoomUI();
    }

    private void ApplyUIForPhase(GamePhase targetPhase, bool playBattleStartUI)
    {
        EnsureControllers();
        uiController.ApplyUIForPhase(targetPhase, playBattleStartUI);
    }

    private void LogReadyStates()
    {
        EnsureControllers();

        debugLogger.LogReadyStates(
            GetValidPlayers(),
            readyController,
            minPlayersToStart
        );
    }

    private void LogSkillSlots()
    {
        EnsureControllers();
        debugLogger.LogSkillSlots(availableRoundSkills);
    }

    private void LogSpawnPointSettings()
    {
        EnsureControllers();
        debugLogger.LogSpawnPointSettings(spawnController.GetAvailableSpawnPoints());
    }
}