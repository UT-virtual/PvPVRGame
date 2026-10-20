using Fusion;
using UnityEngine;

/// <summary>
/// 2つのスキル枠の選択、発動状態、効果時間とクールダウンを同期する。
/// StateAuthorityで状態を進め、発動処理はPlayerSkillActivator、継続効果の反映はPlayerSkillEffectApplierへ委譲する。
/// </summary>
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerWeapon))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerSkillController : NetworkBehaviour
{
    [Header("Common")]
    [SerializeField] private float cooldownAfterSkillEnd = 20.0f;

    [Header("Double Jump")]
    [SerializeField] private float doubleJumpDuration = 10.0f;
    [SerializeField] private int doubleJumpExtraAirJumpCount = 1;

    [Header("Rapid Fire")]
    [SerializeField] private float rapidFireDuration = 10.0f;
    [SerializeField] private float rapidFireIntervalMultiplier = 0.4f;

    [Header("Bullet Speed Up")]
    [SerializeField] private float bulletSpeedUpDuration = 10.0f;
    [SerializeField] private float bulletSpeedMultiplier = 1.8f;

    [Header("Damage Reduction")]
    [SerializeField] private float damageReductionDuration = 10.0f;
    [SerializeField] private float damageTakenMultiplier = 0.5f;

    [Header("XRay Vision")]
    [SerializeField] private float xRayVisionDuration = 10.0f;

    [Header("Move Speed Up")]
    [SerializeField] private float moveSpeedUpDuration = 10.0f;
    [SerializeField] private float moveSpeedMultiplier = 2.0f;

    [Header("Slow Fall")]
    [SerializeField] private float slowFallDuration = 10.0f;
    [SerializeField] private float slowFallGravityMultiplier = 0.35f;

    [Header("Shrink")]
    [SerializeField] private float shrinkDuration = 10.0f;
    [SerializeField] private float shrinkSizeMultiplier = 0.5f;
    [SerializeField] private float shrinkDamageDealtMultiplier = 0.5f;

    [Header("Delayed Damage Invincible")]
    [SerializeField] private float delayedDamageInvincibleDuration = 5.0f;

    [Header("Instant Reload")]
    [SerializeField] private float instantReloadDuration = 20.0f;

    [Header("Gravity Burst Reload")]
    [SerializeField] private float gravityBurstReloadDuration = 0.1f;
    [SerializeField] private int gravityBurstReloadAmmoCount = 20;

    [Header("Heavy Bullet Reload")]
    [SerializeField] private float heavyBulletReloadDuration = 0.1f;
    [SerializeField] private int heavyBulletReloadAmmoCount = 5;

    [Header("Next Shot Damage Boost")]
    [SerializeField] private float nextShotDamageBoostDuration = 0.1f;
    [SerializeField] private float nextShotDamageBoostMultiplier = 1.5f;

    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerHealth playerHealth;

    private PlayerSkillSettings skillSettings;
    private PlayerSkillEffectApplier effectApplier;
    private PlayerSkillActivator skillActivator;

    [Networked] public PlayerSkillType CurrentRoundSkill { get; private set; }
    [Networked] public PlayerSkillType CurrentRoundSecondSkill { get; private set; }

    [Networked] public PlayerSkillType LastRoundSkill { get; private set; }
    [Networked] public PlayerSkillType LastRoundSecondSkill { get; private set; }

    // 各枠の状態と期限を同期し、HUDと各端末の効果表示は同じ期限を参照する。
    [Networked] public PlayerSkillType ActiveSkill { get; private set; }
    [Networked] private TickTimer SkillActiveTimer { get; set; }
    [Networked] private TickTimer CooldownTimer { get; set; }

    [Networked] public PlayerSkillType ActiveSecondSkill { get; private set; }
    [Networked] private TickTimer SecondSkillActiveTimer { get; set; }
    [Networked] private TickTimer SecondCooldownTimer { get; set; }

    [Networked] public int CurrentSelectedSkillIndex { get; private set; }

    public PlayerSkillType CurrentSelectedRoundSkill =>
        CurrentSelectedSkillIndex <= 0
            ? CurrentRoundSkill
            : CurrentRoundSecondSkill;

    public bool CanUseCurrentSelectedSkill
    {
        get
        {
            int slotIndex = GetNormalizedCurrentSelectedSkillIndex();

            return
                GetRoundSkillBySlot(slotIndex) != PlayerSkillType.None &&
                !IsSkillSlotActive(slotIndex) &&
                !IsSkillSlotCoolingDown(slotIndex);
        }
    }

    public float CurrentSkillCooldownRemaining
    {
        get
        {
            int slotIndex = GetNormalizedCurrentSelectedSkillIndex();
            return GetSkillCooldownRemaining(slotIndex);
        }
    }

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerWeapon = GetComponent<PlayerWeapon>();
        playerHealth = GetComponent<PlayerHealth>();

        InitializeSkillHelpers();
    }

    public override void Render()
    {
        if (Object == null)
        {
            return;
        }

        // Renderでも同期済みの状態から補正を適用する。期限切れの確定とクールダウン開始はTick側で行う。
        ApplySkillEffects();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        UpdateSkillState();
    }

    public void PrepareForRound(PlayerSkillType requestedSkill)
    {
        PrepareForRound(requestedSkill, PlayerSkillType.None);
    }

    /// <summary>
    /// StateAuthorityで発動状態とクールダウンを消去し、ラウンドの2枠と履歴を設定する。
    /// 2枠目が1枠目と同じ場合はNoneに置き換える。
    /// </summary>
    public void PrepareForRound(PlayerSkillType requestedFirstSkill, PlayerSkillType requestedSecondSkill)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        ResetActiveAndCooldownState();

        PlayerSkillType selectedFirstSkill = requestedFirstSkill;
        PlayerSkillType selectedSecondSkill = requestedSecondSkill;

        if (selectedSecondSkill != PlayerSkillType.None && selectedSecondSkill == selectedFirstSkill)
        {
            Debug.Log(
                $"[Skill] {gameObject.name}: Same skill selected twice. Set second skill to None. Skill={selectedSecondSkill}"
            );

            selectedSecondSkill = PlayerSkillType.None;
        }

        CurrentRoundSkill = selectedFirstSkill;
        CurrentRoundSecondSkill = selectedSecondSkill;
        CurrentSelectedSkillIndex = GetInitialSelectedSkillIndex();

        LastRoundSkill = selectedFirstSkill;
        LastRoundSecondSkill = selectedSecondSkill;

        ApplySkillEffects();

        Debug.Log(
            $"[Skill] {gameObject.name}: " +
            $"RoundSkill1={CurrentRoundSkill}, RoundSkill2={CurrentRoundSecondSkill}, " +
            $"SelectedIndex={CurrentSelectedSkillIndex}, " +
            $"LastSkill1={LastRoundSkill}, LastSkill2={LastRoundSecondSkill}"
        );
    }

    /// <summary>
    /// StateAuthorityで今ラウンドの選択・発動・クールダウンを消去して継続補正を標準へ戻す。
    /// 前回の選択履歴と、武器側に残る特殊弾・次弾強化は保持する。
    /// </summary>
    public void ClearSkillState()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        CurrentRoundSkill = PlayerSkillType.None;
        CurrentRoundSecondSkill = PlayerSkillType.None;
        CurrentSelectedSkillIndex = 0;

        ResetActiveAndCooldownState();

        ApplySkillEffects();
    }

    /// <summary>
    /// 試合を待機へ戻す際、StateAuthorityで前回履歴を含むスキル状態を消去する。
    /// </summary>
    public void ResetSkillHistory()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        LastRoundSkill = PlayerSkillType.None;
        LastRoundSecondSkill = PlayerSkillType.None;

        ClearSkillState();
    }

    /// <summary>
    /// StateAuthorityで操作対象のスキル枠を切り替える。有効な枠が1つだけならその枠を選ぶ。
    /// </summary>
    public void SwitchCurrentSkill()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        bool hasFirstSkill = CurrentRoundSkill != PlayerSkillType.None;
        bool hasSecondSkill = CurrentRoundSecondSkill != PlayerSkillType.None;

        if (!hasFirstSkill && !hasSecondSkill)
        {
            Debug.Log($"[Skill] {gameObject.name}: Cannot switch skill because no skills are selected.");
            return;
        }

        if (hasFirstSkill && !hasSecondSkill)
        {
            CurrentSelectedSkillIndex = 0;
        }
        else if (!hasFirstSkill && hasSecondSkill)
        {
            CurrentSelectedSkillIndex = 1;
        }
        else
        {
            CurrentSelectedSkillIndex = CurrentSelectedSkillIndex <= 0 ? 1 : 0;
        }

        Debug.Log(
            $"[Skill] {gameObject.name}: Switched current skill. " +
            $"SelectedIndex={CurrentSelectedSkillIndex}, " +
            $"SelectedSkill={CurrentSelectedRoundSkill}"
        );
    }

    /// <summary>
    /// StateAuthorityで現在選択中の枠を検証し、効果中でもクールダウン中でもなければ発動する。
    /// 戦闘フェーズの許可は呼び出し元のPlayerControllerが確認する。
    /// </summary>
    public void TryActivateSkill()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        int slotIndex = GetNormalizedCurrentSelectedSkillIndex();
        PlayerSkillType selectedSkill = GetRoundSkillBySlot(slotIndex);

        if (selectedSkill == PlayerSkillType.None)
        {
            Debug.Log($"[Skill] {gameObject.name}: No current skill selected.");
            return;
        }

        if (IsSkillSlotActive(slotIndex))
        {
            Debug.Log(
                $"[Skill] {gameObject.name}: Current skill slot is already active. " +
                $"Slot={slotIndex + 1}, Skill={selectedSkill}"
            );
            return;
        }

        if (IsSkillSlotCoolingDown(slotIndex))
        {
            Debug.Log(
                $"[Skill] {gameObject.name}: Current skill slot is cooling down. " +
                $"Slot={slotIndex + 1}, Skill={selectedSkill}, " +
                $"Remaining={GetSkillCooldownRemaining(slotIndex):0.0}"
            );
            return;
        }

        skillActivator.Activate(slotIndex, selectedSkill);
    }

    /// <summary>
    /// StateAuthorityで指定枠の未発動・クールダウン完了を確認して発動する。
    /// slotIndexは0以下を1枠目、それ以外を2枠目として扱い、現在の選択インデックスは変更しない。
    /// </summary>
    public void TryActivateSkillBySlot(int slotIndex)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }
        slotIndex = slotIndex <= 0 ? 0 : 1;
        PlayerSkillType selectedSkill = GetRoundSkillBySlot(slotIndex);
        if (selectedSkill == PlayerSkillType.None)
        {
            return;
        }
        if (IsSkillSlotActive(slotIndex) || IsSkillSlotCoolingDown(slotIndex))
        {
            return;
        }
        skillActivator.Activate(slotIndex, selectedSkill);
    }

    public bool IsXRayVisionActive()
    {
        return IsSkillCurrentlyActive(PlayerSkillType.XRayVision);
    }

    private void InitializeSkillHelpers()
    {
        skillSettings = BuildSkillSettings();

        effectApplier = new PlayerSkillEffectApplier(
            playerMove,
            playerWeapon,
            playerHealth,
            skillSettings,
            IsSkillCurrentlyActive
        );

        skillActivator = new PlayerSkillActivator(
            playerWeapon,
            skillSettings,
            () => gameObject != null ? gameObject.name : "Unknown",
            BeginActiveSkill,
            ApplySkillEffects
        );
    }

    private PlayerSkillSettings BuildSkillSettings()
    {
        return new PlayerSkillSettings(
            cooldownAfterSkillEnd,
            doubleJumpDuration,
            doubleJumpExtraAirJumpCount,
            rapidFireDuration,
            rapidFireIntervalMultiplier,
            bulletSpeedUpDuration,
            bulletSpeedMultiplier,
            damageReductionDuration,
            damageTakenMultiplier,
            xRayVisionDuration,
            moveSpeedUpDuration,
            moveSpeedMultiplier,
            slowFallDuration,
            slowFallGravityMultiplier,
            shrinkDuration,
            shrinkSizeMultiplier,
            shrinkDamageDealtMultiplier,
            delayedDamageInvincibleDuration,
            instantReloadDuration,
            gravityBurstReloadDuration,
            gravityBurstReloadAmmoCount,
            heavyBulletReloadDuration,
            heavyBulletReloadAmmoCount,
            nextShotDamageBoostDuration,
            nextShotDamageBoostMultiplier
        );
    }

    /// <summary>
    /// 発動可否を検証した枠に発動状態とTickTimerを記録する。選択中の枠には依存しない。
    /// </summary>
    private void BeginActiveSkill(int slotIndex, PlayerSkillType skill, float duration)
    {
        TickTimer activeTimer = TickTimer.CreateFromSeconds(Runner, duration);

        if (slotIndex <= 0)
        {
            ActiveSkill = skill;
            SkillActiveTimer = activeTimer;
        }
        else
        {
            ActiveSecondSkill = skill;
            SecondSkillActiveTimer = activeTimer;
        }

        Debug.Log(
            $"[Skill] {gameObject.name}: BeginActiveSkill. " +
            $"Slot={slotIndex + 1}, Skill={skill}, Duration={duration}"
        );
    }

    /// <summary>
    /// StateAuthorityで各枠の効果終了を確定し、その時点からクールダウンを開始する。
    /// 期限はTickTimerで同期し、補正は期限の判定結果から再適用する。
    /// </summary>
    private void UpdateSkillState()
    {
        if (Runner == null)
        {
            return;
        }

        if (ActiveSkill != PlayerSkillType.None &&
            SkillActiveTimer.IsRunning &&
            SkillActiveTimer.Expired(Runner))
        {
            Debug.Log($"[Skill] {gameObject.name}: Slot 1 {ActiveSkill} ended. Cooldown started.");

            ActiveSkill = PlayerSkillType.None;
            SkillActiveTimer = TickTimer.None;
            CooldownTimer = TickTimer.CreateFromSeconds(Runner, skillSettings.CooldownAfterSkillEnd);
        }

        if (ActiveSecondSkill != PlayerSkillType.None &&
            SecondSkillActiveTimer.IsRunning &&
            SecondSkillActiveTimer.Expired(Runner))
        {
            Debug.Log($"[Skill] {gameObject.name}: Slot 2 {ActiveSecondSkill} ended. Cooldown started.");

            ActiveSecondSkill = PlayerSkillType.None;
            SecondSkillActiveTimer = TickTimer.None;
            SecondCooldownTimer = TickTimer.CreateFromSeconds(Runner, skillSettings.CooldownAfterSkillEnd);
        }

        if (CooldownTimer.IsRunning && CooldownTimer.Expired(Runner))
        {
            CooldownTimer = TickTimer.None;
            Debug.Log($"[Skill] {gameObject.name}: Slot 1 cooldown ended.");
        }

        if (SecondCooldownTimer.IsRunning && SecondCooldownTimer.Expired(Runner))
        {
            SecondCooldownTimer = TickTimer.None;
            Debug.Log($"[Skill] {gameObject.name}: Slot 2 cooldown ended.");
        }

        ApplySkillEffects();
    }

    private void ApplySkillEffects()
    {
        effectApplier.ApplySkillEffects();
    }

    private void ResetActiveAndCooldownState()
    {
        ActiveSkill = PlayerSkillType.None;
        ActiveSecondSkill = PlayerSkillType.None;

        SkillActiveTimer = TickTimer.None;
        SecondSkillActiveTimer = TickTimer.None;

        CooldownTimer = TickTimer.None;
        SecondCooldownTimer = TickTimer.None;
    }

    private int GetInitialSelectedSkillIndex()
    {
        if (CurrentRoundSkill != PlayerSkillType.None)
        {
            return 0;
        }

        if (CurrentRoundSecondSkill != PlayerSkillType.None)
        {
            return 1;
        }

        return 0;
    }

    private bool IsSkillCurrentlyActive(PlayerSkillType skill)
    {
        if (Runner == null || skill == PlayerSkillType.None)
        {
            return false;
        }

        bool firstSlotActive =
            ActiveSkill == skill &&
            SkillActiveTimer.IsRunning &&
            !SkillActiveTimer.Expired(Runner);

        bool secondSlotActive =
            ActiveSecondSkill == skill &&
            SecondSkillActiveTimer.IsRunning &&
            !SecondSkillActiveTimer.Expired(Runner);

        return firstSlotActive || secondSlotActive;
    }

    private bool IsSkillSlotActive(int slotIndex)
    {
        if (Runner == null)
        {
            return false;
        }

        PlayerSkillType activeSkill = slotIndex <= 0
            ? ActiveSkill
            : ActiveSecondSkill;

        TickTimer activeTimer = slotIndex <= 0
            ? SkillActiveTimer
            : SecondSkillActiveTimer;

        return
            activeSkill != PlayerSkillType.None &&
            activeTimer.IsRunning &&
            !activeTimer.Expired(Runner);
    }

    private bool IsSkillSlotCoolingDown(int slotIndex)
    {
        if (Runner == null)
        {
            return false;
        }

        TickTimer cooldownTimer = slotIndex <= 0
            ? CooldownTimer
            : SecondCooldownTimer;

        return cooldownTimer.IsRunning && !cooldownTimer.Expired(Runner);
    }

    private float GetSkillCooldownRemaining(int slotIndex)
    {
        if (Runner == null)
        {
            return 0.0f;
        }

        TickTimer cooldownTimer = slotIndex <= 0
            ? CooldownTimer
            : SecondCooldownTimer;

        if (!cooldownTimer.IsRunning)
        {
            return 0.0f;
        }

        float remaining = cooldownTimer.RemainingTime(Runner) ?? 0.0f;
        return Mathf.Max(remaining, 0.0f);
    }

    private int GetNormalizedCurrentSelectedSkillIndex()
    {
        return CurrentSelectedSkillIndex <= 0 ? 0 : 1;
    }

    private PlayerSkillType GetRoundSkillBySlot(int slotIndex)
    {
        return slotIndex <= 0
            ? CurrentRoundSkill
            : CurrentRoundSecondSkill;
    }
}
