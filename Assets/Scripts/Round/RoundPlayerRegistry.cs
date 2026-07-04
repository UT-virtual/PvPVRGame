using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class RoundPlayerRegistry
{
    private readonly List<PlayerHealth> players = new();
    private readonly RoundManager.TeamColor[] teamOrder;
    private readonly Action<PlayerHealth> onPlayerDied;

    public RoundPlayerRegistry(
        RoundManager.TeamColor[] teamOrder,
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

        RoundManager.PlayerTeam playerTeam = player.GetComponent<RoundManager.PlayerTeam>();

        if (playerTeam == null)
        {
            playerTeam = player.gameObject.AddComponent<RoundManager.PlayerTeam>();
        }

        int index = players.Count - 1;
        RoundManager.TeamColor assignedColor = teamOrder[index % teamOrder.Length];

        playerTeam.SetTeam(assignedColor);
        player.SetTeam(assignedColor);

        Debug.Log($"{player.gameObject.name} joined as {assignedColor}");
    }
}