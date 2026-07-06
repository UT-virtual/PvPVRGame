using System;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerLook))]
[RequireComponent(typeof(PlayerCamera))]
public class PlayerWeapon : NetworkBehaviour
{
    [Header("Shoot")]
    [SerializeField] private NetworkPrefabRef projectilePrefab;
    [SerializeField] private float projectileSpeed = 18.0f;
    [SerializeField] private float projectileLifeTime = 3.0f;
    [SerializeField] private float projectileDamage = 1.0f;
    [SerializeField] private float projectileSpawnDistance = 0.8f;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 20;
    [SerializeField] private float fireInterval = 0.15f;

    [Header("Reload")]
    [SerializeField] private float reloadDuration = 3.0f;

    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private float muzzleExitOffset = 0.05f;

    [Header("Gravity Burst Ammo")]
    [SerializeField] private float gravityBurstSpeedMultiplier = 0.75f;
    [SerializeField] private float gravityBurstDamageMultiplier = 1.0f;
    [SerializeField] private float gravityBurstGravityAcceleration = 18.0f;
    [SerializeField] private float gravityBurstExplosionRadius = 3.0f;
    [SerializeField] private float gravityBurstExplosionDamageMultiplier = 1.0f;

    [Header("Heavy Bullet Ammo")]
    [SerializeField] private float heavyBulletSpeedMultiplier = 3.0f;
    [SerializeField] private float heavyBulletDamageMultiplier = 5.0f;
    [SerializeField] private float heavyBulletFireIntervalMultiplier = 6.0f;

    private PlayerHealth playerHealth;
    private PlayerLook playerLook;
    private PlayerCamera playerCamera;

    private WeaponShotSettings shotSettings;
    private WeaponShotState shotState;
    private ProjectileSpawner projectileSpawner;

    private float fireTimer;
    private float reloadTimer;

    [Networked, OnChangedRender(nameof(OnNetworkedAmmoChanged))]
    public int NetworkedCurrentAmmo { get; private set; }

    [Networked, OnChangedRender(nameof(OnNetworkedReloadingChanged))]
    public NetworkBool NetworkedIsReloading { get; private set; }

    public int CurrentAmmo => NetworkedCurrentAmmo;
    public int MaxAmmo => maxAmmo;
    public bool IsReloading => NetworkedIsReloading;

    public event Action OnShot;
    public event Action OnReloadStarted;
    public event Action OnReloadCanceled;
    public event Action OnReloaded;
    public event Action OnDryFire;
    public event Action<int, int> OnAmmoChanged;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerLook = GetComponent<PlayerLook>();
        playerCamera = GetComponent<PlayerCamera>();

        fireTimer = 0.0f;
        reloadTimer = 0.0f;

        shotSettings = BuildShotSettings();
        shotState = new WeaponShotState();
        projectileSpawner = new ProjectileSpawner(
            playerLook,
            playerCamera,
            playerHealth,
            shotSettings
        );
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            NetworkedCurrentAmmo = maxAmmo;
            NetworkedIsReloading = false;
            shotState.Reset();
        }

        NotifyAmmoChanged();

        Debug.Log($"Weapon Spawned: Ammo={NetworkedCurrentAmmo}/{maxAmmo}");
    }

    private WeaponShotSettings BuildShotSettings()
    {
        return new WeaponShotSettings(
            projectilePrefab,
            maxAmmo,
            fireInterval,
            reloadDuration,
            projectileSpeed,
            projectileLifeTime,
            projectileDamage,
            projectileSpawnDistance,
            muzzleTransform,
            muzzleExitOffset,
            gravityBurstSpeedMultiplier,
            gravityBurstDamageMultiplier,
            gravityBurstGravityAcceleration,
            gravityBurstExplosionRadius,
            gravityBurstExplosionDamageMultiplier,
            heavyBulletSpeedMultiplier,
            heavyBulletDamageMultiplier,
            heavyBulletFireIntervalMultiplier
        );
    }

    private void OnNetworkedAmmoChanged()
    {
        NotifyAmmoChanged();
    }

    private void OnNetworkedReloadingChanged()
    {
        NotifyAmmoChanged();
    }

    private void NotifyAmmoChanged()
    {
        OnAmmoChanged?.Invoke(NetworkedCurrentAmmo, maxAmmo);
    }

    public void Tick(float deltaTime)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (fireTimer > 0.0f)
        {
            fireTimer -= deltaTime;
        }

        if (!NetworkedIsReloading)
        {
            return;
        }

        reloadTimer -= deltaTime;

        if (reloadTimer > 0.0f)
        {
            return;
        }

        CompleteReload();
    }

    public void SetFireIntervalMultiplier(float multiplier)
    {
        shotState.SetFireIntervalMultiplier(multiplier);
    }

    public void SetProjectileSpeedMultiplier(float multiplier)
    {
        shotState.SetProjectileSpeedMultiplier(multiplier);
    }

    public void SetDamageDealtMultiplier(float multiplier)
    {
        shotState.SetDamageDealtMultiplier(multiplier);
    }

    public void ReloadAmmo()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (NetworkedIsReloading)
        {
            return;
        }

        if (NetworkedCurrentAmmo >= maxAmmo)
        {
            return;
        }

        shotState.ClearLoadedSpecialAmmo();

        if (shotState.InstantReloadEnabled)
        {
            RefillAmmoImmediately();

            Debug.Log("Instant reload activated.");
            return;
        }

        NetworkedIsReloading = true;
        reloadTimer = reloadDuration;

        OnReloadStarted?.Invoke();

        NotifyAmmoChanged();

        Debug.Log($"Reload started. Duration={reloadDuration} seconds.");
    }

    public void RefillAmmoImmediately()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;
        NetworkedCurrentAmmo = maxAmmo;

        shotState.ClearLoadedSpecialAmmo();
        shotState.ClearNextShotDamageMultiplier();

        NotifyAmmoChanged();

        Debug.Log($"Ammo refilled immediately: {NetworkedCurrentAmmo}/{maxAmmo}");
    }

    public void CancelReload()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (!NetworkedIsReloading)
        {
            return;
        }

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;

        OnReloadCanceled?.Invoke();

        NotifyAmmoChanged();

        Debug.Log("Reload canceled.");
    }

    public void TryFireProjectile()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (NetworkedIsReloading)
        {
            Debug.Log("Cannot fire while reloading.");
            return;
        }

        if (fireTimer > 0.0f)
        {
            return;
        }

        WeaponShotKind shotKind = shotState.GetCurrentShotKind();
        float currentFireInterval = shotSettings.GetCurrentFireInterval(
            shotKind,
            shotState.FireIntervalMultiplier
        );

        if (NetworkedCurrentAmmo <= 0)
        {
            Debug.Log("No ammo. Press K or ZL to reload.");

            fireTimer = currentFireInterval;
            OnDryFire?.Invoke();

            return;
        }

        if (!projectileSpawner.FireProjectile(
                Runner,
                Object.InputAuthority,
                name,
                shotKind,
                shotState.ProjectileSpeedMultiplier,
                shotState.DamageDealtMultiplier,
                shotState.PendingNextShotDamageMultiplier
            ))
        {
            return;
        }

        shotState.ConsumeNextShotDamageMultiplier();

        NetworkedCurrentAmmo--;

        shotState.ConsumeLoadedSpecialAmmo(NetworkedCurrentAmmo);

        fireTimer = currentFireInterval;

        OnShot?.Invoke();
        NotifyAmmoChanged();

        Debug.Log(
            $"Ammo: {NetworkedCurrentAmmo}/{maxAmmo}, " +
            $"FireInterval={currentFireInterval:0.00}, " +
            $"FireIntervalMultiplier={shotState.FireIntervalMultiplier:0.00}, " +
            $"ProjectileDamage={projectileDamage:0.00}, " +
            $"ProjectileSpeed={shotSettings.GetCurrentProjectileSpeed(shotKind, shotState.ProjectileSpeedMultiplier):0.00}, " +
            $"ProjectileSpeedMultiplier={shotState.ProjectileSpeedMultiplier:0.00}"
        );
    }

    public void LoadGravityBurstAmmo(int ammoCount)
    {
        LoadSpecialAmmo(WeaponShotKind.GravityBurst, ammoCount);
    }

    public void LoadHeavyBulletAmmo(int ammoCount)
    {
        LoadSpecialAmmo(WeaponShotKind.Heavy, ammoCount);
    }

    public void SetNextShotDamageMultiplier(float multiplier)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        shotState.SetNextShotDamageMultiplier(multiplier);
    }

    public void SetInstantReloadEnabled(bool enabled)
    {
        shotState.SetInstantReloadEnabled(enabled);

        if (!shotState.InstantReloadEnabled)
        {
            return;
        }

        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (NetworkedIsReloading)
        {
            RefillAmmoImmediately();
        }
    }

    private void CompleteReload()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;
        NetworkedCurrentAmmo = maxAmmo;

        shotState.ClearLoadedSpecialAmmo();

        OnReloaded?.Invoke();
        NotifyAmmoChanged();

        Debug.Log($"Reload completed: {NetworkedCurrentAmmo}/{maxAmmo}");
    }

    private void LoadSpecialAmmo(WeaponShotKind shotKind, int ammoCount)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;
        NetworkedCurrentAmmo = shotState.LoadSpecialAmmo(
            shotKind,
            ammoCount,
            maxAmmo
        );

        NotifyAmmoChanged();

        Debug.Log(
            $"Special ammo loaded: Kind={shotKind}, " +
            $"Ammo={NetworkedCurrentAmmo}/{maxAmmo}"
        );
    }
}
