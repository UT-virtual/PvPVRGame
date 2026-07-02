using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class HealthItemSpawner : MonoBehaviour
{
    [Header("Health Item")]
    [SerializeField] private NetworkPrefabRef healthItemPrefab;
    [SerializeField] private int healAmount = 3;
    [SerializeField] private float respawnDelay = 10.0f;

    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPointsRoot;
    [SerializeField] private List<Transform> spawnPoints = new();
    [SerializeField] private bool shuffleSpawnPointsOnRoundStart = false;

    [Header("Surface Align")]
    [SerializeField] private Transform planetCenter;
    [SerializeField] private bool alignToPlanetSurface = true;

    private readonly Dictionary<int, NetworkObject> spawnedItems = new();
    private readonly Dictionary<int, Coroutine> respawnCoroutines = new();

    private int currentItemCount;

    public void SetupItemsForPlayerCount(int playerCount)
    {
        ClearAllItems();

        if (!healthItemPrefab.IsValid)
        {
            Debug.LogError("[HealthItemSpawner] Health Item Prefab is not assigned.");
            return;
        }

        List<Transform> availableSpawnPoints = GetAvailableSpawnPoints();

        if (availableSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[HealthItemSpawner] No health item spawn points assigned.");
            return;
        }

        int itemCount = Mathf.Max(1, playerCount / 2);
        itemCount = Mathf.Min(itemCount, availableSpawnPoints.Count);

        currentItemCount = itemCount;

        Debug.Log(
            $"[HealthItemSpawner] Setup. " +
            $"PlayerCount={playerCount}, ItemCount={itemCount}, SpawnPoints={availableSpawnPoints.Count}"
        );

        List<int> spawnIndices = Enumerable.Range(0, availableSpawnPoints.Count).ToList();

        if (shuffleSpawnPointsOnRoundStart)
        {
            Shuffle(spawnIndices);
        }

        for (int i = 0; i < itemCount; i++)
        {
            int spawnIndex = spawnIndices[i];
            SpawnItemAtIndex(spawnIndex);
        }
    }

    public void NotifyItemPickedUp(int spawnIndex)
    {
        if (spawnedItems.ContainsKey(spawnIndex))
        {
            spawnedItems.Remove(spawnIndex);
        }

        if (respawnCoroutines.ContainsKey(spawnIndex))
        {
            StopCoroutine(respawnCoroutines[spawnIndex]);
            respawnCoroutines.Remove(spawnIndex);
        }

        Coroutine coroutine = StartCoroutine(RespawnItemAfterDelay(spawnIndex));
        respawnCoroutines[spawnIndex] = coroutine;
    }

    public void ClearAllItems()
    {
        foreach (Coroutine coroutine in respawnCoroutines.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }

        respawnCoroutines.Clear();

        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

        foreach (NetworkObject itemObject in spawnedItems.Values)
        {
            if (itemObject == null)
            {
                continue;
            }

            if (runner != null && runner.IsServer)
            {
                runner.Despawn(itemObject);
            }
        }

        spawnedItems.Clear();
        currentItemCount = 0;
    }

    private IEnumerator RespawnItemAfterDelay(int spawnIndex)
    {
        yield return new WaitForSeconds(respawnDelay);

        respawnCoroutines.Remove(spawnIndex);

        if (RoundManager.Instance != null && !RoundManager.Instance.CanUseWeapons)
        {
            yield break;
        }

        if (spawnedItems.ContainsKey(spawnIndex))
        {
            yield break;
        }

        SpawnItemAtIndex(spawnIndex);
    }

    private void SpawnItemAtIndex(int spawnIndex)
    {
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

        if (runner == null)
        {
            Debug.LogWarning("[HealthItemSpawner] NetworkRunner was not found.");
            return;
        }

        if (!runner.IsServer)
        {
            return;
        }

        List<Transform> availableSpawnPoints = GetAvailableSpawnPoints();

        if (spawnIndex < 0 || spawnIndex >= availableSpawnPoints.Count)
        {
            Debug.LogWarning($"[HealthItemSpawner] Invalid spawn index: {spawnIndex}");
            return;
        }

        Transform spawnPoint = availableSpawnPoints[spawnIndex];

        Quaternion spawnRotation = GetSurfaceAlignedRotation(spawnPoint);

        NetworkObject itemObject = runner.Spawn(
            healthItemPrefab,
            spawnPoint.position,
            spawnRotation,
            PlayerRef.None
        );

        HealthItem healthItem = itemObject.GetComponent<HealthItem>();

        if (healthItem == null)
        {
            Debug.LogError("[HealthItemSpawner] Spawned item does not have HealthItem component.");
            runner.Despawn(itemObject);
            return;
        }

        healthItem.Initialize(this, spawnIndex, healAmount);

        spawnedItems[spawnIndex] = itemObject;

        Debug.Log(
            $"[HealthItemSpawner] Spawned item. " +
            $"Index={spawnIndex}, " +
            $"SpawnPoint={spawnPoint.name}, " +
            $"Position={spawnPoint.position}, " +
            $"Rotation={spawnRotation.eulerAngles}, " +
            $"HealAmount={healAmount}"
        );
    }

    private Quaternion GetSurfaceAlignedRotation(Transform spawnPoint)
    {
        if (spawnPoint == null)
        {
            return Quaternion.identity;
        }

        if (!alignToPlanetSurface || planetCenter == null)
        {
            return spawnPoint.rotation;
        }

        Vector3 surfaceUp = spawnPoint.position - planetCenter.position;

        if (surfaceUp.sqrMagnitude < 0.001f)
        {
            surfaceUp = spawnPoint.up;
        }

        surfaceUp.Normalize();

        Vector3 forward = Vector3.ProjectOnPlane(spawnPoint.forward, surfaceUp);

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.forward, surfaceUp);
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.Cross(Vector3.right, surfaceUp);
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.Cross(Vector3.up, surfaceUp);
        }

        forward.Normalize();

        return Quaternion.LookRotation(forward, surfaceUp);
    }

    private List<Transform> GetAvailableSpawnPoints()
    {
        List<Transform> result = spawnPoints
            .Where(spawnPoint => spawnPoint != null)
            .Distinct()
            .ToList();

        if (result.Count > 0)
        {
            return result;
        }

        if (spawnPointsRoot == null)
        {
            return result;
        }

        for (int i = 0; i < spawnPointsRoot.childCount; i++)
        {
            Transform child = spawnPointsRoot.GetChild(i);

            if (child != null)
            {
                result.Add(child);
            }
        }

        return result
            .Where(spawnPoint => spawnPoint != null)
            .Distinct()
            .ToList();
    }

    private void Shuffle(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            int temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}