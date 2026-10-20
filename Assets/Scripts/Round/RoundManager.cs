using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 同期する試合フェーズと、準備・スキル選択・戦闘・結果表示をつなぐ窓口を持つ。
/// StateAuthority側の参加者管理と進行を各Controllerへ委譲し、各端末のUIは同期フェーズと結果通知RPCで更新する。
/// </summary>
public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    public enum GamePhase
    {
        WaitingForReady,
        SkillSelecting,
        RoundStarting,
        RoundPlaying,
        RoundEnding,
        MatchFinished
    }

    private const int SkillOptionSlotCountValue = 4;

    [Header("Ready")]
    [SerializeField] private int minPlayersToStart = 2;

    [Header("Skill Selection")]
    [SerializeField] private float skillSelectionDuration = 10.0f;
    [SerializeField] private float startAfterAllSkillsSelectedDelay = 2.0f;

    [SerializeField]
    private List<PlayerSkillType> selectableRoundSkills = new()
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

    [Header("Debug Spectator Dummy")]
    [SerializeField] private bool countDebugSpectatorDummiesAsAlive = false;

    [Networked] public int CurrentRound { get; private set; }
    [Networked] public int SkillSelectionVersion { get; private set; }
    [Networked] private TickTimer RoundStartDeadline { get; set; }

    public float RoundStartRemainingSeconds => IsRoundStarting && Runner != null
        ? RoundStartDeadline.RemainingTime(Runner) ?? 0f
        : 0f;

    [Networked]
    private GamePhase phase { get; set; }

    private bool uiPhaseInitialized;
    private GamePhase lastAppliedUIPhase;

    private bool isSpawned;
    private int requiredPlayersForCurrentMatch = 1;

    private RoundPlayerRegistry playerRegistry;
    private RoundReadyController readyController;
    private RoundScoreCalculator scoreCalculator;
    private RoundSkillSelectionController skillSelectionController;
    private RoundStartController roundStartController;
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

    // Spawn前とDespawn後は同期プロパティを読まず、操作・UIの許可を閉じる。
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

    public bool IsRoundStarting =>
        CanReadNetworkedPhase && phase == GamePhase.RoundStarting;

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
            : SkillOptionSlotCountValue;

    public int RequiredSkillSelectionCount => 2;

    public float SkillSelectionDuration => skillSelectionDuration;

    public PlayerSkillType GetSkillOption(int slotIndex)
    {
        return GetSkillOption(slotIndex, 0);
    }

    public PlayerSkillType GetSkillOption(int slotIndex, int selectionIndex)
    {
        return skillSelectionController.GetSkillOption(slotIndex, selectionIndex);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitializeControllers();

        Debug.Log("[RoundManager] Awake");
    }

    public override void Spawned()
    {
        isSpawned = true;

        if (Object.HasStateAuthority)
        {
            CurrentRound = 1;
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
        flowController?.StopProgression();
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

        // 同期フェーズへの進入時だけ開始演出を再生し、Renderごとの再起動を避ける。
        bool enteredRoundStarting =
            lastAppliedUIPhase != GamePhase.RoundStarting &&
            phase == GamePhase.RoundStarting;

        ApplyUIForPhase(phase, enteredRoundStarting);

        lastAppliedUIPhase = phase;
    }

    private void InitializeControllers()
    {
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

        InitializeRoundStartController();
        InitializeSkillSelectionController();
        InitializeFlowController();
    }

    private void InitializeRoundStartController()
    {
        roundStartController = new RoundStartController(
            this,
            () => phase,
            SetPhase,
            () => CurrentRound,
            GetValidPlayers,
            () => startAfterAllSkillsSelectedDelay,
            player => skillSelectionController.GetFirstSelectedSkill(player),
            player => skillSelectionController.GetSecondSelectedSkill(player),
            healthItemController,
            () => RoundStartDeadline.ExpiredOrNotRunning(Runner)
        );
    }

    private void SetPhase(GamePhase value)
    {
        if (value == GamePhase.SkillSelecting && phase != value)
        {
            SkillSelectionVersion++;
        }

        RoundStartDeadline = value == GamePhase.RoundStarting
            ? TickTimer.CreateFromSeconds(Runner, Mathf.Max(0f, startAfterAllSkillsSelectedDelay))
            : TickTimer.None;
        phase = value;
    }

    private void InitializeSkillSelectionController()
    {
        skillSelectionController = new RoundSkillSelectionController(
            this,
            () => phase,
            SetPhase,
            () => CurrentRound,
            value => CurrentRound = value,
            GetValidPlayers,
            player => playerRegistry.Contains(player),
            selectableRoundSkills,
            SkillOptionSlotCountValue,
            RequiredSkillSelectionCount,
            () => skillSelectionDuration,
            new RoundSkillOptionBuilder(),
            spawnController,
            healthItemController,
            projectileCleaner,
            debugLogger,
            HideWaitingRoomUI,
            (player, count, step, first) => RPC_NotifySkillSelectionProgress(player, count, step, first, SkillSelectionVersion),
            RPC_SetSkillOptions,
            roundStartController.StartRoundAfterSkillSelection,
            roundStartController.StopStartRoundCoroutine
        );
    }

    private void InitializeFlowController()
    {
        flowController = new RoundFlowController(
            this,
            () => phase,
            SetPhase,
            () => CurrentRound,
            value => CurrentRound = value,
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
            RPC_ShowRoundEndUI,
            RPC_HideRoundEndUI,
            RPC_ShowFinalResultUI,
            RPC_HideFinalResultUI,
            ShowWaitingRoomUI,
            LogReadyStates
        );
    }

    /// <summary>
    /// StateAuthorityでラウンド参加者を登録し、死亡購読・準備・得点・選択状態を初期化する。重複登録は無視する。
    /// </summary>
    public void RegisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        if (Object == null || !Object.HasStateAuthority)
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

    /// <summary>
    /// 参加者の死亡購読と各管理状態を解除する。
    /// StateAuthorityでは人数不足なら試合を中断し、それ以外は現在のフェーズの完了条件を再評価する。
    /// </summary>
    public void UnregisterPlayer(PlayerHealth player)
    {
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

        if (!IsNetworkReady || Runner == null || !Runner.IsRunning)
        {
            return;
        }

        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (!IsWaitingForReady && RegisteredPlayerCount < requiredPlayersForCurrentMatch)
        {
            flowController.AbortMatchToWaiting();
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
        else if (IsRoundPlaying)
        {
            flowController.CheckRoundEnd();
        }
    }

    public bool IsPlayerReady(PlayerHealth player)
    {
        return readyController.IsPlayerReady(player);
    }

    /// <summary>
    /// StateAuthorityで待機中の登録済み参加者を準備完了にし、人数と全員の準備条件を再判定する。
    /// </summary>
    public void SetPlayerReady(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        if (Object == null || !Object.HasStateAuthority)
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
        skillSelectionController.SelectSkillBySlot(player, slotIndex);
    }

    /// <summary>
    /// 選択スロットの要求をRoundManagerのStateAuthorityへ届ける。
    /// 送信者・選択フェーズの世代・選択段階を照合し、再送や前の試合の遅延要求を重複受理しない。
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_RequestSelectSkillBySlot(PlayerRef playerRef, int slotIndex, int selectionVersion, int selectionStep, RpcInfo info = default)
    {
        if (info.Source == PlayerRef.None || info.Source != playerRef)
        {
            Debug.LogWarning("[RoundManager] Skill selection sender does not match the requested player.");
            return;
        }

        if (selectionVersion != SkillSelectionVersion)
        {
            return;
        }

        skillSelectionController.HandleRequestSelectSkillBySlot(playerRef, slotIndex, selectionStep);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifySkillSelectionProgress(PlayerRef playerRef, int selectedCount, int requestStep, PlayerSkillType firstSkill, int selectionVersion)
    {
        NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();

        if (launcher != null)
        {
            launcher.OnServerSkillSelectionProgress(playerRef, selectedCount, requestStep, firstSkill, selectionVersion);
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
    private void RPC_ShowRoundEndUI(
        int roundNumber,
        int winnerTeamIndex,
        int teamMask,
        int redWins,
        int blueWins,
        int greenWins,
        int yellowWins
    )
    {
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
    private void RPC_HideRoundEndUI()
    {
        uiController.HideRoundEndUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowFinalResultUI(int winnerTeamMask)
    {
        uiController.ShowFinalResultUI(winnerTeamMask);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideFinalResultUI()
    {
        uiController.HideFinalResultUI();
    }

    private void TryStartFirstRound()
    {
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

        // 1人開始の確認設定は維持するが、複数人で始めた試合は1人になったら中断する。
        requiredPlayersForCurrentMatch = Mathf.Max(minPlayersToStart, validPlayers.Count > 1 ? 2 : 1);
        CurrentRound = 1;
        skillSelectionController.BeginSkillSelectionForRound(CurrentRound);
    }

    private List<PlayerHealth> GetValidPlayers()
    {
        return playerRegistry.GetValidPlayers();
    }

    private void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        flowController.HandlePlayerDied(deadPlayer);
    }

    private void ShowWaitingRoomUI()
    {
        uiController.ShowWaitingRoomUI();
    }

    private void HideWaitingRoomUI()
    {
        uiController.HideWaitingRoomUI();
    }

    private void ApplyUIForPhase(GamePhase targetPhase, bool playBattleStartUI)
    {
        uiController.ApplyUIForPhase(targetPhase, playBattleStartUI);
    }

    private void LogReadyStates()
    {
        debugLogger.LogReadyStates(
            GetValidPlayers(),
            readyController,
            minPlayersToStart
        );
    }

    private void LogSkillSlots()
    {
        debugLogger.LogSkillSlots(selectableRoundSkills);
    }

    private void LogSpawnPointSettings()
    {
        debugLogger.LogSpawnPointSettings(spawnController.GetAvailableSpawnPoints());
    }
}