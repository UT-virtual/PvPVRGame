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

    [Header("Debug")]
    [SerializeField] private bool allowSameSkillConsecutiveForDebug = false;

    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerHealth playerHealth;

    [Networked] public PlayerSkillType CurrentRoundSkill { get; private set; }
    [Networked] public PlayerSkillType LastRoundSkill { get; private set; }
    [Networked] public PlayerSkillType ActiveSkill { get; private set; }

    [Networked] private TickTimer SkillActiveTimer { get; set; }
    [Networked] private TickTimer CooldownTimer { get; set; }

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerWeapon = GetComponent<PlayerWeapon>();
        playerHealth = GetComponent<PlayerHealth>();
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
        if (skill == PlayerSkillType.None)
        {
            return true;
        }

        if (allowSameSkillConsecutiveForDebug)
        {
            return true;
        }

        return skill != LastRoundSkill;
    }

    public void PrepareForRound(PlayerSkillType requestedSkill)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        ActiveSkill = PlayerSkillType.None;
        SkillActiveTimer = TickTimer.None;
        CooldownTimer = TickTimer.None;

        PlayerSkillType selectedSkill = requestedSkill;

        if (!CanSelectSkill(selectedSkill))
        {
            Debug.Log(
                $"[Skill] {gameObject.name}: {selectedSkill} cannot be used in consecutive rounds. Set to None."
            );

            selectedSkill = PlayerSkillType.None;
        }

        CurrentRoundSkill = selectedSkill;

        if (selectedSkill != PlayerSkillType.None)
        {
            LastRoundSkill = selectedSkill;
        }

        ApplySkillEffects();

        Debug.Log(
            $"[Skill] {gameObject.name}: RoundSkill={CurrentRoundSkill}, LastRoundSkill={LastRoundSkill}"
        );
    }

    public void ClearSkillState()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        CurrentRoundSkill = PlayerSkillType.None;
        ActiveSkill = PlayerSkillType.None;
        SkillActiveTimer = TickTimer.None;
        CooldownTimer = TickTimer.None;

        ApplySkillEffects();
    }

    public void ResetSkillHistory()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        LastRoundSkill = PlayerSkillType.None;
        ClearSkillState();
    }

    public void TryActivateSkill()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (CurrentRoundSkill == PlayerSkillType.None)
        {
            Debug.Log($"[Skill] {gameObject.name}: No skill selected.");
            return;
        }

        if (IsSkillActive())
        {
            Debug.Log($"[Skill] {gameObject.name}: Skill is already active.");
            return;
        }

        if (IsCooldownActive())
        {
            Debug.Log($"[Skill] {gameObject.name}: Skill is cooling down.");
            return;
        }

        switch (CurrentRoundSkill)
        {
            case PlayerSkillType.DoubleJump:
                ActivateDoubleJump();
                break;

            case PlayerSkillType.RapidFire:
                ActivateRapidFire();
                break;

            case PlayerSkillType.BulletSpeedUp:
                ActivateBulletSpeedUp();
                break;

            case PlayerSkillType.DamageReduction:
                ActivateDamageReduction();
                break;

            case PlayerSkillType.XRayVision:
                ActivateXRayVision();
                break;

            case PlayerSkillType.MoveSpeedUp:
                ActivateMoveSpeedUp();
                break;

            default:
                Debug.LogWarning($"[Skill] Unsupported skill: {CurrentRoundSkill}");
                break;
        }
    }

    public bool IsXRayVisionActive()
    {
        if (Runner == null)
        {
            return false;
        }

        return
            ActiveSkill == PlayerSkillType.XRayVision &&
            SkillActiveTimer.IsRunning &&
            !SkillActiveTimer.Expired(Runner);
    }

    private void ActivateDoubleJump()
    {
        ActiveSkill = PlayerSkillType.DoubleJump;
        SkillActiveTimer = TickTimer.CreateFromSeconds(Runner, doubleJumpDuration);

        ApplySkillEffects();

        Debug.Log($"[Skill] {gameObject.name}: DoubleJump activated for {doubleJumpDuration} seconds.");
    }

    private void ActivateRapidFire()
    {
        ActiveSkill = PlayerSkillType.RapidFire;
        SkillActiveTimer = TickTimer.CreateFromSeconds(Runner, rapidFireDuration);

        ApplySkillEffects();

        Debug.Log(
            $"[Skill] {gameObject.name}: RapidFire activated for {rapidFireDuration} seconds. " +
            $"IntervalMultiplier={rapidFireIntervalMultiplier}"
        );
    }

    private void ActivateBulletSpeedUp()
    {
        ActiveSkill = PlayerSkillType.BulletSpeedUp;
        SkillActiveTimer = TickTimer.CreateFromSeconds(Runner, bulletSpeedUpDuration);

        ApplySkillEffects();

        Debug.Log(
            $"[Skill] {gameObject.name}: BulletSpeedUp activated for {bulletSpeedUpDuration} seconds. " +
            $"BulletSpeedMultiplier={bulletSpeedMultiplier}"
        );
    }

    private void ActivateDamageReduction()
    {
        ActiveSkill = PlayerSkillType.DamageReduction;
        SkillActiveTimer = TickTimer.CreateFromSeconds(Runner, damageReductionDuration);

        ApplySkillEffects();

        Debug.Log(
            $"[Skill] {gameObject.name}: DamageReduction activated for {damageReductionDuration} seconds. " +
            $"DamageTakenMultiplier={damageTakenMultiplier}"
        );
    }

    private void ActivateXRayVision()
    {
        ActiveSkill = PlayerSkillType.XRayVision;
        SkillActiveTimer = TickTimer.CreateFromSeconds(Runner, xRayVisionDuration);

        ApplySkillEffects();

        Debug.Log($"[Skill] {gameObject.name}: XRayVision activated for {xRayVisionDuration} seconds.");
    }

    private void UpdateSkillState()
    {
        if (ActiveSkill != PlayerSkillType.None && SkillActiveTimer.Expired(Runner))
        {
            Debug.Log($"[Skill] {gameObject.name}: {ActiveSkill} ended. Cooldown started.");

            ActiveSkill = PlayerSkillType.None;
            SkillActiveTimer = TickTimer.None;
            CooldownTimer = TickTimer.CreateFromSeconds(Runner, cooldownAfterSkillEnd);
        }

        if (CooldownTimer.Expired(Runner))
        {
            CooldownTimer = TickTimer.None;
            Debug.Log($"[Skill] {gameObject.name}: Cooldown ended.");
        }

        ApplySkillEffects();
    }

    private void ApplySkillEffects()
    {
        ApplyDoubleJumpEffect();
        ApplyRapidFireEffect();
        ApplyBulletSpeedUpEffect();
        ApplyDamageReductionEffect();
        ApplyMoveSpeedUpEffect();
    }

    private void ApplyDoubleJumpEffect()
    {
        if (playerMove == null)
        {
            return;
        }

        if (IsDoubleJumpActive())
        {
            playerMove.SetExtraAirJumpCount(doubleJumpExtraAirJumpCount);
        }
        else
        {
            playerMove.SetExtraAirJumpCount(0);
        }
    }

    private void ApplyRapidFireEffect()
    {
        if (playerWeapon == null)
        {
            return;
        }

        if (IsRapidFireActive())
        {
            playerWeapon.SetFireIntervalMultiplier(rapidFireIntervalMultiplier);
        }
        else
        {
            playerWeapon.SetFireIntervalMultiplier(1.0f);
        }
    }

    private void ApplyBulletSpeedUpEffect()
    {
        if (playerWeapon == null)
        {
            return;
        }

        if (IsBulletSpeedUpActive())
        {
            playerWeapon.SetProjectileSpeedMultiplier(bulletSpeedMultiplier);
        }
        else
        {
            playerWeapon.SetProjectileSpeedMultiplier(1.0f);
        }
    }

    private void ApplyDamageReductionEffect()
    {
        if (playerHealth == null)
        {
            return;
        }

        if (IsDamageReductionActive())
        {
            playerHealth.SetDamageTakenMultiplier(damageTakenMultiplier);
        }
        else
        {
            playerHealth.SetDamageTakenMultiplier(1.0f);
        }
    }

    private void ApplyMoveSpeedUpEffect()
    {
        if (playerMove == null)
        {
            return;
        }

        if (IsMoveSpeedUpActive())
        {
            playerMove.SetMoveSpeedMultiplier(moveSpeedMultiplier);
        }
        else
        {
            playerMove.SetMoveSpeedMultiplier(1.0f);
        }
    }

    private bool IsMoveSpeedUpActive()
    {
        return ActiveSkill == PlayerSkillType.MoveSpeedUp && IsSkillActive();
    }

    private void ActivateMoveSpeedUp()
    {
        ActiveSkill = PlayerSkillType.MoveSpeedUp;
        SkillActiveTimer = TickTimer.CreateFromSeconds(Runner, moveSpeedUpDuration);

        ApplySkillEffects();

        Debug.Log(
            $"[Skill] {gameObject.name}: MoveSpeedUp activated for {moveSpeedUpDuration} seconds. " +
            $"MoveSpeedMultiplier={moveSpeedMultiplier}"
        );
    }

    private bool IsDoubleJumpActive()
    {
        return ActiveSkill == PlayerSkillType.DoubleJump && IsSkillActive();
    }

    private bool IsRapidFireActive()
    {
        return ActiveSkill == PlayerSkillType.RapidFire && IsSkillActive();
    }

    private bool IsBulletSpeedUpActive()
    {
        return ActiveSkill == PlayerSkillType.BulletSpeedUp && IsSkillActive();
    }

    private bool IsDamageReductionActive()
    {
        return ActiveSkill == PlayerSkillType.DamageReduction && IsSkillActive();
    }

    private bool IsSkillActive()
    {
        return SkillActiveTimer.IsRunning && !SkillActiveTimer.Expired(Runner);
    }

    private bool IsCooldownActive()
    {
        return CooldownTimer.IsRunning && !CooldownTimer.Expired(Runner);
    }
}