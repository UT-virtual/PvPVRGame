using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Vector3 moveDirection;
    private float moveSpeed;
    private float lifeTime;
    private float timer;
    private int damage;

    private PlayerHealth owner;
    private bool initialized;

    public void Initialize(
        Vector3 direction,
        float speed,
        float duration,
        int projectileDamage,
        PlayerHealth projectileOwner)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        lifeTime = duration;
        damage = projectileDamage;
        owner = projectileOwner;

        timer = 0.0f;
        initialized = true;

        SetupCollision();
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        timer += Time.deltaTime;

        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
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

        Destroy(gameObject);
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