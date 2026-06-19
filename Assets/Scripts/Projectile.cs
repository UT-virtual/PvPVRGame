using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class Projectile : NetworkBehaviour
{
    private Vector3 moveDirection;
    private float moveSpeed;
    private float lifeTime;
    private float timer;
    private int damage;

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
        int projectileDamage,
        PlayerHealth projectileOwner)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        moveDirection = direction.normalized;
        moveSpeed = speed;
        lifeTime = duration;
        damage = projectileDamage;
        owner = projectileOwner;

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

        transform.position += moveDirection * moveSpeed * Runner.DeltaTime;

        timer += Runner.DeltaTime;

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

        if (targetHealth == null)
        {
            return;
        }

        if (targetHealth == owner)
        {
            return;
        }

        targetHealth.TakeDamage(damage);

        Runner.Despawn(Object);
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