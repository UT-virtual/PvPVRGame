using System;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerLook))]
[RequireComponent(typeof(PlayerCamera))]
public class PlayerWeapon : MonoBehaviour
{
    [Header("Shoot")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 18.0f;
    [SerializeField] private float projectileLifeTime = 3.0f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileSpawnDistance = 0.8f;
    [SerializeField] private float projectileScale = 0.15f;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 20;
    [SerializeField] private float fireInterval = 0.15f;

    private PlayerHealth playerHealth;
    private PlayerLook playerLook;
    private PlayerCamera PlayerCamera;

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
        PlayerCamera = GetComponent<PlayerCamera>();

        currentAmmo = maxAmmo;
        fireTimer = 0.0f;
    }

    private void Start()
    {
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
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
        currentAmmo = maxAmmo;

        OnReloaded?.Invoke();
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);

        Debug.Log($"Reloaded: {currentAmmo}/{maxAmmo}");
    }

    public void TryFireProjectile()
    {
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
        Vector3 fireDirection = playerLook.ViewForward;

        Vector3 spawnPosition =
            PlayerCamera.CameraPosition
            + fireDirection * projectileSpawnDistance;

        Quaternion spawnRotation = Quaternion.LookRotation(fireDirection, playerLook.ViewUp);

        GameObject projectileObject;

        if (projectilePrefab != null)
        {
            projectileObject = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
        }
        else
        {
            projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            projectileObject.transform.localScale = Vector3.one * projectileScale;

            Renderer renderer = projectileObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.yellow;
            }
        }

        Projectile projectile = projectileObject.GetComponent<Projectile>();

        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<Projectile>();
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