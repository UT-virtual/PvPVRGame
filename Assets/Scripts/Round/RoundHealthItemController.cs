using System;
using UnityEngine;

/// <summary>
/// ラウンド進行からHealthItemSpawnerを呼び出し、登録人数に応じた配置と終了時の撤去を仲介する。
/// </summary>
public sealed class RoundHealthItemController
{
    private HealthItemSpawner healthItemSpawner;
    private readonly Func<int> getRegisteredPlayerCount;

    public RoundHealthItemController(
        HealthItemSpawner healthItemSpawner,
        Func<int> getRegisteredPlayerCount
    )
    {
        this.healthItemSpawner = healthItemSpawner;
        this.getRegisteredPlayerCount = getRegisteredPlayerCount;
    }

    public void SetupHealthItemsForCurrentPlayers()
    {
        HealthItemSpawner itemSpawner = GetHealthItemSpawner();

        if (itemSpawner == null)
        {
            Debug.LogWarning("[RoundManager] HealthItemSpawner was not found.");
            return;
        }

        itemSpawner.SetupItemsForPlayerCount(getRegisteredPlayerCount());
    }

    public void ClearHealthItems()
    {
        HealthItemSpawner itemSpawner = GetHealthItemSpawner();

        if (itemSpawner == null)
        {
            return;
        }

        itemSpawner.ClearAllItems();
    }

    private HealthItemSpawner GetHealthItemSpawner()
    {
        if (healthItemSpawner != null)
        {
            return healthItemSpawner;
        }

        healthItemSpawner = UnityEngine.Object.FindFirstObjectByType<HealthItemSpawner>();
        return healthItemSpawner;
    }
}