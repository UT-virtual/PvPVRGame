using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ラウンド参加者の登録と死亡イベントの購読を管理し、既存参加者と重複しないチーム色を割り当てる。
/// 得点・準備・スキルの状態はRoundManagerが別の担当へ登録する。
/// </summary>
public sealed class RoundPlayerRegistry
{
    private readonly List<PlayerHealth> players = new();
    private readonly TeamColor[] teamOrder;
    private readonly Action<PlayerHealth> onPlayerDied;

    public RoundPlayerRegistry(
        TeamColor[] teamOrder,
        Action<PlayerHealth> onPlayerDied
    )
    {
        this.teamOrder = teamOrder;
        this.onPlayerDied = onPlayerDied;
    }

    public IReadOnlyList<PlayerHealth> Players => players;

    public int RegisteredPlayerCount
    {
        get
        {
            return players.Count(player => player != null);
        }
    }

    public bool Contains(PlayerHealth player)
    {
        return player != null && players.Contains(player);
    }

    public bool RegisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return false;
        }

        if (players.Contains(player))
        {
            return false;
        }

        players.Add(player);

        AssignTeamColor(player);

        player.OnDied += onPlayerDied;

        Debug.Log(
            $"[RoundPlayerRegistry] Registered: {player.gameObject.name}, " +
            $"Count={RegisteredPlayerCount}"
        );

        return true;
    }

    public bool UnregisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return false;
        }

        player.OnDied -= onPlayerDied;

        bool removed = players.Remove(player);

        if (removed)
        {
            Debug.Log(
                $"[RoundPlayerRegistry] Unregistered: {player.gameObject.name}, " +
                $"Count={RegisteredPlayerCount}"
            );
        }

        return removed;
    }

    public List<PlayerHealth> GetValidPlayers()
    {
        return players
            .Where(player => player != null)
            .ToList();
    }

    private void AssignTeamColor(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        // 退出で空いた色だけを再利用し、残ったプレイヤーの色・得点の対応は変えない。
        foreach (TeamColor color in teamOrder)
        {
            bool used = players.Any(other => other != null && other != player &&
                other.HasTeamAssigned && other.Team == color);
            if (used)
            {
                continue;
            }

            player.SetTeam(color);
            Debug.Log($"{player.gameObject.name} joined as {color}");
            return;
        }

        Debug.LogWarning("[RoundPlayerRegistry] No unused team color is available.");
    }
}