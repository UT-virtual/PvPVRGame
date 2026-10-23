
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
    [SerializeField] private float raycastMaxDistance = 100.0f;
    [SerializeField] private LayerMask hitScanLayerMask = Physics.DefaultRaycastLayers;

    [Header("Muzzle")]
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private float muzzleExitOffset = 0.05f;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 20;
    [SerializeField] private float fireInterval = 0.15f;

    [Header("Reload")]
    [SerializeField] private float reloadDuration = 3.0f;

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
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            NetworkedCurrentAmmo = maxAmmo;
            NetworkedIsReloading = false;
        }

        NotifyAmmoChanged();
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
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (fireTimer > 0.0f)
            fireTimer -= deltaTime;

        if (!NetworkedIsReloading)
            return;

        reloadTimer -= deltaTime;

        if (reloadTimer <= 0.0f)
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

    private ProjectileShotKind GetCurrentShotKind()
    {
        if (loadedShotKind == ProjectileShotKind.Normal ||
            loadedSpecialAmmoRemaining <= 0)
        {
            return ProjectileShotKind.Normal;
        }

        return loadedShotKind;
    }

    private float GetCurrentFireInterval()
    {
        float result = fireInterval * fireIntervalMultiplier;

        if (GetCurrentShotKind() == ProjectileShotKind.Heavy)
            result *= heavyBulletFireIntervalMultiplier;

        return result;
    }

    private float GetCurrentProjectileSpeed()
    {
        return projectileSpeed
            * projectileSpeedMultiplier
            * GetShotSpeedMultiplier(GetCurrentShotKind());
    }

    private float GetShotSpeedMultiplier(ProjectileShotKind kind)
    {
        switch (kind)
        {
            case ProjectileShotKind.GravityBurst:
                return gravityBurstSpeedMultiplier;
            case ProjectileShotKind.Heavy:
                return heavyBulletSpeedMultiplier;
            default:
                return 1.0f;
        }
    }

    private float GetShotDamageMultiplier(ProjectileShotKind kind)
    {
        switch (kind)
        {
            case ProjectileShotKind.GravityBurst:
                return gravityBurstDamageMultiplier;
            case ProjectileShotKind.Heavy:
                return heavyBulletDamageMultiplier;
            default:
                return 1.0f;
        }
    }

    public void TryFireProjectile()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (NetworkedIsReloading || fireTimer > 0.0f)
            return;

        float interval = GetCurrentFireInterval();

        if (NetworkedCurrentAmmo <= 0)
        {
            fireTimer = interval;
            OnDryFire?.Invoke();
            return;
        }

        ProjectileShotKind kind = GetCurrentShotKind();

        if (!FireProjectile(kind))
            return;

        NetworkedCurrentAmmo--;
        ConsumeLoadedSpecialAmmo();
        fireTimer = interval;

        OnShot?.Invoke();
        NotifyAmmoChanged();
    }

    private bool FireProjectile(ProjectileShotKind kind)
    {
        if (!projectilePrefab.IsValid)
        {
            Debug.LogError("Projectile Prefab is not assigned.");
            return false;
        }

        Vector3 direction;
        Vector3 spawnPosition;
        Vector3 up;

        if (muzzleTransform != null)
        {
            direction = muzzleTransform.forward.normalized;
            spawnPosition = muzzleTransform.position
                + direction * muzzleExitOffset;
            up = muzzleTransform.up;
        }
        else
        {
            direction = playerLook.ViewForward.normalized;
            spawnPosition = playerCamera.CameraPosition
                + direction * projectileSpawnDistance;
            up = playerLook.ViewUp;
        }

        float damage = projectileDamage
            * damageDealtMultiplier
            * GetShotDamageMultiplier(kind)
            * ConsumeNextShotDamageMultiplier();

        float speed = projectileSpeed
            * projectileSpeedMultiplier
            * GetShotSpeedMultiplier(kind);

        bool isGravityBurst = kind == ProjectileShotKind.GravityBurst;

        // 通常弾・ヘビー弾は発射時に即座に命中判定
        if (!isGravityBurst)
        {
            ApplyHitscan(spawnPosition, direction, damage);
        }

        NetworkObject projectileObject = Runner.Spawn(
            projectilePrefab,
            spawnPosition,
            Quaternion.LookRotation(direction, up),
            Object.InputAuthority
        );

        if (projectileObject == null)
            return false;

        Projectile projectile = projectileObject.GetComponent<Projectile>();

        if (projectile == null)
        {
            Runner.Despawn(projectileObject);
            return false;
        }

        if (isGravityBurst)
        {
            // 重力弾は実際の飛行と衝突でダメージを判定
            projectile.Initialize(
                direction,
                speed,
                projectileLifeTime,
                damage,
                playerHealth,
                true,
                GetProjectileGravityDirection(),
                gravityBurstGravityAcceleration,
                true,
                gravityBurstExplosionRadius,
                damage * gravityBurstExplosionDamageMultiplier
            );
        }
        else
        {
            // 通常弾・ヘビー弾は視覚専用
            projectile.InitializeVisualOnly(
                direction,
                speed,
                projectileLifeTime
            );
        }

        return true;
    }

    private void ApplyHitscan(Vector3 origin, Vector3 direction, float damage)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            raycastMaxDistance,
            hitScanLayerMask,
            QueryTriggerInteraction.Ignore
        );

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform.root == transform.root)
                continue;

            PlayerHealth target =
                hit.collider.GetComponentInParent<PlayerHealth>();

            if (target != null)
                target.TakeDamage(damage);

            // 壁も含め、最初の衝突で終了
            return;
        }
    }

    private Vector3 GetProjectileGravityDirection()
    {
        if (playerLook != null &&
            playerLook.ViewUp.sqrMagnitude > 0.0001f)
        {
            return -playerLook.ViewUp.normalized;
        }

        if (Physics.gravity.sqrMagnitude > 0.0001f)
            return Physics.gravity.normalized;

        return Vector3.down;
    }

    public void ReloadAmmo()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (NetworkedIsReloading || NetworkedCurrentAmmo >= maxAmmo)
            return;

        ClearLoadedSpecialAmmo();

        if (instantReloadEnabled)
        {
            RefillAmmoImmediately();
            return;
        }

        NetworkedIsReloading = true;
        reloadTimer = reloadDuration;

        OnReloadStarted?.Invoke();
        NotifyAmmoChanged();
    }

    public void RefillAmmoImmediately()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;
        NetworkedCurrentAmmo = maxAmmo;

        ClearLoadedSpecialAmmo();
        pendingNextShotDamageMultiplier = 1.0f;

        NotifyAmmoChanged();
    }

    private void CompleteReload()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;
        NetworkedCurrentAmmo = maxAmmo;

        ClearLoadedSpecialAmmo();

        OnReloaded?.Invoke();
        NotifyAmmoChanged();
    }

    public void CancelReload()
    {
        if (Object == null || !Object.HasStateAuthority ||
            !NetworkedIsReloading)
            return;

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;

        OnReloadCanceled?.Invoke();
        NotifyAmmoChanged();
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
            return;

        pendingNextShotDamageMultiplier = Mathf.Clamp(multiplier, 1.0f, 10.0f);
    }

    private void LoadSpecialAmmo(ProjectileShotKind kind, int ammoCount)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        int count = Mathf.Clamp(ammoCount, 1, maxAmmo);

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;

        loadedShotKind = kind;
        loadedSpecialAmmoRemaining = count;
        NetworkedCurrentAmmo = count;

        NotifyAmmoChanged();
    }

    private void ConsumeLoadedSpecialAmmo()
    {
        if (loadedShotKind == ProjectileShotKind.Normal ||
            loadedSpecialAmmoRemaining <= 0)
            return;

        loadedSpecialAmmoRemaining--;

        if (loadedSpecialAmmoRemaining <= 0 ||
            NetworkedCurrentAmmo <= 0)
        {
            ClearLoadedSpecialAmmo();
        }
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

    public void SetInstantReloadEnabled(bool enabled)
    {
        instantReloadEnabled = enabled;

        if (enabled && Object != null &&
            Object.HasStateAuthority && NetworkedIsReloading)
        {
            RefillAmmoImmediately();
        }
    }
}
