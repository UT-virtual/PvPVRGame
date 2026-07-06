using Fusion;
using UnityEngine;

public sealed class ProjectileSpawner
{
    private readonly PlayerLook playerLook;
    private readonly PlayerCamera playerCamera;
    private readonly PlayerHealth playerHealth;
    private readonly WeaponShotSettings settings;

    public ProjectileSpawner(
        PlayerLook playerLook,
        PlayerCamera playerCamera,
        PlayerHealth playerHealth,
        WeaponShotSettings settings
    )
    {
        this.playerLook = playerLook;
        this.playerCamera = playerCamera;
        this.playerHealth = playerHealth;
        this.settings = settings;
    }

    public bool FireProjectile(
        NetworkRunner runner,
        PlayerRef inputAuthority,
        string ownerName,
        WeaponShotKind shotKind,
        float projectileSpeedMultiplier,
        float damageDealtMultiplier,
        float nextShotDamageMultiplier
    )
    {
        if (runner == null)
        {
            Debug.LogError($"{ownerName}: NetworkRunner is null.");
            return false;
        }

        if (!settings.ProjectilePrefab.IsValid)
        {
            Debug.LogError($"{ownerName}: Projectile Prefab is not assigned.");
            return false;
        }

        GetFirePose(
            out Vector3 fireDirection,
            out Vector3 spawnPosition,
            out Quaternion spawnRotation
        );

        NetworkObject projectileObject = runner.Spawn(
            settings.ProjectilePrefab,
            spawnPosition,
            spawnRotation,
            inputAuthority
        );

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile == null)
        {
            runner.Despawn(projectileObject);
            return false;
        }

        bool useGravityBurst = shotKind == WeaponShotKind.GravityBurst;

        float currentDamage = settings.GetCurrentProjectileDamage(
            shotKind,
            damageDealtMultiplier,
            nextShotDamageMultiplier
        );

        float currentSpeed = settings.GetCurrentProjectileSpeed(
            shotKind,
            projectileSpeedMultiplier
        );

        Vector3 gravityDirection = GetProjectileGravityDirection();

        projectile.Initialize(
            fireDirection,
            currentSpeed,
            settings.ProjectileLifeTime,
            currentDamage,
            playerHealth,
            useGravityBurst,
            gravityDirection,
            settings.GravityBurstGravityAcceleration,
            useGravityBurst,
            settings.GravityBurstExplosionRadius,
            currentDamage * settings.GravityBurstExplosionDamageMultiplier
        );

        return true;
    }

    private void GetFirePose(
        out Vector3 fireDirection,
        out Vector3 spawnPosition,
        out Quaternion spawnRotation
    )
    {
        if (settings.MuzzleTransform != null)
        {
            fireDirection = settings.MuzzleTransform.forward.normalized;
            spawnPosition = settings.MuzzleTransform.position + fireDirection * settings.MuzzleExitOffset;
            spawnRotation = Quaternion.LookRotation(fireDirection, settings.MuzzleTransform.up);
            return;
        }

        fireDirection = playerLook.ViewForward;
        spawnPosition = playerCamera.CameraPosition + fireDirection * settings.ProjectileSpawnDistance;
        spawnRotation = Quaternion.LookRotation(fireDirection, playerLook.ViewUp);
    }

    private Vector3 GetProjectileGravityDirection()
    {
        if (playerLook != null && playerLook.ViewUp.sqrMagnitude > 0.0001f)
        {
            return -playerLook.ViewUp.normalized;
        }

        if (Physics.gravity.sqrMagnitude > 0.0001f)
        {
            return Physics.gravity.normalized;
        }

        return Vector3.down;
    }
}
