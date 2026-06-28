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

    private int currentAmmo;
    private float fireTimer;
    private float reloadTimer;
    private bool isReloading;

    private float fireIntervalMultiplier = 1.0f;
    private float projectileSpeedMultiplier = 1.0f;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public bool IsReloading => isReloading;

    public event Action OnShot;
    public event Action OnReloaded;
    public event Action OnDryFire;
    public event Action<int, int> OnAmmoChanged;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerLook = GetComponent<PlayerLook>();
        playerCamera = GetComponent<PlayerCamera>();

        currentAmmo = maxAmmo;
        fireTimer = 0.0f;
        reloadTimer = 0.0f;
        isReloading = false;

        fireIntervalMultiplier = 1.0f;
        projectileSpeedMultiplier = 1.0f;
    }

    private void Start()
    {
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
        Debug.Log($"Reloaded: {currentAmmo}/{maxAmmo}");
    }

    public void Tick(float deltaTime)
    {
        if (fireTimer > 0.0f)
        {
            fireTimer -= deltaTime;
        }

        if (!isReloading)
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

        if (isReloading)
        {
            return;
        }

        if (currentAmmo >= maxAmmo)
        {
            return;
        }

        isReloading = true;
        reloadTimer = reloadDuration;

        Debug.Log($"Reload started. Duration={reloadDuration} seconds.");
    }

    public void RefillAmmoImmediately()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        isReloading = false;
        reloadTimer = 0.0f;
        currentAmmo = maxAmmo;

        OnReloaded?.Invoke();
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);

        Debug.Log($"Ammo refilled immediately: {currentAmmo}/{maxAmmo}");
    }

    private void CompleteReload()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        isReloading = false;
        reloadTimer = 0.0f;
        currentAmmo = maxAmmo;

        OnReloaded?.Invoke();
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);

        Debug.Log($"Reload completed: {currentAmmo}/{maxAmmo}");
    }

    public void CancelReload()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (!isReloading)
        {
            return;
        }

        isReloading = false;
        reloadTimer = 0.0f;

        Debug.Log("Reload canceled.");
    }

    public void TryFireProjectile()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (isReloading)
        {
            Debug.Log("Cannot fire while reloading.");
            return;
        }

        if (fireTimer > 0.0f)
        {
            return;
        }

        float currentFireInterval = GetCurrentFireInterval();

        if (currentAmmo <= 0)
        {
            Debug.Log("No ammo. Press K or ZL to reload.");

            fireTimer = currentFireInterval;
            OnDryFire?.Invoke();

            return;
        }

        FireProjectile();

        currentAmmo--;
        fireTimer = currentFireInterval;

        OnShot?.Invoke();
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);

        Debug.Log(
            $"Ammo: {currentAmmo}/{maxAmmo}, " +
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