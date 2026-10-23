using Fusion;
using System.Collections.Generic;
using UnityEngine;

public sealed class NetworkPlayerSpawner
{
    private readonly NetworkPrefabRef playerPrefab;
    private readonly bool spawnDebugSpectatorDummies;
    private readonly int debugSpectatorDummyCount;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();
    private readonly List<NetworkObject> debugSpectatorDummies = new();

    private bool debugSpectatorDummiesSpawned;

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

        Vector3 spawnPosition = GetSpawnPosition(player);
        Quaternion spawnRotation = Quaternion.identity;

        NetworkObject playerObject = runner.Spawn(
            playerPrefab,
            spawnPosition,
            spawnRotation,
            player
        );

        int visualIndex = spawnedPlayers.Count;

        ApplyPlayerIdentity(playerObject, visualIndex);

        spawnedPlayers.Add(player, playerObject);

        Debug.Log($"[NetworkPlayerSpawner] Spawned player: {player}, VisualIndex={visualIndex}");

        SpawnDebugSpectatorDummiesIfNeeded(runner);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null)
        {
            return;
        }

        if (spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
        {
            runner.Despawn(playerObject);
            spawnedPlayers.Remove(player);
        }
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

            NetworkObject dummyObject = runner.Spawn(
                playerPrefab,
                position,
                rotation,
                PlayerRef.None
            );

            dummyObject.name = $"DebugSpectatorDummy_{i + 1}";

            debugSpectatorDummies.Add(dummyObject);

            int visualIndex = spawnedPlayers.Count + i;
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
            playerIdentity.SetIdentity(
                visualIndex + 1,
                visualIndex
            );
        }
        else
        {
            Debug.LogWarning($"{playerObject.name}: PlayerIdentity is not attached.");
        }
    }

    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        if (player.RawEncoded % 2 == 0)
        {
            return new Vector3(-5.0f, 5.0f, 0.0f);
        }

        return new Vector3(5.0f, 5.0f, 0.0f);
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
