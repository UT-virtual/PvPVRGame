using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// サーバーで参加者のNetworkObjectを生成し、参加者をInputAuthorityとして割り当てる。
/// 生成したオブジェクトを保持し、退出時のDespawnと識別情報の初期化を担当する。
/// </summary>
public sealed class NetworkPlayerSpawner
{
    private readonly NetworkPrefabRef playerPrefab;
    private readonly bool spawnDebugSpectatorDummies;
    private readonly int debugSpectatorDummyCount;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();
    private readonly List<NetworkObject> debugSpectatorDummies = new();

    private bool debugSpectatorDummiesSpawned;
    private int nextVisualIndex;
    private readonly Dictionary<PlayerRef, int> spawnSlots = new();
    private static readonly float[] InitialSpawnAngles = { 180.0f, 0.0f, 90.0f, 270.0f };

    public NetworkPlayerSpawner(
        NetworkPrefabRef playerPrefab,
        bool spawnDebugSpectatorDummies,
        int debugSpectatorDummyCount
    )
    {
        this.playerPrefab = playerPrefab;
        this.spawnDebugSpectatorDummies = spawnDebugSpectatorDummies;
        this.debugSpectatorDummyCount = debugSpectatorDummyCount;
    }

    /// <summary>
    /// サーバーだけが参加者のプレイヤーを生成する。Spawnのplayer引数をInputAuthorityに割り当てる。
    /// </summary>
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null)
        {
            return;
        }

        Debug.Log($"[NetworkPlayerSpawner] OnPlayerJoined: {player}, IsServer: {runner.IsServer}");

        if (!runner.IsServer)
        {
            return;
        }

        if (!playerPrefab.IsValid)
        {
            Debug.LogWarning("[NetworkPlayerSpawner] Cannot spawn player because Player Prefab is not valid.");
            return;
        }

        if (spawnedPlayers.Count >= NetworkSessionController.MaxPlayerCount || spawnedPlayers.ContainsKey(player))
        {
            return;
        }

        int spawnSlot = 0;
        while (spawnSlots.ContainsValue(spawnSlot))
        {
            spawnSlot++;
        }

        Vector3 spawnPosition = GetSpawnPosition(spawnSlot);
        Quaternion spawnRotation = Quaternion.identity;

        NetworkSpawnStatus status = runner.TrySpawn(
            playerPrefab, out NetworkObject playerObject, spawnPosition, spawnRotation, player
        );
        if (status != NetworkSpawnStatus.Spawned || playerObject == null)
        {
            Debug.LogError($"[NetworkPlayerSpawner] Player spawn failed: {player}, Status={status}");
            return;
        }

        int visualIndex = nextVisualIndex++;

        ApplyPlayerIdentity(playerObject, visualIndex);

        spawnedPlayers.Add(player, playerObject);
        spawnSlots.Add(player, spawnSlot);

        Debug.Log($"[NetworkPlayerSpawner] Spawned player: {player}, VisualIndex={visualIndex}");

        SpawnDebugSpectatorDummiesIfNeeded(runner);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        if (spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
        {
            spawnedPlayers.Remove(player);
            spawnSlots.Remove(player);
            if (playerObject != null)
            {
                runner.Despawn(playerObject);
            }
        }
    }

    /// <summary>
    /// Runnerの終了に伴って生成物が破棄される際に、前セッションの管理参照を解放する。
    /// </summary>
    public void ResetSession()
    {
        spawnedPlayers.Clear();
        spawnSlots.Clear();
        nextVisualIndex = 0;
        debugSpectatorDummies.Clear();
        debugSpectatorDummiesSpawned = false;
    }

    private void SpawnDebugSpectatorDummiesIfNeeded(NetworkRunner runner)
    {
        if (!spawnDebugSpectatorDummies)
        {
            return;
        }

        if (debugSpectatorDummiesSpawned)
        {
            return;
        }

        if (runner == null || !runner.IsServer)
        {
            return;
        }

        if (!playerPrefab.IsValid)
        {
            Debug.LogWarning("[NetworkPlayerSpawner] Cannot spawn debug spectator dummy because Player Prefab is not valid.");
            return;
        }

        debugSpectatorDummiesSpawned = true;

        int count = Mathf.Max(0, debugSpectatorDummyCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 position = GetDebugSpectatorDummyPosition(i);
            Quaternion rotation = Quaternion.identity;

            NetworkSpawnStatus status = runner.TrySpawn(
                playerPrefab, out NetworkObject dummyObject, position, rotation, PlayerRef.None
            );
            if (status != NetworkSpawnStatus.Spawned || dummyObject == null)
            {
                Debug.LogWarning($"[NetworkPlayerSpawner] Dummy spawn failed: Status={status}");
                continue;
            }

            dummyObject.name = $"DebugSpectatorDummy_{i + 1}";

            debugSpectatorDummies.Add(dummyObject);

            int visualIndex = nextVisualIndex++;
            ApplyPlayerIdentity(dummyObject, visualIndex);

            Debug.Log($"[NetworkPlayerSpawner] Spawned debug spectator dummy: {dummyObject.name}");
        }
    }

    private void ApplyPlayerIdentity(NetworkObject playerObject, int visualIndex)
    {
        if (playerObject == null)
        {
            return;
        }

        PlayerIdentity playerIdentity = playerObject.GetComponent<PlayerIdentity>();

        if (playerIdentity != null)
        {
            PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
            int colorIndex = health != null && health.HasTeamAssigned
                ? health.NetworkedTeamIndex
                : visualIndex % NetworkSessionController.MaxPlayerCount;
            playerIdentity.SetIdentity(visualIndex + 1, colorIndex);
        }
        else
        {
            Debug.LogWarning($"{playerObject.name}: PlayerIdentity is not attached.");
        }
    }

    private Vector3 GetSpawnPosition(int slotIndex)
    {
        // 元の左右2地点を含む円周上へ分散し、同時参加者が同じ位置を使わないようにする。
        float angle = InitialSpawnAngles[slotIndex % InitialSpawnAngles.Length] * Mathf.Deg2Rad;
        float radius = 5.0f + 3.0f * (slotIndex / InitialSpawnAngles.Length);
        return new Vector3(Mathf.Cos(angle) * radius, 5.0f, Mathf.Sin(angle) * radius);
    }

    private Vector3 GetDebugSpectatorDummyPosition(int index)
    {
        float angle = index * 120.0f * Mathf.Deg2Rad;
        float radius = 8.0f;

        return new Vector3(
            Mathf.Cos(angle) * radius,
            5.0f,
            Mathf.Sin(angle) * radius
        );
    }
}
