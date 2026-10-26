using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 進行側の準備状態を保持し、PlayerHealthの同期フラグにも反映する。
/// 開始人数と全員準備完了の判定を提供し、フェーズ遷移はRoundManagerに委ねる。
/// </summary>
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

    /// <summary>
    /// 準備完了を記録してPlayerHealthへ反映し、初めて変更した場合だけtrueを返す。
    /// 登録済みかどうかと待機フェーズの確認はRoundManagerが行う。
    /// </summary>
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