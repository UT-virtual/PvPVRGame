using System;
using UnityEngine;

public sealed class PlayerSkillActivator
{
    private readonly PlayerWeapon playerWeapon;
    private readonly PlayerSkillSettings settings;
    private readonly Func<string> getOwnerName;
    private readonly Action<PlayerSkillType, float, int> beginActiveSkill;
    private readonly Action applySkillEffects;

    public PlayerSkillActivator(
        PlayerWeapon playerWeapon,
        PlayerSkillSettings settings,
        Func<string> getOwnerName,
        Action<PlayerSkillType, float, int> beginActiveSkill,
        Action applySkillEffects
    )
    {
        this.playerWeapon = playerWeapon;
        this.settings = settings;
        this.getOwnerName = getOwnerName;
        this.beginActiveSkill = beginActiveSkill;
        this.applySkillEffects = applySkillEffects;
    }

    public void Activate(PlayerSkillType skill, int slotIndex)
    {
        slotIndex = slotIndex <= 0 ? 0 : 1;

        switch (skill)
        {
            case PlayerSkillType.DoubleJump:
                ActivateTimedSkill(
                    PlayerSkillType.DoubleJump,
                    settings.DoubleJumpDuration,
                    slotIndex,
                    $"DoubleJump activated for {settings.DoubleJumpDuration} seconds."
                );
                break;

            case PlayerSkillType.RapidFire:
                ActivateTimedSkill(
                    PlayerSkillType.RapidFire,
                    settings.RapidFireDuration,
                    slotIndex,
                    $"RapidFire activated for {settings.RapidFireDuration} seconds. " +
                    $"IntervalMultiplier={settings.RapidFireIntervalMultiplier}"
                );
                break;

            case PlayerSkillType.BulletSpeedUp:
                ActivateTimedSkill(
                    PlayerSkillType.BulletSpeedUp,
                    settings.BulletSpeedUpDuration,
                    slotIndex,
                    $"BulletSpeedUp activated for {settings.BulletSpeedUpDuration} seconds. " +
                    $"BulletSpeedMultiplier={settings.BulletSpeedMultiplier}"
                );
                break;

            case PlayerSkillType.DamageReduction:
                ActivateTimedSkill(
                    PlayerSkillType.DamageReduction,
                    settings.DamageReductionDuration,
                    slotIndex,
                    $"DamageReduction activated for {settings.DamageReductionDuration} seconds. " +
                    $"DamageTakenMultiplier={settings.DamageTakenMultiplier}"
                );
                break;

            case PlayerSkillType.XRayVision:
                ActivateTimedSkill(
                    PlayerSkillType.XRayVision,
                    settings.XRayVisionDuration,
                    slotIndex,
                    $"XRayVision activated for {settings.XRayVisionDuration} seconds."
                );
                break;

            case PlayerSkillType.MoveSpeedUp:
                ActivateTimedSkill(
                    PlayerSkillType.MoveSpeedUp,
                    settings.MoveSpeedUpDuration,
                    slotIndex,
                    $"MoveSpeedUp activated for {settings.MoveSpeedUpDuration} seconds. " +
                    $"MoveSpeedMultiplier={settings.MoveSpeedMultiplier}"
                );
                break;

            case PlayerSkillType.SlowFall:
                ActivateTimedSkill(
                    PlayerSkillType.SlowFall,
                    settings.SlowFallDuration,
                    slotIndex,
                    $"SlowFall activated for {settings.SlowFallDuration} seconds. " +
                    $"GravityMultiplier={settings.SlowFallGravityMultiplier}"
                );
                break;

            case PlayerSkillType.Shrink:
                ActivateTimedSkill(
                    PlayerSkillType.Shrink,
                    settings.ShrinkDuration,
                    slotIndex,
                    $"Shrink activated for {settings.ShrinkDuration} seconds. " +
                    $"SizeMultiplier={settings.ShrinkSizeMultiplier}, " +
                    $"DamageDealtMultiplier={settings.ShrinkDamageDealtMultiplier}"
                );
                break;

            case PlayerSkillType.DelayedDamageInvincible:
                ActivateTimedSkill(
                    PlayerSkillType.DelayedDamageInvincible,
                    settings.DelayedDamageInvincibleDuration,
                    slotIndex,
                    $"DelayedDamageInvincible activated for {settings.DelayedDamageInvincibleDuration} seconds."
                );
                break;

            case PlayerSkillType.InstantReload:
                ActivateTimedSkill(
                    PlayerSkillType.InstantReload,
                    settings.InstantReloadDuration,
                    slotIndex,
                    $"InstantReload activated for {settings.InstantReloadDuration} seconds."
                );
                break;

            case PlayerSkillType.GravityBurstReload:
                ActivateGravityBurstReload(slotIndex);
                break;

            case PlayerSkillType.HeavyBulletReload:
                ActivateHeavyBulletReload(slotIndex);
                break;

            case PlayerSkillType.NextShotDamageBoost:
                ActivateNextShotDamageBoost(slotIndex);
                break;

            default:
                Debug.LogWarning($"[Skill] Unsupported skill: {skill}");
                break;
        }
    }

    private void ActivateTimedSkill(
        PlayerSkillType skill,
        float duration,
        int slotIndex,
        string logMessage
    )
    {
        beginActiveSkill?.Invoke(skill, duration, slotIndex);
        applySkillEffects?.Invoke();

        Debug.Log($"[Skill] {GetOwnerName()}: {logMessage}");
    }

    private void ActivateGravityBurstReload(int slotIndex)
    {
        beginActiveSkill?.Invoke(
            PlayerSkillType.GravityBurstReload,
            settings.GravityBurstReloadDuration,
            slotIndex
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

    private void ActivateHeavyBulletReload(int slotIndex)
    {
        beginActiveSkill?.Invoke(
            PlayerSkillType.HeavyBulletReload,
            settings.HeavyBulletReloadDuration,
            slotIndex
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

    private void ActivateNextShotDamageBoost(int slotIndex)
    {
        beginActiveSkill?.Invoke(
            PlayerSkillType.NextShotDamageBoost,
            settings.NextShotDamageBoostDuration,
            slotIndex
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
