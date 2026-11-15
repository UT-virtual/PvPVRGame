using System;
using Fusion;
using UnityEngine;

/// <summary>
/// StateAuthorityで射撃間隔、逐次装填、弾薬消費と即時命中判定を管理する。
/// 射撃時のレーザーと着弾演出はRPCで全端末へ通知する。
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerLook))]
[RequireComponent(typeof(PlayerCamera))]
public class PlayerWeapon : NetworkBehaviour
{
    private enum ProjectileShotKind
    {
        Normal,
        Heavy
    }

    [Header("Shoot")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float projectileSpeed = 18.0f;
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
    public Transform MuzzleTransform => muzzleTransform;
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

    private float GetShotSpeedMultiplier(ProjectileShotKind kind)
    {
        switch (kind)
        {
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
    /// 通常弾・ヘビー弾は即時命中判定し、射撃の軌跡だけを全端末に通知する。
    /// </summary>
    private bool FireProjectile(ProjectileShotKind shotKind)
    {
        PlayerController controller = GetComponent<PlayerController>();
        bool isVrPlayer = controller != null && controller.IsCurrentInputVR;
        Ray aim = GetAimRay(isVrPlayer, out Vector3 aimUp);
        float exitOffset = isVrPlayer && muzzleTransform != null ? muzzleExitOffset : projectileSpawnDistance;
        RaycastHit aimHit = default;
        if (TryGetAimHit(aim, out aimHit))
            exitOffset = Mathf.Min(exitOffset, Mathf.Max(0f, aimHit.distance - 0.01f));
        float currentDamage =
            projectileDamage *
            damageDealtMultiplier *
            GetShotDamageMultiplier(shotKind) *
            ConsumeNextShotDamageMultiplier();

        Vector3 endpoint = aimHit.collider != null ? aimHit.point : aim.GetPoint(raycastMaxDistance);
        Vector3 muzzle = muzzleTransform != null ? muzzleTransform.position : aim.GetPoint(exitOffset);
        RPC_ShowShotEffects(muzzle, endpoint, Quaternion.LookRotation(aim.direction, aimUp));

        if (aimHit.collider != null) ApplyHitscan(aimHit, currentDamage);

        return true;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowShotEffects(Vector3 origin, Vector3 endpoint, Quaternion rotation)
    {
        // 弾の子にあった光だけを複製する。弾本体や通信・衝突コンポーネントは生成しない。
        if (muzzleFlashPrefab != null)
        {
            Vector3 position = muzzleTransform != null ? muzzleTransform.position : origin;
            GameObject flash = Instantiate(muzzleFlashPrefab, position, rotation);
            flash.transform.localScale = muzzleFlashPrefab.transform.lossyScale;
            ParticleSystem particles = flash.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                ParticleSystem.MainModule main = particles.main;
                main.loop = false;
                main.stopAction = ParticleSystemStopAction.Destroy;
                particles.Play(true);
            }
            else
            {
                Destroy(flash, 2f);
            }
        }

        // 本人は常時の照準ガイドを使う。観戦端末の除外はGuide側で行う。
        if (Object.HasInputAuthority) return;

        PlayerAimGuide guide = GetComponent<PlayerAimGuide>();
        if (guide == null) guide = gameObject.AddComponent<PlayerAimGuide>();
        guide.ShowShot(origin, endpoint);
    }

    private Ray GetAimRay(bool vr, out Vector3 up)
    {
        if (vr && muzzleTransform != null)
        {
            up = muzzleTransform.up;
            return new Ray(muzzleTransform.position, muzzleTransform.forward);
        }

        up = playerLook.ViewUp;
        return new Ray(playerCamera.CameraPosition, playerLook.ViewForward);
    }

    public Vector3 GetAimEndpoint(bool vr, out bool isTarget)
    {
        Ray aim = GetAimRay(vr, out _);
        // PCの代理PlayerLookには入力が来ないため、観戦用に同期済みの視点を使う。
        PlayerController controller = GetComponent<PlayerController>();
        if (!vr && Object != null && Object.IsValid && !Object.HasInputAuthority &&
            !Object.HasStateAuthority && controller != null && controller.HasSpectatorView)
        {
            aim = new Ray(controller.NetworkedSpectatorCameraPosition, controller.NetworkedSpectatorViewForward);
        }
        isTarget = false;
        if (!TryGetAimHit(aim, out RaycastHit hit))
            return aim.GetPoint(raycastMaxDistance);

        PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            isTarget = player.Object != null && player.Object.IsValid && !player.IsDead;
        }
        else
        {
            PracticeTarget practiceTarget = hit.collider.GetComponentInParent<PracticeTarget>();
            isTarget = practiceTarget != null && practiceTarget.Object != null &&
                practiceTarget.Object.IsValid && practiceTarget.CanReceiveDamage;
        }
        return hit.point;
    }

    private readonly RaycastHit[] aimHits = new RaycastHit[16];

    private bool TryGetAimHit(Ray aim, out RaycastHit nearest)
    {
        int count = Physics.RaycastNonAlloc(aim, aimHits, raycastMaxDistance, hitScanLayerMask, QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = aimHits;
        if (count == aimHits.Length)
        {
            // A full buffer does not guarantee that it contains the closest hits.
            hits = Physics.RaycastAll(aim, raycastMaxDistance, hitScanLayerMask, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        nearest = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearestDistance)
                continue;

            nearest = hit;
            nearestDistance = hit.distance;
        }

        return nearest.collider != null;
    }

    private void ApplyHitscan(RaycastHit hit, float damage)
    {
        PlayerHealth targetHealth = hit.collider.GetComponentInParent<PlayerHealth>();
        bool isEnemyHit = targetHealth != null && !targetHealth.IsDead;

        if (targetHealth != null)
        {
            // ハプニング・サドンデス倍率と実効与ダメージの確定はHP側で統一し、特殊弾と二重適用しない。
            targetHealth.TakeDamage(damage, Object.InputAuthority,
                RoundManager.Instance != null ? RoundManager.Instance.DamageRoundVersion : 0);
        }
        else
        {
            // 待機場の練習ターゲットも即時命中判定で扱い、見た目用の弾には加害させない。
            PracticeTarget practiceTarget = hit.collider.GetComponentInParent<PracticeTarget>();
            if (practiceTarget != null && practiceTarget.CanReceiveDamage)
            {
                practiceTarget.TakeDamage(damage);
                isEnemyHit = true;
            }
        }

        NotifyProjectileImpactEffect(hit.point);

        PlayerSound playerSound = GetComponent<PlayerSound>();
        if (playerSound != null)
        {
            playerSound.NotifyProjectileHitFromServer(isEnemyHit, hit.point);
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
