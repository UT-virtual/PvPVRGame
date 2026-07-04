public sealed class PlayerSkillSettings
{
    public readonly float CooldownAfterSkillEnd;

    public readonly float DoubleJumpDuration;
    public readonly int DoubleJumpExtraAirJumpCount;

    public readonly float RapidFireDuration;
    public readonly float RapidFireIntervalMultiplier;

    public readonly float BulletSpeedUpDuration;
    public readonly float BulletSpeedMultiplier;

    public readonly float DamageReductionDuration;
    public readonly float DamageTakenMultiplier;

    public readonly float XRayVisionDuration;

    public readonly float MoveSpeedUpDuration;
    public readonly float MoveSpeedMultiplier;

    public readonly float SlowFallDuration;
    public readonly float SlowFallGravityMultiplier;

    public readonly float ShrinkDuration;
    public readonly float ShrinkSizeMultiplier;
    public readonly float ShrinkDamageDealtMultiplier;

    public readonly float DelayedDamageInvincibleDuration;

    public readonly float InstantReloadDuration;

    public readonly float GravityBurstReloadDuration;
    public readonly int GravityBurstReloadAmmoCount;

    public readonly float HeavyBulletReloadDuration;
    public readonly int HeavyBulletReloadAmmoCount;

    public readonly float NextShotDamageBoostDuration;
    public readonly float NextShotDamageBoostMultiplier;

    public PlayerSkillSettings(
        float cooldownAfterSkillEnd,
        float doubleJumpDuration,
        int doubleJumpExtraAirJumpCount,
        float rapidFireDuration,
        float rapidFireIntervalMultiplier,
        float bulletSpeedUpDuration,
        float bulletSpeedMultiplier,
        float damageReductionDuration,
        float damageTakenMultiplier,
        float xRayVisionDuration,
        float moveSpeedUpDuration,
        float moveSpeedMultiplier,
        float slowFallDuration,
        float slowFallGravityMultiplier,
        float shrinkDuration,
        float shrinkSizeMultiplier,
        float shrinkDamageDealtMultiplier,
        float delayedDamageInvincibleDuration,
        float instantReloadDuration,
        float gravityBurstReloadDuration,
        int gravityBurstReloadAmmoCount,
        float heavyBulletReloadDuration,
        int heavyBulletReloadAmmoCount,
        float nextShotDamageBoostDuration,
        float nextShotDamageBoostMultiplier
    )
    {
        CooldownAfterSkillEnd = cooldownAfterSkillEnd;

        DoubleJumpDuration = doubleJumpDuration;
        DoubleJumpExtraAirJumpCount = doubleJumpExtraAirJumpCount;

        RapidFireDuration = rapidFireDuration;
        RapidFireIntervalMultiplier = rapidFireIntervalMultiplier;

        BulletSpeedUpDuration = bulletSpeedUpDuration;
        BulletSpeedMultiplier = bulletSpeedMultiplier;

        DamageReductionDuration = damageReductionDuration;
        DamageTakenMultiplier = damageTakenMultiplier;

        XRayVisionDuration = xRayVisionDuration;

        MoveSpeedUpDuration = moveSpeedUpDuration;
        MoveSpeedMultiplier = moveSpeedMultiplier;

        SlowFallDuration = slowFallDuration;
        SlowFallGravityMultiplier = slowFallGravityMultiplier;

        ShrinkDuration = shrinkDuration;
        ShrinkSizeMultiplier = shrinkSizeMultiplier;
        ShrinkDamageDealtMultiplier = shrinkDamageDealtMultiplier;

        DelayedDamageInvincibleDuration = delayedDamageInvincibleDuration;

        InstantReloadDuration = instantReloadDuration;

        GravityBurstReloadDuration = gravityBurstReloadDuration;
        GravityBurstReloadAmmoCount = gravityBurstReloadAmmoCount;

        HeavyBulletReloadDuration = heavyBulletReloadDuration;
        HeavyBulletReloadAmmoCount = heavyBulletReloadAmmoCount;

        NextShotDamageBoostDuration = nextShotDamageBoostDuration;
        NextShotDamageBoostMultiplier = nextShotDamageBoostMultiplier;
    }
}
