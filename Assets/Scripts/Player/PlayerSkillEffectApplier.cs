using System;
using UnityEngine;

public sealed class PlayerSkillEffectApplier
{
    private readonly PlayerMove playerMove;
    private readonly PlayerWeapon playerWeapon;
    private readonly PlayerHealth playerHealth;
    private readonly PlayerSkillSettings settings;
    private readonly Func<PlayerSkillType, bool> isSkillActive;

    public PlayerSkillEffectApplier(
        PlayerMove playerMove,
        PlayerWeapon playerWeapon,
        PlayerHealth playerHealth,
        PlayerSkillSettings settings,
        Func<PlayerSkillType, bool> isSkillActive
    )
    {
        this.playerMove = playerMove;
        this.playerWeapon = playerWeapon;
        this.playerHealth = playerHealth;
        this.settings = settings;
        this.isSkillActive = isSkillActive;
    }

    public void ApplySkillEffects()
    {
        ApplyDoubleJumpEffect();
        ApplyRapidFireEffect();
        ApplyBulletSpeedUpEffect();
        ApplyDamageReductionEffect();
        ApplyMoveSpeedUpEffect();
        ApplySlowFallEffect();
        ApplyShrinkEffect();
        ApplyDelayedDamageInvincibleEffect();
        ApplyInstantReloadEffect();
    }

    private bool IsActive(PlayerSkillType skill)
    {
        return isSkillActive != null && isSkillActive(skill);
    }

    private void ApplyDoubleJumpEffect()
    {
        if (playerMove == null)
        {
            return;
        }

        playerMove.SetExtraAirJumpCount(
            IsActive(PlayerSkillType.DoubleJump)
                ? settings.DoubleJumpExtraAirJumpCount
                : 0
        );
    }

    private void ApplyRapidFireEffect()
    {
        if (playerWeapon == null)
        {
            return;
        }

        playerWeapon.SetFireIntervalMultiplier(
            IsActive(PlayerSkillType.RapidFire)
                ? settings.RapidFireIntervalMultiplier
                : 1.0f
        );
    }

    private void ApplyBulletSpeedUpEffect()
    {
        if (playerWeapon == null)
        {
            return;
        }

        playerWeapon.SetProjectileSpeedMultiplier(
            IsActive(PlayerSkillType.BulletSpeedUp)
                ? settings.BulletSpeedMultiplier
                : 1.0f
        );
    }

    private void ApplyDamageReductionEffect()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.SetDamageTakenMultiplier(
            IsActive(PlayerSkillType.DamageReduction)
                ? settings.DamageTakenMultiplier
                : 1.0f
        );
    }

    private void ApplyMoveSpeedUpEffect()
    {
        if (playerMove == null)
        {
            return;
        }

        playerMove.SetMoveSpeedMultiplier(
            IsActive(PlayerSkillType.MoveSpeedUp)
                ? settings.MoveSpeedMultiplier
                : 1.0f
        );
    }

    private void ApplySlowFallEffect()
    {
        if (playerMove == null)
        {
            return;
        }

        playerMove.SetFallGravityMultiplier(
            IsActive(PlayerSkillType.SlowFall)
                ? settings.SlowFallGravityMultiplier
                : 1.0f
        );
    }

    private void ApplyShrinkEffect()
    {
        bool isShrinkActive = IsActive(PlayerSkillType.Shrink);

        if (playerHealth != null)
        {
            playerHealth.SetBodySizeMultiplier(
                isShrinkActive
                    ? settings.ShrinkSizeMultiplier
                    : 1.0f
            );
        }

        if (playerWeapon != null)
        {
            playerWeapon.SetDamageDealtMultiplier(
                isShrinkActive
                    ? settings.ShrinkDamageDealtMultiplier
                    : 1.0f
            );
        }
    }

    private void ApplyDelayedDamageInvincibleEffect()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.SetDelayedDamageMode(
            IsActive(PlayerSkillType.DelayedDamageInvincible)
        );
    }

    private void ApplyInstantReloadEffect()
    {
        if (playerWeapon == null)
        {
            return;
        }

        playerWeapon.SetInstantReloadEnabled(
            IsActive(PlayerSkillType.InstantReload)
        );
    }
}
