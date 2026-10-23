
using System.Collections.Generic;
using Fusion;
using UnityEngine;

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
            direction, speed, duration, projectileDamage,
            projectileOwner, false, Vector3.down, 0.0f,
            false, 0.0f, 0.0f
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
        if (Object == null || !Object.HasStateAuthority)
            return;

        visualOnly = false;
        hasImpacted = false;

        velocity = direction.normalized * speed;
        lifeTime = duration;
        timer = 0.0f;
        damage = projectileDamage;
        owner = projectileOwner;

        useGravity = projectileUsesGravity;

        gravityDirection =
            projectileGravityDirection.sqrMagnitude > 0.0001f
                ? projectileGravityDirection.normalized
                : Vector3.down;

        gravityAcceleration =
            Mathf.Max(0.0f, projectileGravityAcceleration);

        explodeOnHit = projectileExplodesOnHit;
        explosionRadius = Mathf.Max(0.0f, projectileExplosionRadius);
        explosionDamage = Mathf.Max(0.0f, projectileExplosionDamage);

        initialized = true;
        SetupCollision();
    }

    public void InitializeVisualOnly(
        Vector3 direction,
        float speed,
        float duration)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        visualOnly = true;
        hasImpacted = false;

        velocity = direction.normalized * speed;
        lifeTime = duration;
        timer = 0.0f;

        owner = null;
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
        if (Object == null || !Object.HasStateAuthority ||
            !initialized || hasImpacted)
            return;

        float dt = Runner.DeltaTime;

        if (useGravity)
            velocity += gravityDirection * gravityAcceleration * dt;

        Vector3 start = transform.position;
        Vector3 displacement = velocity * dt;
        float distance = displacement.magnitude;

        if (distance > 0.0001f)
        {
            Vector3 direction = displacement / distance;
            float radius = GetWorldSphereRadius();

            RaycastHit[] hits = Physics.SphereCastAll(
                start,
                radius,
                direction,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            System.Array.Sort(
                hits,
                (a, b) => a.distance.CompareTo(b.distance)
            );

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.root == transform.root)
                    continue;

                PlayerHealth target =
                    hit.collider.GetComponentInParent<PlayerHealth>();

                if (target != null && target == owner)
                    continue;

                // 弾丸が衝突した地点で停止・処理
                transform.position = hit.point;
                HandleImpact(hit.point);
                return;
            }
        }

        transform.position = start + displacement;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = velocity.normalized;
            Vector3 up = -gravityDirection;

            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
                up = Vector3.up;

            transform.rotation = Quaternion.LookRotation(forward, up);
        }

        timer += dt;

        if (timer >= lifeTime)
            Runner.Despawn(Object);
    }

    private float GetWorldSphereRadius()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();

        if (sphere == null)
            return 0.05f;

        Vector3 scale = transform.lossyScale;

        return sphere.radius * Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object == null || !Object.HasStateAuthority ||
            !initialized || hasImpacted || other == null)
            return;

        if (other.transform.root == transform.root)
            return;

        PlayerHealth target =
            other.GetComponentInParent<PlayerHealth>();

        if (target != null && target == owner)
            return;

        HandleImpact(transform.position);
    }

    private void HandleImpact(Vector3 impactPosition)
    {
        if (Object == null || !Object.HasStateAuthority ||
            !initialized || hasImpacted)
            return;

        hasImpacted = true;

        // 見た目専用弾はダメージなしで消える
        if (visualOnly)
        {
            Runner.Despawn(Object);
            return;
        }

        if (explodeOnHit)
        {
            ApplyExplosionDamage(impactPosition);
        }
        else
        {
            Collider[] colliders = Physics.OverlapSphere(
                impactPosition,
                GetWorldSphereRadius(),
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            HashSet<PlayerHealth> damaged = new HashSet<PlayerHealth>();

            foreach (Collider col in colliders)
            {
                if (col == null)
                    continue;

                PlayerHealth target =
                    col.GetComponentInParent<PlayerHealth>();

                if (target == null || target == owner ||
                    !damaged.Add(target))
                    continue;

                target.TakeDamage(damage);
            }
        }

        Runner.Despawn(Object);
    }

    private void ApplyExplosionDamage(Vector3 position)
    {
        if (explosionRadius <= 0.0f || explosionDamage <= 0.0f)
            return;

        Collider[] colliders = Physics.OverlapSphere(
            position,
            explosionRadius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        HashSet<PlayerHealth> damaged = new HashSet<PlayerHealth>();

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            PlayerHealth target =
                col.GetComponentInParent<PlayerHealth>();

            if (target == null || target == owner ||
                !damaged.Add(target))
                continue;

            target.TakeDamage(explosionDamage);
        }
    }

    private void SetupCollision()
    {
        Collider col = GetComponent<Collider>();

        if (col == null)
            col = gameObject.AddComponent<SphereCollider>();

        col.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
    }
}
