using System;
using Fusion;
using UnityEngine;

/// <summary>
/// StateAuthorityで射撃間隔、逐次装填、弾薬消費とProjectile生成を管理する。
/// 通常弾・ヘビー弾は即時命中判定、重力弾はProjectile側で衝突判定を行う。
/// </summary>
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
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float projectileSpeed = 18.0f;
    [SerializeField] private float projectileLifeTime = 3.0f;
    [SerializeField] private float projectileDamage = 1.0f;
    [SerializeField] private float projectileSpawnDistance = 0.8f;
    [SerializeField] private float raycastMaxDistance = 100.0f;
    [SerializeField] private LayerMask hitScanLayerMask = Physics.DefaultRaycastLayers;

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
    private int reloadStartAmmo;

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

    /// <summary>
    /// PlayerControllerのネットワークTickから呼び、射撃待ちと装填の進行を更新する。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (Object == null || !Object.HasStateAuthority)
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

        reloadTimer += deltaTime;

        // 装填時間の前半は待機し、後半で弾薬を1発ずつ補充する。
        float reloadWait = Mathf.Max(0.0f, reloadDuration) * 0.5f;
        if (reloadTimer < reloadWait)
        {
            return;
        }

        int missingAmmo = maxAmmo - reloadStartAmmo;
        float progress = reloadWait > 0.0f
            ? Mathf.Clamp01((reloadTimer - reloadWait) / reloadWait)
            : 1.0f;

        int restoredAmmo = 1 + Mathf.FloorToInt(Mathf.Max(0, missingAmmo - 1) * progress);
        int nextAmmo = Mathf.Min(maxAmmo, reloadStartAmmo + restoredAmmo);
        if (nextAmmo != NetworkedCurrentAmmo)
        {
            NetworkedCurrentAmmo = nextAmmo;
            NotifyAmmoChanged();
        }

        if (NetworkedCurrentAmmo >= maxAmmo)
        {
            CompleteReload();
        }
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
        float currentInterval = fireInterval * fireIntervalMultiplier;

        if (GetCurrentShotKind() == ProjectileShotKind.Heavy)
        {
            currentInterval *= heavyBulletFireIntervalMultiplier;
        }

        return currentInterval;
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

    /// <summary>
    /// 通常弾の逐次装填を開始する。待機後は途中から射撃できる。
    /// </summary>
    public void ReloadAmmo()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (NetworkedIsReloading || NetworkedCurrentAmmo >= maxAmmo)
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

        NetworkedIsReloading = true;
        reloadTimer = 0.0f;
        reloadStartAmmo = NetworkedCurrentAmmo;

        OnReloadStarted?.Invoke();
        NotifyAmmoChanged();

        Debug.Log($"Reload started. Duration={reloadDuration} seconds.");
    }

    /// <summary>
    /// 即時装填では進行中の装填と特殊弾・未消費の次弾強化を解除する。
    /// </summary>
    public void RefillAmmoImmediately()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        CancelReload();
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
        if (Object == null || !Object.HasStateAuthority)
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
        if (Object == null || !Object.HasStateAuthority || !NetworkedIsReloading)
        {
            return;
        }

        NetworkedIsReloading = false;
        reloadTimer = 0.0f;

        OnReloadCanceled?.Invoke();
        NotifyAmmoChanged();

        Debug.Log("Reload canceled.");
    }

    /// <summary>
    /// 発射成功時だけ残弾・特殊弾を消費する。
    /// 逐次装填中に撃った場合は装填を終了し、弾切れ時は自動で再開する。
    /// </summary>
    public void TryFireProjectile()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (fireTimer > 0.0f)
        {
            return;
        }

        float currentFireInterval = GetCurrentFireInterval();

        if (NetworkedCurrentAmmo <= 0)
        {
            if (!NetworkedIsReloading)
            {
                OnDryFire?.Invoke();
            }

            ReloadAmmo();
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

        if (NetworkedIsReloading)
        {
            CancelReload();
        }

        OnShot?.Invoke();
        NotifyAmmoChanged();

        if (NetworkedCurrentAmmo <= 0)
        {
            ReloadAmmo();
        }

        Debug.Log(
            $"Ammo: {NetworkedCurrentAmmo}/{maxAmmo}, " +
            $"FireInterval={currentFireInterval:0.00}, " +
            $"FireIntervalMultiplier={fireIntervalMultiplier:0.00}, " +
            $"ProjectileDamage={projectileDamage:0.00}, " +
            $"ProjectileSpeed={GetCurrentProjectileSpeed():0.00}, " +
            $"ProjectileSpeedMultiplier={projectileSpeedMultiplier:0.00}"
        );
    }

    /// <summary>
    /// 発射用Prefabが生成できた後にダメージ強化を消費する。
    /// 通常弾・ヘビー弾はRaycastで即時命中判定し、生成弾は視覚専用にする。
    /// </summary>
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
            fireDirection = playerLook.ViewForward.normalized;
            spawnPosition = playerCamera.CameraPosition + fireDirection * projectileSpawnDistance;
            spawnRotation = Quaternion.LookRotation(fireDirection, playerLook.ViewUp);
        }

        NetworkSpawnStatus spawnStatus = Runner.TrySpawn(
            projectilePrefab,
            out NetworkObject projectileObject,
            spawnPosition,
            spawnRotation,
            Object.InputAuthority
        );

        if (spawnStatus != NetworkSpawnStatus.Spawned || projectileObject == null)
        {
            Debug.LogWarning($"{name}: Projectile spawn failed: {spawnStatus}");
            return false;
        }

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile == null)
        {
            Runner.Despawn(projectileObject);
            return false;
        }

        float currentDamage =
            projectileDamage *
            damageDealtMultiplier *
            GetShotDamageMultiplier(shotKind) *
            ConsumeNextShotDamageMultiplier();

        float currentSpeed =
            projectileSpeed *
            projectileSpeedMultiplier *
            GetShotSpeedMultiplier(shotKind);

        bool isGravityBurst = shotKind == ProjectileShotKind.GravityBurst;
        if (isGravityBurst)
        {
            projectile.Initialize(
                fireDirection,
                currentSpeed,
                projectileLifeTime,
                currentDamage,
                playerHealth,
                true,
                GetProjectileGravityDirection(),
                gravityBurstGravityAcceleration,
                true,
                gravityBurstExplosionRadius,
                currentDamage * gravityBurstExplosionDamageMultiplier
            );
        }
        else
        {
            projectile.InitializeVisualOnly(
                fireDirection,
                currentSpeed,
                projectileLifeTime,
                playerHealth
            );

            ApplyHitscan(spawnPosition, fireDirection, currentDamage);
        }

        return true;
    }

    /// <summary>
    /// 最初の非自身Colliderで止める。通常弾・ヘビー弾の着弾SEはここで通知する。
    /// </summary>
    private void ApplyHitscan(Vector3 origin, Vector3 direction, float damage)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            raycastMaxDistance,
            hitScanLayerMask,
            QueryTriggerInteraction.Ignore
        );

        Array.Sort(hits, (firstHit, secondHit) => firstHit.distance.CompareTo(secondHit.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.root == transform.root)
            {
                continue;
            }

            PlayerHealth targetHealth = hit.collider.GetComponentInParent<PlayerHealth>();
            bool isEnemyHit = targetHealth != null && !targetHealth.IsDead;

            if (targetHealth != null)
            {
                // ハプニング・サドンデス倍率と実効与ダメージの確定はHP側で統一し、特殊弾と二重適用しない。
                targetHealth.TakeDamage(damage, Object.InputAuthority,
                    RoundManager.Instance != null ? RoundManager.Instance.DamageRoundVersion : 0);
            }

            NotifyProjectileImpactEffect(hit.point);

            PlayerSound playerSound = GetComponent<PlayerSound>();
            if (playerSound != null)
            {
                playerSound.NotifyProjectileHitFromServer(isEnemyHit, hit.point);
            }

            return;
        }
    }

    /// <summary>
    /// StateAuthorityで確定した着弾位置を全端末へ通知する。弾の寿命切れやDespawnでは呼ばない。
    /// </summary>
    public void NotifyProjectileImpactEffect(Vector3 position)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        RPC_ShowHitEffect(position);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowHitEffect(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            // 弾の最終TransformがClientへ届く前にDespawnされても、通知された命中地点で再生する。
            Instantiate(hitEffectPrefab, position, Quaternion.identity);
        }
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

    /// <summary>
    /// 次弾強化は発射成功まで保持し、即時装填でも解除する。
    /// </summary>
    public void SetNextShotDamageMultiplier(float multiplier)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        pendingNextShotDamageMultiplier = Mathf.Clamp(multiplier, 1.0f, 10.0f);
    }

    /// <summary>
    /// 特殊弾の補充は通常弾への追加ではなく、残弾と弾種の置き換えとする。
    /// </summary>
    private void LoadSpecialAmmo(ProjectileShotKind shotKind, int ammoCount)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        int clampedAmmoCount = Mathf.Clamp(ammoCount, 1, maxAmmo);

        CancelReload();
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
        float damageMultiplier = pendingNextShotDamageMultiplier;
        pendingNextShotDamageMultiplier = 1.0f;
        return damageMultiplier;
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
