using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// StateAuthorityで弾の移動・寿命・衝突ダメージを処理する。
/// 通常の直進弾に加え、初期化時に指定された固定方向への重力と着弾時の範囲ダメージを扱う。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class Projectile : NetworkBehaviour
{
    private Vector3 moveDirection;
    private Vector3 velocity;
    private Vector3 gravityDirection = Vector3.down;

    private float moveSpeed;
    private float lifeTime;
    private float timer;
    private float damage;

    private float gravityAcceleration;
    private bool useGravity;

    private bool explodeOnHit;
    private float explosionRadius;
    private float explosionDamage;

    private PlayerHealth owner;
    private bool initialized;

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
    /// Spawn後、StateAuthorityで速度・寿命・所有者・重力・爆発設定を初期化する。
    /// これらの通常フィールドは同期せず、他端末で同じ弾道計算を実行する入力には使わない。
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
        if (!Object.HasStateAuthority)
        {
            return;
        }

        moveDirection = direction.normalized;
        moveSpeed = speed;
        velocity = moveDirection * moveSpeed;

        lifeTime = duration;
        damage = projectileDamage;
        owner = projectileOwner;

        useGravity = projectileUsesGravity;
        gravityDirection = projectileGravityDirection.sqrMagnitude > 0.0001f
            ? projectileGravityDirection.normalized
            : Vector3.down;
        gravityAcceleration = Mathf.Max(0.0f, projectileGravityAcceleration);

        explodeOnHit = projectileExplodesOnHit;
        explosionRadius = Mathf.Max(0.0f, projectileExplosionRadius);
        explosionDamage = Mathf.Max(0.0f, projectileExplosionDamage);

        timer = 0.0f;
        initialized = true;

        SetupCollision();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (!initialized)
        {
            return;
        }

        float deltaTime = Runner.DeltaTime;

        if (useGravity)
        {
            velocity += gravityDirection * gravityAcceleration * deltaTime;
        }

        transform.position += velocity * deltaTime;

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

    private void OnTriggerEnter(Collider other)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (!initialized)
        {
            return;
        }

        PlayerHealth targetHealth = other.GetComponentInParent<PlayerHealth>();

        if (targetHealth == owner)
        {
            return;
        }

        if (explodeOnHit)
        {
            ApplyExplosionDamage(transform.position);
            Runner.Despawn(Object);
            return;
        }

        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage);
        }

        Runner.Despawn(Object);
    }

    private void ApplyExplosionDamage(Vector3 explosionPosition)
    {
        if (explosionRadius <= 0.0f || explosionDamage <= 0.0f)
        {
            return;
        }

        Collider[] hitColliders = Physics.OverlapSphere(
            explosionPosition,
            explosionRadius,
            ~0,
            QueryTriggerInteraction.Collide
        );

        // 体に複数のColliderがあっても、1回の爆発で同じプレイヤーへ重複して加害しない。
        HashSet<PlayerHealth> damagedTargets = new();

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider == null)
            {
                continue;
            }

            PlayerHealth targetHealth = hitCollider.GetComponentInParent<PlayerHealth>();

            if (targetHealth == null)
            {
                continue;
            }

            if (targetHealth == owner)
            {
                continue;
            }

            if (!damagedTargets.Add(targetHealth))
            {
                continue;
            }

            targetHealth.TakeDamage(explosionDamage);
        }
    }

    private void SetupCollision()
    {
        Collider projectileCollider = GetComponent<Collider>();

        if (projectileCollider == null)
        {
            projectileCollider = gameObject.AddComponent<SphereCollider>();
        }

        projectileCollider.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }
}