using System;
using UnityEngine;

public sealed class PlayerSkillActivator
{
    private readonly PlayerWeapon playerWeapon;
    private readonly PlayerSkillSettings settings;
    private readonly Func<string> getOwnerName;
    private readonly Action<PlayerSkillType, float> beginActiveSkill;
    private readonly Action applySkillEffects;

    public PlayerSkillActivator(
        PlayerWeapon playerWeapon,
        PlayerSkillSettings settings,
        Func<string> getOwnerName,
        Action<PlayerSkillType, float> beginActiveSkill,
        Action applySkillEffects
    )
    {
        this.playerWeapon = playerWeapon;
        this.settings = settings;
        this.getOwnerName = getOwnerName;
        this.beginActiveSkill = beginActiveSkill;
        this.applySkillEffects = applySkillEffects;
    }

    public void Activate(PlayerSkillType skill)
    {
        switch (skill)
        {
            case PlayerSkillType.DoubleJump:
                ActivateTimedSkill(
                    PlayerSkillType.DoubleJump,
                    settings.DoubleJumpDuration,
                    $"DoubleJump activated for {settings.DoubleJumpDuration} seconds."
                );
                break;

            case PlayerSkillType.RapidFire:
                ActivateTimedSkill(
                    PlayerSkillType.RapidFire,
                    settings.RapidFireDuration,
                    $"RapidFire activated for {settings.RapidFireDuration} seconds. " +
                    $"IntervalMultiplier={settings.RapidFireIntervalMultiplier}"
                );
                break;

            case PlayerSkillType.BulletSpeedUp:
                ActivateTimedSkill(
                    PlayerSkillType.BulletSpeedUp,
                    settings.BulletSpeedUpDuration,
                    $"BulletSpeedUp activated for {settings.BulletSpeedUpDuration} seconds. " +
                    $"BulletSpeedMultiplier={settings.BulletSpeedMultiplier}"
                );
                break;

            case PlayerSkillType.DamageReduction:
                ActivateTimedSkill(
                    PlayerSkillType.DamageReduction,
                    settings.DamageReductionDuration,
                    $"DamageReduction activated for {settings.DamageReductionDuration} seconds. " +
                    $"DamageTakenMultiplier={settings.DamageTakenMultiplier}"
                );
                break;

            case PlayerSkillType.XRayVision:
                ActivateTimedSkill(
                    PlayerSkillType.XRayVision,
                    settings.XRayVisionDuration,
                    $"XRayVision activated for {settings.XRayVisionDuration} seconds."
                );
                break;

            case PlayerSkillType.MoveSpeedUp:
                ActivateTimedSkill(
                    PlayerSkillType.MoveSpeedUp,
                    settings.MoveSpeedUpDuration,
                    $"MoveSpeedUp activated for {settings.MoveSpeedUpDuration} seconds. " +
                    $"MoveSpeedMultiplier={settings.MoveSpeedMultiplier}"
                );
                break;

            case PlayerSkillType.SlowFall:
                ActivateTimedSkill(
                    PlayerSkillType.SlowFall,
                    settings.SlowFallDuration,
                    $"SlowFall activated for {settings.SlowFallDuration} seconds. " +
                    $"GravityMultiplier={settings.SlowFallGravityMultiplier}"
                );
                break;

            case PlayerSkillType.Shrink:
                ActivateTimedSkill(
                    PlayerSkillType.Shrink,
                    settings.ShrinkDuration,
                    $"Shrink activated for {settings.ShrinkDuration} seconds. " +
                    $"SizeMultiplier={settings.ShrinkSizeMultiplier}, " +
                    $"DamageDealtMultiplier={settings.ShrinkDamageDealtMultiplier}"
                );
                break;

            case PlayerSkillType.DelayedDamageInvincible:
                ActivateTimedSkill(
                    PlayerSkillType.DelayedDamageInvincible,
                    settings.DelayedDamageInvincibleDuration,
                    $"DelayedDamageInvincible activated for {settings.DelayedDamageInvincibleDuration} seconds."
                );
                break;

            case PlayerSkillType.InstantReload:
                ActivateTimedSkill(
                    PlayerSkillType.InstantReload,
                    settings.InstantReloadDuration,
                    $"InstantReload activated for {settings.InstantReloadDuration} seconds."
                );
                break;

            case PlayerSkillType.GravityBurstReload:
                ActivateGravityBurstReload();
                break;

            case PlayerSkillType.HeavyBulletReload:
                ActivateHeavyBulletReload();
                break;

            case PlayerSkillType.NextShotDamageBoost:
                ActivateNextShotDamageBoost();
                break;

            default:
                Debug.LogWarning($"[Skill] Unsupported skill: {skill}");
                break;
        }
    }

    private void ActivateTimedSkill(
        PlayerSkillType skill,
        float duration,
        string logMessage
    )
    {
        beginActiveSkill?.Invoke(skill, duration);
        applySkillEffects?.Invoke();

        Debug.Log($"[Skill] {GetOwnerName()}: {logMessage}");
    }

    private void ActivateGravityBurstReload()
    {
        beginActiveSkill?.Invoke(
            PlayerSkillType.GravityBurstReload,
            settings.GravityBurstReloadDuration
        );

        if (playerWeapon != null)
        {
            playerWeapon.LoadGravityBurstAmmo(settings.GravityBurstReloadAmmoCount);
        }

        applySkillEffects?.Invoke();

        Debug.Log(
            $"[Skill] {GetOwnerName()}: GravityBurstReload activated. " +
            $"Ammo={settings.GravityBurstReloadAmmoCount}"
        );
    }

    private void ActivateHeavyBulletReload()
    {
        beginActiveSkill?.Invoke(
            PlayerSkillType.HeavyBulletReload,
            settings.HeavyBulletReloadDuration
        );

        if (playerWeapon != null)
        {
            playerWeapon.LoadHeavyBulletAmmo(settings.HeavyBulletReloadAmmoCount);
        }

        applySkillEffects?.Invoke();

        Debug.Log(
            $"[Skill] {GetOwnerName()}: HeavyBulletReload activated. " +
            $"Ammo={settings.HeavyBulletReloadAmmoCount}"
        );
    }

    private void ActivateNextShotDamageBoost()
    {
        beginActiveSkill?.Invoke(
            PlayerSkillType.NextShotDamageBoost,
            settings.NextShotDamageBoostDuration
        );

        if (playerWeapon != null)
        {
            playerWeapon.SetNextShotDamageMultiplier(settings.NextShotDamageBoostMultiplier);
        }

        applySkillEffects?.Invoke();

        Debug.Log(
            $"[Skill] {GetOwnerName()}: NextShotDamageBoost activated. " +
            $"Multiplier={settings.NextShotDamageBoostMultiplier}"
        );
    }

    private string GetOwnerName()
    {
        return getOwnerName != null ? getOwnerName() : "Unknown";
    }
}
