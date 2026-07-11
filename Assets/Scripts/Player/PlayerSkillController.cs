using Fusion;
using UnityEngine;

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

    [Header("Debug")]
    [SerializeField] private bool allowSameSkillConsecutiveForDebug = false;

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

    public PlayerSkillType GetRoundSkillForSlot(int slotIndex)
    {
        return GetRoundSkillBySlot(NormalizeSkillSlotIndex(slotIndex));
    }

    public bool CanUseSkillSlot(int slotIndex)
    {
        slotIndex = NormalizeSkillSlotIndex(slotIndex);

        return
            GetRoundSkillBySlot(slotIndex) != PlayerSkillType.None &&
            !IsSkillSlotActive(slotIndex) &&
            !IsSkillSlotCoolingDown(slotIndex);
    }

    public float GetSkillCooldownRemainingForSlot(int slotIndex)
    {
        return GetSkillCooldownRemaining(NormalizeSkillSlotIndex(slotIndex));
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

    public bool CanSelectSkill(PlayerSkillType skill)
    {
        // 現状の挙動維持: 連続使用制限は無効。
        // 連続使用禁止を戻す場合は allowSameSkillConsecutiveForDebug を使ってここを切り替える。
        return true;
    }

    public void PrepareForRound(PlayerSkillType requestedSkill)
    {
        PrepareForRound(requestedSkill, PlayerSkillType.None);
    }

    public void PrepareForRound(PlayerSkillType requestedFirstSkill, PlayerSkillType requestedSecondSkill)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        ResetActiveAndCooldownState();

        PlayerSkillType selectedFirstSkill = requestedFirstSkill;
        PlayerSkillType selectedSecondSkill = requestedSecondSkill;

        if (!CanSelectSkill(selectedFirstSkill))
        {
            Debug.Log(
                $"[Skill] {gameObject.name}: {selectedFirstSkill} cannot be used in consecutive rounds. Set first skill to None."
            );

            selectedFirstSkill = PlayerSkillType.None;
        }

        if (!CanSelectSkill(selectedSecondSkill))
        {
            Debug.Log(
                $"[Skill] {gameObject.name}: {selectedSecondSkill} cannot be used in consecutive rounds. Set second skill to None."
            );

            selectedSecondSkill = PlayerSkillType.None;
        }

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

        skillActivator.Activate(selectedSkill, slotIndex);
    }

    //wayo追記
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

        skillActivator.Activate(selectedSkill, slotIndex);
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

    private void BeginActiveSkill(PlayerSkillType skill, float duration, int slotIndex)
    {
        slotIndex = slotIndex <= 0 ? 0 : 1;

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

    private int NormalizeSkillSlotIndex(int slotIndex)
    {
        return slotIndex <= 0 ? 0 : 1;
    }

    private PlayerSkillType GetRoundSkillBySlot(int slotIndex)
    {
        return slotIndex <= 0
            ? CurrentRoundSkill
            : CurrentRoundSecondSkill;
    }
}
