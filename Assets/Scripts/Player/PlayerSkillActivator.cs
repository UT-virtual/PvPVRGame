using System;
using UnityEngine;

/// <summary>
/// スキル種別に応じた発動処理を行い、発動元の枠を保ったままPlayerSkillControllerへ状態記録を委ねる。
/// 特殊弾の装填など発動時だけの操作と、継続効果の再計算を順に実行する。
/// </summary>
public sealed class PlayerSkillActivator
{
    private readonly PlayerWeapon playerWeapon;
    private readonly PlayerSkillSettings settings;
    private readonly Func<string> getOwnerName;
    private readonly Action<int, PlayerSkillType, float> beginActiveSkill;
    private readonly Action applySkillEffects;

    public PlayerSkillActivator(
        PlayerWeapon playerWeapon,
        PlayerSkillSettings settings,
        Func<string> getOwnerName,
        Action<int, PlayerSkillType, float> beginActiveSkill,
        Action applySkillEffects
    )
    {
        this.playerWeapon = playerWeapon;
        this.settings = settings;
        this.getOwnerName = getOwnerName;
        this.beginActiveSkill = beginActiveSkill;
        this.applySkillEffects = applySkillEffects;
    }

    /// <summary>
    /// 呼び出し側で発動可能と確認したスキルを、指定枠の状態記録とともに実行する。
    /// slotIndexは0または1とし、選択中の枠には依存しない。
    /// </summary>
    public void Activate(PlayerSkillType skill, int slotIndex)
    {
        switch (skill)
        {
            case PlayerSkillType.DoubleJump:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.DoubleJump,
                    settings.DoubleJumpDuration,
                    $"DoubleJump activated for {settings.DoubleJumpDuration} seconds."
                );
                break;

            case PlayerSkillType.RapidFire:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.RapidFire,
                    settings.RapidFireDuration,
                    $"RapidFire activated for {settings.RapidFireDuration} seconds. " +
                    $"IntervalMultiplier={settings.RapidFireIntervalMultiplier}"
                );
                break;

            case PlayerSkillType.BulletSpeedUp:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.BulletSpeedUp,
                    settings.BulletSpeedUpDuration,
                    $"BulletSpeedUp activated for {settings.BulletSpeedUpDuration} seconds. " +
                    $"BulletSpeedMultiplier={settings.BulletSpeedMultiplier}"
                );
                break;

            case PlayerSkillType.DamageReduction:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.DamageReduction,
                    settings.DamageReductionDuration,
                    $"DamageReduction activated for {settings.DamageReductionDuration} seconds. " +
                    $"DamageTakenMultiplier={settings.DamageTakenMultiplier}"
                );
                break;

            case PlayerSkillType.XRayVision:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.XRayVision,
                    settings.XRayVisionDuration,
                    $"XRayVision activated for {settings.XRayVisionDuration} seconds."
                );
                break;

            case PlayerSkillType.MoveSpeedUp:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.MoveSpeedUp,
                    settings.MoveSpeedUpDuration,
                    $"MoveSpeedUp activated for {settings.MoveSpeedUpDuration} seconds. " +
                    $"MoveSpeedMultiplier={settings.MoveSpeedMultiplier}"
                );
                break;

            case PlayerSkillType.SlowFall:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.SlowFall,
                    settings.SlowFallDuration,
                    $"SlowFall activated for {settings.SlowFallDuration} seconds. " +
                    $"GravityMultiplier={settings.SlowFallGravityMultiplier}"
                );
                break;

            case PlayerSkillType.Shrink:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.Shrink,
                    settings.ShrinkDuration,
                    $"Shrink activated for {settings.ShrinkDuration} seconds. " +
                    $"SizeMultiplier={settings.ShrinkSizeMultiplier}, " +
                    $"DamageDealtMultiplier={settings.ShrinkDamageDealtMultiplier}"
                );
                break;

            case PlayerSkillType.DelayedDamageInvincible:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.DelayedDamageInvincible,
                    settings.DelayedDamageInvincibleDuration,
                    $"DelayedDamageInvincible activated for {settings.DelayedDamageInvincibleDuration} seconds."
                );
                break;

            case PlayerSkillType.InstantReload:
                ActivateTimedSkill(
                    slotIndex,
                    PlayerSkillType.InstantReload,
                    settings.InstantReloadDuration,
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
        int slotIndex,
        PlayerSkillType skill,
        float duration,
        string logMessage
    )
    {
        beginActiveSkill?.Invoke(slotIndex, skill, duration);
        applySkillEffects?.Invoke();

        Debug.Log($"[Skill] {GetOwnerName()}: {logMessage}");
    }

    private void ActivateGravityBurstReload(int slotIndex)
    {
        beginActiveSkill?.Invoke(
            slotIndex,
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

    private void ActivateHeavyBulletReload(int slotIndex)
    {
        beginActiveSkill?.Invoke(
            slotIndex,
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

    private void ActivateNextShotDamageBoost(int slotIndex)
    {
        beginActiveSkill?.Invoke(
            slotIndex,
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
