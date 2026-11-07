using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 同期する試合フェーズと、準備・スキル選択・戦闘・結果表示をつなぐ窓口を持つ。
/// StateAuthority側の参加者管理と進行を各Controllerへ委譲し、各端末のUIは同期フェーズと結果通知RPCで更新する。
/// </summary>
public class RoundManager : NetworkBehaviour, IAfterTick
{
    public static RoundManager Instance { get; private set; }

    public enum GamePhase
    {
        WaitingForReady,
        RoundEffect,
        SkillSelecting,
        RoundStarting,
        RoundPlaying,
        RoundEnding,
        MatchFinished
    }

    public enum RoundEffectType
    {
        None,
        BanJump,
        Health1,
    }

    private const int SkillOptionSlotCountValue = 4;
    public const float RoundDurationSeconds = 90f;
    public const float SuddenDeathRemainingSeconds = 30f;

    [Header("Ready")]
    [SerializeField] private int minPlayersToStart = 2;
    [SerializeField] private bool allowWeaponsInWaitingRoom = true;

    [Header("Round Effect")]
    [SerializeField] private float roundEffectDisplayDuration = 3.0f;

    [Networked]
    private RoundEffectType currentRoundEffect { get; set; }
    public bool IsJumpProhibited =>
        CanReadNetworkedPhase && (currentRoundEffect == RoundEffectType.BanJump ||
        (IsHappeningActive && HappeningState.Effect == RoundHappeningType.NoJump));

    public float CurrentMoveSpeedMultiplier =>
        (CanReadNetworkedPhase && currentRoundEffect == RoundEffectType.BanJump ? 2f : 1f) *
        (IsHappeningActive && HappeningState.Effect == RoundHappeningType.FasterMovement ? HappeningState.MoveSpeedMultiplier : 1f);

    [Header("Spectator Happening")]
    [SerializeField] private RoundHappeningSettings happeningSettings = new();

    [Networked] public RoundHappeningState HappeningState { get; private set; }
    [Networked] public int HappeningVoteCount { get; private set; }
    [Networked, Capacity(NetworkSessionController.MaxConnectionCount)]
    public NetworkDictionary<PlayerRef, int> HappeningVotes => default;

    public bool IsLocalDedicatedSpectator => IsNetworkReady && Runner != null && Runner.IsRunning && !Runner.IsShutdown &&
        ConnectedParticipants.TryGet(Runner.LocalPlayer, out ParticipantRole role) && role == ParticipantRole.Spectator;
    public bool IsHappeningVotingOpen => IsRoundPlaying && !IsSuddenDeath && HappeningState.IsVotingOpen(Runner);
    public bool IsHappeningActive => IsRoundPlaying && !IsSuddenDeath && HappeningState.IsEffectActive(Runner);

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

    [Header("Round Effect UI")]
    [SerializeField] private RoundEffectUI roundEffectUI;

    // 接続者の一覧と役割はキャラクターの有無と分け、観戦者も全端末の待機一覧へ載せる。
    [Networked, Capacity(NetworkSessionController.MaxConnectionCount)]
    public NetworkDictionary<PlayerRef, ParticipantRole> ConnectedParticipants => default;

    private NetworkLauncher participantLauncher;
    private int lastParticipantRevision = -1;

    [Networked] public int CurrentRound { get; private set; }
    [Networked] public int TotalRoundCount { get; private set; }
    [Networked] public int SkillSelectionVersion { get; private set; }
    [Networked] private TickTimer RoundStartDeadline { get; set; }
    [Networked] private TickTimer RoundDeadline { get; set; }
    [Networked] private TickTimer SuddenDeathDeadline { get; set; }
    [Networked] private NetworkBool suddenDeathActive { get; set; }
    [Networked] public int DamageRoundVersion { get; private set; }
    [Networked] public int DamageRoundNumber { get; private set; }
    [Networked] public int DamageRevision { get; private set; }
    [Networked] public int DamageEntryCount { get; private set; }
    [Networked] public NetworkBool HasFinalDamageRanking { get; private set; }
    [Networked, Capacity(NetworkSessionController.MaxCombatantCount)]
    public NetworkArray<RoundDamageEntry> DamageEntries => default;

    private readonly List<RoundDamageEntry> damageSnapshot = new(NetworkSessionController.MaxCombatantCount);
    private Coroutine roundEffectCoroutine;

    public float RoundRemainingSeconds => IsRoundPlaying && Runner != null
        ? Mathf.Max(0f, RoundDeadline.RemainingTime(Runner) ?? 0f) : 0f;

    // 同じTickの実行順に依存せず、60秒を境に命中と回復の扱いを切り替える。
    public bool IsSuddenDeath => IsRoundPlaying &&
        (suddenDeathActive || SuddenDeathDeadline.Expired(Runner));

    /// <summary>
    /// 命中した時点の全体倍率を返す。ハプニングとサドンデスは独立した期間で扱い、スキル補正は攻撃側に残す。
    /// </summary>
    public float AttackDamageMultiplier => (IsSuddenDeath ? 2f : 1f) *
        (IsHappeningActive && HappeningState.Effect == RoundHappeningType.DoubleDamage ? HappeningState.DamageMultiplier : 1f);

    public bool CanApplyCombatDamage(int roundVersion) => IsRoundPlaying &&
        roundVersion == DamageRoundVersion && !RoundDeadline.ExpiredOrNotRunning(Runner);

    /// <summary>
    /// HP減少と、その一撃による撃破を集計する。保留ダメージの前ラウンド分を持ち越さない。
    /// </summary>
    public void RecordEffectiveDamage(PlayerRef attacker, PlayerHealth victim, float damage, int roundVersion, bool lethal = false)
    {
        if (Object == null || !Object.HasStateAuthority || !CanApplyCombatDamage(roundVersion) ||
            victim == null || victim.Object == null)
        {
            return;
        }

        scoreCalculator.RecordDamage(attacker, victim.Object.InputAuthority, damage, lethal);
    }

    public float RoundStartRemainingSeconds => IsRoundStarting && Runner != null
        ? RoundStartDeadline.RemainingTime(Runner) ?? 0f
        : 0f;

    public event Action<GamePhase> OnPhaseChanged;
    public event Action<int, bool> OnRoundEnded;
    public event Action<int> OnAllRoundsEnded;

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
    private RoundHappeningController happeningController;
    private SpectatorHappeningInputController happeningInput;
    private readonly List<PlayerRef> departedHappeningVoters = new(NetworkSessionController.MaxConnectionCount);

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
        (phase == GamePhase.RoundPlaying ||
         (allowWeaponsInWaitingRoom && phase == GamePhase.WaitingForReady));

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

    public bool IsRoundEnding => CanReadNetworkedPhase && phase == GamePhase.RoundEnding;

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


    private void BeginRoundEffect()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        // 5分の1の確率でラウンド効果を発生させる
        bool effectOccurs = UnityEngine.Random.Range(0, 5) == 0;

        if (effectOccurs)
        {
            // 効果が発生するときだけ、効果を抽選する
            RoundEffectType[] effects =
            {
                RoundEffectType.BanJump,
                RoundEffectType.Health1
            };

            currentRoundEffect = effects[
                UnityEngine.Random.Range(0, effects.Length)
            ];
        }
        else
        {
            // 効果が発生しない
            currentRoundEffect = RoundEffectType.None;
        }

        SetPhase(GamePhase.RoundEffect);

        RPC_ShowRoundEffect(currentRoundEffect);

        roundEffectCoroutine = StartCoroutine(BeginSkillSelectionAfterEffect());
    }


    public void ApplyHealthOneEffect()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (currentRoundEffect != RoundEffectType.Health1)
        {
            return;
        }

        foreach (PlayerHealth player in Players)
        {
            if (player == null || player.IsDead)
            {
                continue;
            }

            player.SetHealthToOne();
        }
    }

    private IEnumerator BeginSkillSelectionAfterEffect()
    {
        yield return new WaitForSeconds(roundEffectDisplayDuration);

        if (Object == null || !Object.HasStateAuthority || phase != GamePhase.RoundEffect)
        {
            yield break;
        }

        roundEffectCoroutine = null;
        skillSelectionController.BeginSkillSelectionForRound(CurrentRound);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowRoundEffect(RoundEffectType effect)
    {
        Debug.Log($"[RoundManager] Round Effect: {effect}");
        if (roundEffectUI != null)
        {
            roundEffectUI.ShowEffect(effect, 3.0f);
        }
        // ここでRoundEffect UIを表示
    }

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
        happeningInput = GetComponent<SpectatorHappeningInputController>();
        if (happeningInput == null) happeningInput = gameObject.AddComponent<SpectatorHappeningInputController>();
        happeningInput.Initialize(this);
        RoundBattleStatusUI battleStatus = GetComponent<RoundBattleStatusUI>();
        if (battleStatus == null) battleStatus = gameObject.AddComponent<RoundBattleStatusUI>();
        WaitingRoomUI styleSource = waitingRoomUI != null ? waitingRoomUI : FindFirstObjectByType<WaitingRoomUI>(FindObjectsInactive.Include);
        battleStatus.Initialize(this, styleSource != null ? styleSource.DisplayFont : null,
            styleSource != null ? styleSource.ColorIconSprite : null);

        Debug.Log("[RoundManager] Awake");
    }

    public override void Spawned()
    {
        isSpawned = true;

        if (Object.HasStateAuthority)
        {
            lastParticipantRevision = -1;
            CurrentRound = 1;
            TotalRoundCount = Mathf.Max(1, maxRoundCount);
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
        happeningController?.Reset();
        if (roundEffectCoroutine != null) StopCoroutine(roundEffectCoroutine);
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

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (participantLauncher == null) participantLauncher = FindFirstObjectByType<NetworkLauncher>();

        if (IsSuddenDeath && !suddenDeathActive)
        {
            suddenDeathActive = true;
            // 再生成待ちも停止し、次ラウンドのSetupまで回復アイテムを復帰させない。
            healthItemController.ClearHealthItems();
        }
        if (IsRoundPlaying)
        {
            if (IsSuddenDeath) ResetHappening();
            else
            {
                RoundHappeningPhase previous = HappeningState.Phase;
                HappeningState = happeningController.Advance(HappeningState, Runner, participantLauncher != null ? participantLauncher.ParticipantRoles : null);
                if ((previous == RoundHappeningPhase.Waiting || previous == RoundHappeningPhase.Voting) &&
                    HappeningState.Phase >= RoundHappeningPhase.Preview)
                {
                    PublishHappeningVotes();
                }
            }
        }
        if (participantLauncher == null || lastParticipantRevision == participantLauncher.ParticipantRevision)
        {
            return;
        }

        ConnectedParticipants.Clear();
        foreach (var participant in participantLauncher.ParticipantRoles)
        {
            ConnectedParticipants.Add(participant.Key, participant.Value);
        }
        lastParticipantRevision = participantLauncher.ParticipantRevision;
        RemoveDepartedHappeningVoters();
    }

    private void RemoveDepartedHappeningVoters()
    {
        // 同期一覧は現在の接続者の確認用に限る。退出者の票数と投票済み判定はControllerへ残す。
        // 途中退出と新規接続を繰り返しても、固定容量のNetworkDictionaryを超えないようにする。
        departedHappeningVoters.Clear();
        foreach (var vote in HappeningVotes)
        {
            if (!participantLauncher.ParticipantRoles.ContainsKey(vote.Key)) departedHappeningVoters.Add(vote.Key);
        }
        foreach (PlayerRef voter in departedHappeningVoters) HappeningVotes.Remove(voter);
    }

    private void PublishHappeningVotes()
    {
        if (participantLauncher != null)
        {
            RemoveDepartedHappeningVoters();
            foreach (var participant in participantLauncher.ParticipantRoles)
            {
                if (!HappeningVotes.ContainsKey(participant.Key) && happeningController.TryGetVote(participant.Key, out int candidate))
                    HappeningVotes.Add(participant.Key, candidate);
            }
        }
        HappeningVoteCount = happeningController.VoteCount;
    }

    /// <summary>
    /// 全NetworkBehaviourのTick処理後に終了を判定する。
    /// 範囲攻撃で全員が死亡した場合も、途中の一人を勝者として先に確定しない。
    /// </summary>
    public void AfterTick()
    {
        if (!IsNetworkReady || !Object.HasStateAuthority || !IsRoundPlaying) return;

        flowController.ResolvePendingDeaths();
        if (!IsRoundPlaying) return;

        if (RoundDeadline.ExpiredOrNotRunning(Runner))
        {
            flowController.EndRoundOnTimeout();
        }
        else
        {
            PublishDamageSnapshot();
        }
    }

    private void PublishDamageSnapshot()
    {
        scoreCalculator.CopyRoundDamage(damageSnapshot);
        bool changed = DamageEntryCount != damageSnapshot.Count;
        DamageEntryCount = damageSnapshot.Count;
        for (int i = 0; i < damageSnapshot.Count; i++)
        {
            RoundDamageEntry previous = DamageEntries[i];
            RoundDamageEntry current = damageSnapshot[i];
            if (previous.Player == current.Player && previous.Kills == current.Kills && previous.Damage == current.Damage &&
                previous.MaxHealth == current.MaxHealth &&
                previous.Health == current.Health && previous.TeamIndex == current.TeamIndex &&
                previous.IsAlive == current.IsAlive) continue;
            DamageEntries.Set(i, current);
            changed = true;
        }

        if (changed) DamageRevision++;
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
        OnPhaseChanged?.Invoke(phase); // 追加
    }

    private void InitializeControllers()
    {
        happeningController = new RoundHappeningController(count => UnityEngine.Random.Range(0, count));
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
        if (value == phase) return;

        if (value == GamePhase.WaitingForReady && roundEffectCoroutine != null)
        {
            StopCoroutine(roundEffectCoroutine);
            roundEffectCoroutine = null;
        }

        if (phase == GamePhase.RoundPlaying)
        {
            ResetHappening();
            // 復活・スキル解除でHPが変わる前の値を結果として固定する。
            PublishDamageSnapshot();
            HasFinalDamageRanking = value == GamePhase.RoundEnding;
            RoundDeadline = TickTimer.None;
            SuddenDeathDeadline = TickTimer.None;
            suddenDeathActive = false;
        }

        if (value == GamePhase.RoundPlaying)
        {
            // 端末ごとのScene設定ではなく、進行側の総ラウンド数をHUDへ伝える。
            TotalRoundCount = Mathf.Max(1, maxRoundCount);
            DamageRoundVersion++;
            DamageRoundNumber = CurrentRound;
            HasFinalDamageRanking = false;
            suddenDeathActive = false;
            scoreCalculator.BeginRoundDamage(GetValidPlayers());
            RoundDeadline = TickTimer.CreateFromSeconds(Runner, RoundDurationSeconds);
            SuddenDeathDeadline = TickTimer.CreateFromSeconds(Runner, RoundDurationSeconds - SuddenDeathRemainingSeconds);
            HappeningState = happeningController.Begin(Runner, happeningSettings);
            HappeningVotes.Clear();
            HappeningVoteCount = 0;
            PublishDamageSnapshot();
        }
        else if (value == GamePhase.WaitingForReady)
        {
            HasFinalDamageRanking = false;
        }

        if (value == GamePhase.SkillSelecting && phase != value)
        {
            SkillSelectionVersion++;
        }

        RoundStartDeadline = value == GamePhase.RoundStarting
            ? TickTimer.CreateFromSeconds(Runner, Mathf.Max(0f, startAfterAllSkillsSelectedDelay))
            : TickTimer.None;
        phase = value;
    }

    private void ResetHappening()
    {
        if (HappeningState.Phase == RoundHappeningPhase.None) return;
        happeningController.Reset();
        HappeningVotes.Clear();
        HappeningVoteCount = 0;
        HappeningState = default;
    }

    /// <summary>
    /// 接続者本人の専用観戦者権限、ラウンド世代、投票期間をStateAuthorityで検証する。
    /// 同じ候補の再送は受理済みと応答するだけで、変更・追加の一票にはしない。
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_RequestHappeningVote(int roundVersion, int candidate, RpcInfo info = default)
    {
        if (info.Source == PlayerRef.None || roundVersion != DamageRoundVersion) return;
        if (participantLauncher == null) participantLauncher = FindFirstObjectByType<NetworkLauncher>();
        bool eligible = IsRoundPlaying && !IsSuddenDeath && participantLauncher != null &&
            participantLauncher.ParticipantRoles.TryGetValue(info.Source, out ParticipantRole role) && role == ParticipantRole.Spectator;
        bool accepted = false;
        if (eligible)
        {
            RemoveDepartedHappeningVoters();
            if (happeningController.TryGetVote(info.Source, out int existing)) accepted = existing == candidate;
            else if (happeningController.TryVote(info.Source, candidate, true, HappeningState, Runner))
            {
                accepted = true;
            }
            if (accepted && !HappeningVotes.ContainsKey(info.Source)) HappeningVotes.Add(info.Source, candidate);
            HappeningVoteCount = happeningController.VoteCount;
        }
        RPC_HappeningVoteResult(info.Source, roundVersion, candidate, accepted);
    }

    /// <summary>
    /// 本人の専用観戦者権限を確認し、締切で使う仮選択だけを更新する。確定や票数の加算は行わない。
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_UpdateHappeningFocus(int roundVersion, int candidate, int revision, RpcInfo info = default)
    {
        if (info.Source == PlayerRef.None || roundVersion != DamageRoundVersion || !IsHappeningVotingOpen) return;
        if (participantLauncher == null) participantLauncher = FindFirstObjectByType<NetworkLauncher>();
        bool eligible = participantLauncher != null && participantLauncher.ParticipantRoles.TryGetValue(info.Source, out ParticipantRole role) &&
            role == ParticipantRole.Spectator;
        happeningController.TrySetFocus(info.Source, candidate, revision, eligible, HappeningState, Runner);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HappeningVoteResult(PlayerRef voter, int roundVersion, int candidate, bool accepted)
    {
        if (Runner.LocalPlayer == voter) happeningInput?.ApplyResult(roundVersion, candidate, accepted);
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
            LogReadyStates,
            BeginRoundEffect
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

    /// <summary>
    /// 接続者本人の役割要求をStateAuthorityで検証する。観戦者はキャラクターもラウンド登録も持たない。
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_RequestParticipantRole(PlayerRef player, ParticipantRole role,
        int requestId, bool initialAssignment, RpcInfo info = default)
    {
        if (info.Source == PlayerRef.None || info.Source != player)
        {
            return;
        }

        NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();
        if (launcher == null)
        {
            return;
        }

        launcher.TryChangeParticipantRole(Runner, player, role, requestId, IsWaitingForReady, initialAssignment, out string message);
        RPC_ParticipantRoleResult(player, requestId, launcher.GetParticipantRole(player), message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ParticipantRoleResult(PlayerRef player, int requestId,
        ParticipantRole role, string message)
    {
        NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();
        if (launcher != null)
        {
            launcher.OnParticipantRoleResult(player, requestId, role, message);
        }
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

        OnRoundEnded?.Invoke(roundNumber, roundNumber >= maxRoundCount); // 追加
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
        OnAllRoundsEnded?.Invoke(winnerTeamMask); // 追加
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
        BeginRoundEffect();
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
