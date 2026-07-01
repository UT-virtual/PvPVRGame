using System;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerLook))]
[RequireComponent(typeof(PlayerCamera))]
public class PlayerWeapon : NetworkBehaviour
{
    private enum ProjectileShotKind
    {
        Normal,
        GravityBurst,
        Heavy
    }

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

    private float fireTimer;
    private float reloadTimer;

    private float fireIntervalMultiplier = 1.0f;
    private float projectileSpeedMultiplier = 1.0f;
    private bool instantReloadEnabled;
    private float damageDealtMultiplier = 1.0f;
    private ProjectileShotKind loadedShotKind = ProjectileShotKind.Normal;
    private int loadedSpecialAmmoRemaining;
    private float pendingNextShotDamageMultiplier = 1.0f;

    [Networked, OnChangedRender(nameof(OnNetworkedAmmoChanged))]
    public int NetworkedCurrentAmmo { get; private set; }

    [Networked, OnChangedRender(nameof(OnNetworkedReloadingChanged))]
    public NetworkBool NetworkedIsReloading { get; private set; }

    public int CurrentAmmo => NetworkedCurrentAmmo;
    public int MaxAmmo => maxAmmo;
    public bool IsReloading => NetworkedIsReloading;

    public event Action OnShot;
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

        fireIntervalMultiplier = 1.0f;
        projectileSpeedMultiplier = 1.0f;

        loadedShotKind = ProjectileShotKind.Normal;
        loadedSpecialAmmoRemaining = 0;
        pendingNextShotDamageMultiplier = 1.0f;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            NetworkedCurrentAmmo = maxAmmo;
            NetworkedIsReloading = false;
        }

        NotifyAmmoChanged();

        Debug.Log($"Weapon Spawned: Ammo={NetworkedCurrentAmmo}/{maxAmmo}");
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
        fireIntervalMultiplier = Mathf.Clamp(multiplier, 0.05f, 10.0f);
    }

    public void SetProjectileSpeedMultiplier(float multiplier)
    {
        projectileSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 10.0f);
    }

    private float GetCurrentFireInterval()
    {
        float result = fireInterval * fireIntervalMultiplier;

        if (GetCurrentShotKind() == ProjectileShotKind.Heavy)
        {
            result *= heavyBulletFireIntervalMultiplier;
        }

        return result;
    }

    private float GetCurrentProjectileSpeed()
    {
        return projectileSpeed * projectileSpeedMultiplier * GetShotSpeedMultiplier(GetCurrentShotKind());
    }

    private ProjectileShotKind GetCurrentShotKind()
    {
        if (loadedShotKind == ProjectileShotKind.Normal || loadedSpecialAmmoRemaining <= 0)
        {
            return ProjectileShotKind.Normal;
        }

        return loadedShotKind;
    }

    private float GetShotSpeedMultiplier(ProjectileShotKind shotKind)
    {
        switch (shotKind)
        {
            case ProjectileShotKind.GravityBurst:
                return gravityBurstSpeedMultiplier;

            case ProjectileShotKind.Heavy:
                return heavyBulletSpeedMultiplier;

            default:
                return 1.0f;
        }
    }

    private float GetShotDamageMultiplier(ProjectileShotKind shotKind)
    {
        switch (shotKind)
        {
            case ProjectileShotKind.GravityBurst:
                return gravityBurstDamageMultiplier;

            case ProjectileShotKind.Heavy:
                return heavyBulletDamageMultiplier;

            default:
                return 1.0f;
        }
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

        ClearLoadedSpecialAmmo();

        if (instantReloadEnabled)
        {
            RefillAmmoImmediately();

            Debug.Log("Instant reload activated.");
            return;
        }

        if (instantReloadEnabled)
        {
            RefillAmmoImmediately();

            Debug.Log("Instant reload activated.");
            return;
        }

        NetworkedIsReloading = true;
        reloadTimer = reloadDuration;

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

        ClearLoadedSpecialAmmo();
        pendingNextShotDamageMultiplier = 1.0f;

        NotifyAmmoChanged();

        Debug.Log($"Ammo refilled immediately: {NetworkedCurrentAmmo}/{maxAmmo}");
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

        ClearLoadedSpecialAmmo();

        OnReloaded?.Invoke();
        NotifyAmmoChanged();

        Debug.Log($"Reload completed: {NetworkedCurrentAmmo}/{maxAmmo}");
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

        float currentFireInterval = GetCurrentFireInterval();

        if (NetworkedCurrentAmmo <= 0)
        {
            Debug.Log("No ammo. Press K or ZL to reload.");

            fireTimer = currentFireInterval;
            OnDryFire?.Invoke();

            return;
        }

        ProjectileShotKind shotKind = GetCurrentShotKind();

        if (!FireProjectile(shotKind))
        {
            return;
        }

        NetworkedCurrentAmmo--;
        ConsumeLoadedSpecialAmmo();
        fireTimer = currentFireInterval;

        OnShot?.Invoke();
        NotifyAmmoChanged();

        Debug.Log(
            $"Ammo: {NetworkedCurrentAmmo}/{maxAmmo}, " +
            $"FireInterval={currentFireInterval:0.00}, " +
            $"FireIntervalMultiplier={fireIntervalMultiplier:0.00}, " +
            $"ProjectileDamage={projectileDamage:0.00}, " +
            $"ProjectileSpeed={GetCurrentProjectileSpeed():0.00}, " +
            $"ProjectileSpeedMultiplier={projectileSpeedMultiplier:0.00}"
        );
    }

    private bool FireProjectile(ProjectileShotKind shotKind)
    {
        if (!projectilePrefab.IsValid)
        {
            Debug.LogError($"{name}: Projectile Prefab is not assigned.");
            return false;
        }

        Vector3 fireDirection;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (muzzleTransform != null)
        {
            fireDirection = muzzleTransform.forward.normalized;
            spawnPosition = muzzleTransform.position + fireDirection * muzzleExitOffset;
            spawnRotation = Quaternion.LookRotation(fireDirection, muzzleTransform.up);
        }
        else
        {
            fireDirection = playerLook.ViewForward;
            spawnPosition = playerCamera.CameraPosition + fireDirection * projectileSpawnDistance;
            spawnRotation = Quaternion.LookRotation(fireDirection, playerLook.ViewUp);
        }

        NetworkObject projectileObject = Runner.Spawn(
            projectilePrefab,
            spawnPosition,
            spawnRotation,
            Object.InputAuthority
        );

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile == null)
        {
            Runner.Despawn(projectileObject);
            return false;
        }

        bool useGravityBurst = shotKind == ProjectileShotKind.GravityBurst;

        float currentDamage =
            projectileDamage *
            damageDealtMultiplier *
            GetShotDamageMultiplier(shotKind) *
            ConsumeNextShotDamageMultiplier();

        float currentSpeed =
            projectileSpeed *
            projectileSpeedMultiplier *
            GetShotSpeedMultiplier(shotKind);

        Vector3 gravityDirection = GetProjectileGravityDirection();

        projectile.Initialize(
            fireDirection,
            currentSpeed,
            projectileLifeTime,
            currentDamage,
            playerHealth,
            useGravityBurst,
            gravityDirection,
            gravityBurstGravityAcceleration,
            useGravityBurst,
            gravityBurstExplosionRadius,
            currentDamage * gravityBurstExplosionDamageMultiplier
        );

        return true;
    }

    public void SetDamageDealtMultiplier(float multiplier)
    {
        damageDealtMultiplier = Mathf.Clamp(multiplier, 0.0f, 10.0f);
    }

    public void LoadGravityBurstAmmo(int ammoCount)
    {
        LoadSpecialAmmo(ProjectileShotKind.GravityBurst, ammoCount);
    }

    public void LoadHeavyBulletAmmo(int ammoCount)
    {
        LoadSpecialAmmo(ProjectileShotKind.Heavy, ammoCount);
    }

    public void SetNextShotDamageMultiplier(float multiplier)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        pendingNextShotDamageMultiplier = Mathf.Clamp(multiplier, 1.0f, 10.0f);
    }

    private void LoadSpecialAmmo(ProjectileShotKind shotKind, int ammoCount)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        int clampedAmmoCount = Mathf.Clamp(ammoCount, 1, maxAmmo);

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;
        loadedShotKind = shotKind;
        loadedSpecialAmmoRemaining = clampedAmmoCount;
        NetworkedCurrentAmmo = clampedAmmoCount;

        NotifyAmmoChanged();

        Debug.Log(
            $"Special ammo loaded: Kind={loadedShotKind}, " +
            $"Ammo={NetworkedCurrentAmmo}/{maxAmmo}"
        );
    }

    private void ConsumeLoadedSpecialAmmo()
    {
        if (loadedShotKind == ProjectileShotKind.Normal || loadedSpecialAmmoRemaining <= 0)
        {
            return;
        }

        loadedSpecialAmmoRemaining = Mathf.Max(loadedSpecialAmmoRemaining - 1, 0);

        if (loadedSpecialAmmoRemaining > 0 && NetworkedCurrentAmmo > 0)
        {
            return;
        }

        ClearLoadedSpecialAmmo();
    }

    private void ClearLoadedSpecialAmmo()
    {
        loadedShotKind = ProjectileShotKind.Normal;
        loadedSpecialAmmoRemaining = 0;
    }

    private float ConsumeNextShotDamageMultiplier()
    {
        float result = pendingNextShotDamageMultiplier;
        pendingNextShotDamageMultiplier = 1.0f;
        return result;
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

    public void SetInstantReloadEnabled(bool enabled)
    {
        instantReloadEnabled = enabled;

        if (!instantReloadEnabled)
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
}