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
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileSpawnDistance = 0.8f;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 20;
    [SerializeField] private float fireInterval = 0.15f;

    private PlayerHealth playerHealth;
    private PlayerLook playerLook;
    private PlayerCamera playerCamera;

    private int currentAmmo;
    private float fireTimer;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;

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
    }

    public void ReloadAmmo()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        currentAmmo = maxAmmo;

        OnReloaded?.Invoke();
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
    }

    public void TryFireProjectile()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (fireTimer > 0.0f)
        {
            return;
        }

        if (currentAmmo <= 0)
        {
            Debug.Log("No ammo. Press K or ZL to reload.");

            fireTimer = fireInterval;
            OnDryFire?.Invoke();

            return;
        }

        FireProjectile();

        currentAmmo--;
        fireTimer = fireInterval;

        OnShot?.Invoke();
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);

        Debug.Log($"Ammo: {currentAmmo}/{maxAmmo}");
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
            projectileSpeed,
            projectileLifeTime,
            projectileDamage,
            playerHealth
        );
    }
}