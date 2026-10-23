using System.Collections.Generic;
using UnityEngine;

public sealed class RoundReadyController
{
    private readonly Dictionary<PlayerHealth, bool> readyStates = new();

    public void RegisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        readyStates[player] = false;
        player.SetReadyState(false);
    }

    public void UnregisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        readyStates.Remove(player);
    }

    public bool IsPlayerReady(PlayerHealth player)
    {
        return readyStates.TryGetValue(player, out bool ready) && ready;
    }

    public bool SetPlayerReady(PlayerHealth player)
    {
        if (player == null)
        {
            return false;
        }

        if (readyStates.TryGetValue(player, out bool alreadyReady) && alreadyReady)
        {
            return false;
        }

        readyStates[player] = true;
        player.SetReadyState(true);

        Debug.Log($"[RoundReadyController] Ready: {player.gameObject.name}");

        return true;
    }

    public void ResetReadyStates(IEnumerable<PlayerHealth> players)
    {
        foreach (PlayerHealth player in players)
        {
            if (player == null)
            {
                continue;
            }

            readyStates[player] = false;
            player.SetReadyState(false);
        }
    }

    public bool AreAllPlayersReady(
        IReadOnlyList<PlayerHealth> players,
        int minPlayersToStart
    )
    {
        if (players == null || players.Count < minPlayersToStart)
        {
            return false;
        }

        foreach (PlayerHealth player in players)
        {
            if (!IsPlayerReady(player))
            {
                return false;
            }
        }

        return true;
    }

    public int CountReadyPlayers(IReadOnlyList<PlayerHealth> players)
    {
        if (players == null)
        {
            return 0;
        }

        int count = 0;

        foreach (PlayerHealth player in players)
        {
            if (IsPlayerReady(player))
            {
                count++;
            }
        }

        return count;
    }
}