using System.Collections.Generic;
using Fusion;
using UnityEngine;

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
    private bool hasImpacted;

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

        PracticeTarget targetDecoy = other.GetComponentInParent<PracticeTarget>();
        if (targetDecoy != null && !targetDecoy.CanReceiveDamage)
        {
            targetDecoy = null;
        }

        bool canDamagePlayers =
            RoundManager.Instance == null ||
            RoundManager.Instance.IsRoundPlaying;

        if (explodeOnHit)
        {
            bool hitByExplosion = ApplyExplosionDamage(transform.position, canDamagePlayers); // 変更（戻り値を受け取る）
            bool hitEnemy = 
                (canDamagePlayers && targetHealth != null && !targetHealth.IsDead) ||
                (targetDecoy != null) ||
                hitByExplosion; // 変更
            NotifyOwnerProjectileHit(hitEnemy, transform.position); // 追加
            Runner.Despawn(Object);
            return;
        }

        bool isEnemyHit = false;    //変更
        // プレイヤーへのダメージ（戦闘中のみ有効）
        if (canDamagePlayers && targetHealth != null && !targetHealth.IsDead)
        {
            targetHealth.TakeDamage(damage);
            isEnemyHit = true;
        }

        // 追加：練習用ターゲット（デコイ）へのダメージ
        else if (targetDecoy != null)
        {
            targetDecoy.TakeDamage(damage);
            isEnemyHit = true;
        }

        NotifyOwnerProjectileHit(isEnemyHit, transform.position); // 追加
        Runner.Despawn(Object);
    }

    private bool ApplyExplosionDamage(Vector3 explosionPosition, bool canDamagePlayers)
    {
        if (explosionRadius <= 0.0f || explosionDamage <= 0.0f)
        {
            return false;   //変更
        }

        Collider[] hitColliders = Physics.OverlapSphere(
            explosionPosition,
            explosionRadius,
            ~0,
            QueryTriggerInteraction.Collide
        );

        HashSet<PlayerHealth> damagedPlayers = new();
        HashSet<PracticeTarget> damagedDecoys = new();

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider == null)
            {
                continue;
            }

            if (canDamagePlayers)
            {
                PlayerHealth targetHealth = hitCollider.GetComponentInParent<PlayerHealth>();
                if (targetHealth != null && targetHealth != owner && !targetHealth.IsDead)
                {
                    if (damagedPlayers.Add(targetHealth))
                    {
                        targetHealth.TakeDamage(explosionDamage);
                    }
                }
            }

            // ★追加：爆発範囲内の練習用ターゲットにもダメージを与える
            PracticeTarget targetDecoy = hitCollider.GetComponentInParent<PracticeTarget>();
            if (targetDecoy != null && targetDecoy.CanReceiveDamage)
            {
                if (damagedDecoys.Add(targetDecoy))
                {
                    targetDecoy.TakeDamage(explosionDamage);
                }
            }
        }
        return damagedPlayers.Count > 0 || damagedDecoys.Count > 0; //変更
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

    // 追加
    private void NotifyOwnerProjectileHit(bool hitEnemy, Vector3 hitPosition)
    {
        if (owner == null)
        {
            return;
        }

        PlayerSound ownerSound = owner.GetComponent<PlayerSound>();
        if (ownerSound != null)
        {
            ownerSound.NotifyProjectileHitFromServer(hitEnemy, hitPosition);
        }
    }
}