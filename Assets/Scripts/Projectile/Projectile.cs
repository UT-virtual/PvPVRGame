
using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class Projectile : NetworkBehaviour
{
    private Vector3 velocity;

    private float lifeTime;
    private float timer;
    private float damage;

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
        if (Object == null || !Object.HasStateAuthority)
            return;

        visualOnly = false;
        hasImpacted = false;

        velocity = direction.normalized * speed;
        lifeTime = duration;
        timer = 0.0f;
        damage = projectileDamage;
        owner = projectileOwner;

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

        initialized = true;
        SetupCollision();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object == null || !Object.HasStateAuthority ||
            !initialized || hasImpacted)
            return;

        float dt = Runner.DeltaTime;

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

                transform.position = hit.point;
                HandleImpact(hit.point);
                return;
            }
        }

        transform.position = start + displacement;

        if (velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(velocity.normalized);

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

        // 見た目専用弾は衝突時に消える。ダメージは与えない。
        if (visualOnly)
        {
            Runner.Despawn(Object);
            return;
        }

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

        Runner.Despawn(Object);
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
