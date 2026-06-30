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

    private PlayerHealth playerHealth;
    private PlayerLook playerLook;
    private PlayerCamera playerCamera;

    private float fireTimer;
    private float reloadTimer;

    private float fireIntervalMultiplier = 1.0f;
    private float projectileSpeedMultiplier = 1.0f;

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
        return fireInterval * fireIntervalMultiplier;
    }

    private float GetCurrentProjectileSpeed()
    {
        return projectileSpeed * projectileSpeedMultiplier;
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

        FireProjectile();

        NetworkedCurrentAmmo--;
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

    private void FireProjectile()
    {
        if (!projectilePrefab.IsValid)
        {
            Debug.LogError($"{name}: Projectile Prefab is not assigned or is not a NetworkPrefabRef.");
            return;
        }

        Vector3 fireDirection = playerLook.ViewForward;

        Vector3 spawnPosition =
            playerCamera.CameraPosition
            + fireDirection * projectileSpawnDistance;

        Quaternion spawnRotation = Quaternion.LookRotation(fireDirection, playerLook.ViewUp);

        NetworkObject projectileObject = Runner.Spawn(
            projectilePrefab,
            spawnPosition,
            spawnRotation,
            Object.InputAuthority
        );

        Projectile projectile = projectileObject.GetComponent<Projectile>();

        if (projectile == null)
        {
            Debug.LogError($"{name}: Spawned projectile does not have Projectile component.");
            Runner.Despawn(projectileObject);
            return;
        }

        projectile.Initialize(
            fireDirection,
            GetCurrentProjectileSpeed(),
            projectileLifeTime,
            projectileDamage,
            playerHealth
        );
    }
}