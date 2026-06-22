using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class HealthItem : NetworkBehaviour
{
    private HealthItemSpawner spawner;
    private int spawnIndex = -1;
    private int healAmount = 3;
    private bool pickedUp;

    private Collider itemCollider;
    private Rigidbody itemRigidbody;

    private void Awake()
    {
        itemCollider = GetComponent<Collider>();
        itemRigidbody = GetComponent<Rigidbody>();

        if (itemCollider != null)
        {
            itemCollider.isTrigger = true;
        }

        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = true;
            itemRigidbody.useGravity = false;
        }
    }

    public void Initialize(HealthItemSpawner ownerSpawner, int ownerSpawnIndex, int itemHealAmount)
    {
        spawner = ownerSpawner;
        spawnIndex = ownerSpawnIndex;
        healAmount = itemHealAmount;
        pickedUp = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (pickedUp)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
        {
            return;
        }

        bool healed = playerHealth.Heal(healAmount);

        if (!healed)
        {
            return;
        }

        pickedUp = true;

        Debug.Log(
            $"HealthItem picked up by {playerHealth.gameObject.name}. " +
            $"HealAmount={healAmount}, SpawnIndex={spawnIndex}"
        );

        if (spawner != null)
        {
            spawner.NotifyItemPickedUp(spawnIndex);
        }

        Runner.Despawn(Object);
    }
}