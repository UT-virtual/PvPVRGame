using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// StateAuthorityで弾の移動・寿命・衝突ダメージを処理する。
/// 通常弾と重力・爆発弾を扱い、着弾時は発射者へSE通知を送り、物理弾の着弾エフェクトも通知する。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class Projectile : NetworkBehaviour
{
    private Vector3 velocity;
    private Vector3 gravityDirection = Vector3.down;

    private float lifeTime;
    private float timer;
    private float damage;
    private float gravityAcceleration;
    private float explosionRadius;
    private float explosionDamage;

    private bool useGravity;
    private bool explodeOnHit;
    private bool initialized;
    private bool visualOnly;
    private bool hasImpacted;

    private PlayerHealth owner;
    private PlayerRef attacker;
    private int damageRoundVersion;

    public override void Spawned()
    {
        SetupCollision();
    }

    public void Initialize(
        Vector3 direction,
        float speed,
        float duration,
        float projectileDamage,
        PlayerHealth projectileOwner)
    {
        Initialize(
            direction,
            speed,
            duration,
            projectileDamage,
            projectileOwner,
            false,
            Vector3.down,
            0.0f,
            false,
            0.0f,
            0.0f
        );
    }

    /// <summary>
    /// Spawn後にStateAuthority側で設定する。弾道を進めるのもStateAuthorityだけとする。
    /// </summary>
    public void Initialize(
        Vector3 direction,
        float speed,
        float duration,
        float projectileDamage,
        PlayerHealth projectileOwner,
        bool projectileUsesGravity,
        Vector3 projectileGravityDirection,
        float projectileGravityAcceleration,
        bool projectileExplodesOnHit,
        float projectileExplosionRadius,
        float projectileExplosionDamage)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        visualOnly = false;
        hasImpacted = false;
        velocity = direction.normalized * speed;
        lifeTime = duration;
        timer = 0.0f;
        damage = projectileDamage;
        owner = projectileOwner;
        attacker = owner != null && owner.Object != null ? owner.Object.InputAuthority : PlayerRef.None;
        // 発射時の世代を保持し、退出・死亡後も攻撃者を特定する。次ラウンドのHPには命中させない。
        damageRoundVersion = RoundManager.Instance != null && RoundManager.Instance.IsNetworkReady
            ? RoundManager.Instance.DamageRoundVersion : 0;

        useGravity = projectileUsesGravity;
        gravityDirection = projectileGravityDirection.sqrMagnitude > 0.0001f
            ? projectileGravityDirection.normalized
            : Vector3.down;
        gravityAcceleration = Mathf.Max(0.0f, projectileGravityAcceleration);

        explodeOnHit = projectileExplodesOnHit;
        explosionRadius = Mathf.Max(0.0f, projectileExplosionRadius);
        explosionDamage = Mathf.Max(0.0f, projectileExplosionDamage);

        initialized = true;
        SetupCollision();
    }

    /// <summary>
    /// 通常弾・ヘビー弾の見た目だけを移動する。発射者を衝突対象から除外し、ダメージや着弾演出はRaycast側に任せる。
    /// </summary>
    public void InitializeVisualOnly(Vector3 direction, float speed, float duration, PlayerHealth projectileOwner)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        visualOnly = true;
        hasImpacted = false;
        velocity = direction.normalized * speed;
        lifeTime = duration;
        timer = 0.0f;

        owner = projectileOwner;
        damage = 0.0f;
        useGravity = false;
        gravityDirection = Vector3.down;
        gravityAcceleration = 0.0f;
        explodeOnHit = false;
        explosionRadius = 0.0f;
        explosionDamage = 0.0f;

        initialized = true;
        SetupCollision();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object == null || !Object.HasStateAuthority || !initialized || hasImpacted)
        {
            return;
        }

        float deltaTime = Runner.DeltaTime;

        if (useGravity)
        {
            velocity += gravityDirection * gravityAcceleration * deltaTime;
        }

        Vector3 startPosition = transform.position;
        Vector3 displacement = velocity * deltaTime;
        float distance = displacement.magnitude;

        if (distance > 0.0001f)
        {
            Vector3 direction = displacement / distance;
            float radius = GetWorldSphereRadius();

            RaycastHit[] hits = Physics.SphereCastAll(
                startPosition,
                radius,
                direction,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                if (hit.collider.transform.root == transform.root)
                {
                    continue;
                }

                PlayerHealth target = hit.collider.GetComponentInParent<PlayerHealth>();
                if (target != null && target == owner)
                {
                    continue;
                }

                transform.position = hit.point;
                HandleImpact(hit.point);
                return;
            }
        }

        transform.position = startPosition + displacement;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = velocity.normalized;
            Vector3 up = -gravityDirection;

            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            {
                up = Vector3.up;
            }

            transform.rotation = Quaternion.LookRotation(forward, up);
        }

        timer += deltaTime;
        if (timer >= lifeTime)
        {
            Runner.Despawn(Object);
        }
    }

    private float GetWorldSphereRadius()
    {
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            return 0.05f;
        }

        Vector3 scale = transform.lossyScale;
        return sphereCollider.radius * Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object == null || !Object.HasStateAuthority || !initialized || hasImpacted || other == null)
        {
            return;
        }

        if (other.transform.root == transform.root)
        {
            return;
        }

        PlayerHealth target = other.GetComponentInParent<PlayerHealth>();
        if (target != null && target == owner)
        {
            return;
        }

        HandleImpact(transform.position);
    }

    private void HandleImpact(Vector3 impactPosition)
    {
        if (Object == null || !Object.HasStateAuthority || !initialized || hasImpacted)
        {
            return;
        }

        // SphereCastとTriggerが同じ着弾を検出しても、一度しかダメージ・SEを処理しない。
        hasImpacted = true;

        if (visualOnly)
        {
            Runner.Despawn(Object);
            return;
        }

        if (owner != null)
        {
            PlayerWeapon weapon = owner.GetComponent<PlayerWeapon>();
            if (weapon != null)
            {
                weapon.NotifyProjectileImpactEffect(impactPosition);
            }
        }

        bool hasHitAliveEnemy;
        if (explodeOnHit)
        {
            hasHitAliveEnemy = ApplyExplosionDamage(impactPosition);
        }
        else
        {
            hasHitAliveEnemy = ApplyDirectHitDamage(impactPosition);
        }

        NotifyOwnerProjectileHit(hasHitAliveEnemy, impactPosition);
        Runner.Despawn(Object);
    }

    private bool ApplyDirectHitDamage(Vector3 impactPosition)
    {
        Collider[] colliders = Physics.OverlapSphere(
            impactPosition,
            GetWorldSphereRadius(),
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        HashSet<PlayerHealth> damagedTargets = new();
        bool hasHitAliveEnemy = false;

        foreach (Collider hitCollider in colliders)
        {
            if (hitCollider == null)
            {
                continue;
            }

            PlayerHealth target = hitCollider.GetComponentInParent<PlayerHealth>();
            if (target == null || target == owner || !damagedTargets.Add(target))
            {
                continue;
            }

            if (!target.IsDead)
            {
                hasHitAliveEnemy = true;
            }

            target.TakeDamage(damage, attacker, damageRoundVersion);
        }

        return ApplyPracticeTargetDamage(colliders, damage) || hasHitAliveEnemy;
    }

    private bool ApplyExplosionDamage(Vector3 impactPosition)
    {
        if (explosionRadius <= 0.0f || explosionDamage <= 0.0f)
        {
            return false;
        }

        Collider[] colliders = Physics.OverlapSphere(
            impactPosition,
            explosionRadius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        HashSet<PlayerHealth> damagedTargets = new();
        bool hasHitAliveEnemy = false;

        foreach (Collider hitCollider in colliders)
        {
            if (hitCollider == null)
            {
                continue;
            }

            PlayerHealth target = hitCollider.GetComponentInParent<PlayerHealth>();
            if (target == null || target == owner || !damagedTargets.Add(target))
            {
                continue;
            }

            if (!target.IsDead)
            {
                hasHitAliveEnemy = true;
            }

            target.TakeDamage(explosionDamage, attacker, damageRoundVersion);
        }

        return ApplyPracticeTargetDamage(colliders, explosionDamage) || hasHitAliveEnemy;
    }

    private bool ApplyPracticeTargetDamage(Collider[] colliders, float targetDamage)
    {
        // 複数Colliderを持つ練習ターゲットも、一度の着弾につき一回だけ加害する。
        HashSet<PracticeTarget> damagedTargets = new();
        foreach (Collider hitCollider in colliders)
        {
            if (hitCollider == null) continue;

            PracticeTarget target = hitCollider.GetComponentInParent<PracticeTarget>();
            if (target == null || !target.CanReceiveDamage || !damagedTargets.Add(target)) continue;

            target.TakeDamage(targetDamage);
        }

        return damagedTargets.Count > 0;
    }

    private void SetupCollision()
    {
        Collider projectileCollider = GetComponent<Collider>();
        if (projectileCollider == null)
        {
            projectileCollider = gameObject.AddComponent<SphereCollider>();
        }

        projectileCollider.isTrigger = true;

        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void NotifyOwnerProjectileHit(bool hasHitAliveEnemy, Vector3 hitPosition)
    {
        if (owner == null)
        {
            return;
        }

        PlayerSound ownerSound = owner.GetComponent<PlayerSound>();
        if (ownerSound != null)
        {
            ownerSound.NotifyProjectileHitFromServer(hasHitAliveEnemy, hitPosition);
        }
    }
}
