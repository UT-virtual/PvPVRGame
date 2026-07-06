using Fusion;
using UnityEngine;

public sealed class WeaponShotSettings
{
    public readonly NetworkPrefabRef ProjectilePrefab;

    public readonly int MaxAmmo;
    public readonly float FireInterval;
    public readonly float ReloadDuration;

    public readonly float ProjectileSpeed;
    public readonly float ProjectileLifeTime;
    public readonly float ProjectileDamage;
    public readonly float ProjectileSpawnDistance;

    public readonly Transform MuzzleTransform;
    public readonly float MuzzleExitOffset;

    public readonly float GravityBurstSpeedMultiplier;
    public readonly float GravityBurstDamageMultiplier;
    public readonly float GravityBurstGravityAcceleration;
    public readonly float GravityBurstExplosionRadius;
    public readonly float GravityBurstExplosionDamageMultiplier;

    public readonly float HeavyBulletSpeedMultiplier;
    public readonly float HeavyBulletDamageMultiplier;
    public readonly float HeavyBulletFireIntervalMultiplier;

    public WeaponShotSettings(
        NetworkPrefabRef projectilePrefab,
        int maxAmmo,
        float fireInterval,
        float reloadDuration,
        float projectileSpeed,
        float projectileLifeTime,
        float projectileDamage,
        float projectileSpawnDistance,
        Transform muzzleTransform,
        float muzzleExitOffset,
        float gravityBurstSpeedMultiplier,
        float gravityBurstDamageMultiplier,
        float gravityBurstGravityAcceleration,
        float gravityBurstExplosionRadius,
        float gravityBurstExplosionDamageMultiplier,
        float heavyBulletSpeedMultiplier,
        float heavyBulletDamageMultiplier,
        float heavyBulletFireIntervalMultiplier
    )
    {
        ProjectilePrefab = projectilePrefab;

        MaxAmmo = maxAmmo;
        FireInterval = fireInterval;
        ReloadDuration = reloadDuration;

        ProjectileSpeed = projectileSpeed;
        ProjectileLifeTime = projectileLifeTime;
        ProjectileDamage = projectileDamage;
        ProjectileSpawnDistance = projectileSpawnDistance;

        MuzzleTransform = muzzleTransform;
        MuzzleExitOffset = muzzleExitOffset;

        GravityBurstSpeedMultiplier = gravityBurstSpeedMultiplier;
        GravityBurstDamageMultiplier = gravityBurstDamageMultiplier;
        GravityBurstGravityAcceleration = gravityBurstGravityAcceleration;
        GravityBurstExplosionRadius = gravityBurstExplosionRadius;
        GravityBurstExplosionDamageMultiplier = gravityBurstExplosionDamageMultiplier;

        HeavyBulletSpeedMultiplier = heavyBulletSpeedMultiplier;
        HeavyBulletDamageMultiplier = heavyBulletDamageMultiplier;
        HeavyBulletFireIntervalMultiplier = heavyBulletFireIntervalMultiplier;
    }

    public float GetCurrentFireInterval(
        WeaponShotKind shotKind,
        float fireIntervalMultiplier
    )
    {
        float result = FireInterval * fireIntervalMultiplier;

        if (shotKind == WeaponShotKind.Heavy)
        {
            result *= HeavyBulletFireIntervalMultiplier;
        }

        return result;
    }

    public float GetCurrentProjectileSpeed(
        WeaponShotKind shotKind,
        float projectileSpeedMultiplier
    )
    {
        return ProjectileSpeed * projectileSpeedMultiplier * GetShotSpeedMultiplier(shotKind);
    }

    public float GetCurrentProjectileDamage(
        WeaponShotKind shotKind,
        float damageDealtMultiplier,
        float nextShotDamageMultiplier
    )
    {
        return
            ProjectileDamage *
            damageDealtMultiplier *
            GetShotDamageMultiplier(shotKind) *
            nextShotDamageMultiplier;
    }

    public float GetShotSpeedMultiplier(WeaponShotKind shotKind)
    {
        switch (shotKind)
        {
            case WeaponShotKind.GravityBurst:
                return GravityBurstSpeedMultiplier;

            case WeaponShotKind.Heavy:
                return HeavyBulletSpeedMultiplier;

            default:
                return 1.0f;
        }
    }

    public float GetShotDamageMultiplier(WeaponShotKind shotKind)
    {
        switch (shotKind)
        {
            case WeaponShotKind.GravityBurst:
                return GravityBurstDamageMultiplier;

            case WeaponShotKind.Heavy:
                return HeavyBulletDamageMultiplier;

            default:
                return 1.0f;
        }
    }
}
