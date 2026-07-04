using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class RoundScoreCalculator
{
    private readonly Dictionary<PlayerHealth, int> points = new();

    public void RegisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        points[player] = 0;
    }

    public void UnregisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        points.Remove(player);
    }

    public void ResetPoints(IEnumerable<PlayerHealth> players)
    {
        foreach (PlayerHealth player in players)
        {
            if (player == null)
            {
                continue;
            }

            points[player] = 0;
        }
    }

    public void AddPoint(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        if (!points.ContainsKey(player))
        {
            points[player] = 0;
        }

        points[player]++;
    }

    public int GetPoint(PlayerHealth player)
    {
        if (player == null)
        {
            return 0;
        }

        return points.TryGetValue(player, out int point)
            ? point
            : 0;
    }

    public int GetPointForTeam(
        IEnumerable<PlayerHealth> players,
        TeamColor team
    )
    {
        int result = 0;

        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.HasTeamAssigned)
            {
                continue;
            }

            if (player.Team == team)
            {
                result += GetPoint(player);
            }
        }

        return result;
    }

    public int GetWinnerTeamIndex(PlayerHealth winner)
    {
        if (winner == null || !winner.HasTeamAssigned)
        {
            return -1;
        }

        return (int)winner.Team;
    }

    public int CreateRoundEndTeamMask(IEnumerable<PlayerHealth> players)
    {
        int mask = 0;

        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.HasTeamAssigned)
            {
                continue;
            }

            mask |= 1 << (int)player.Team;
        }

        return mask;
    }

    public int CreateFinalWinnerTeamMask(IEnumerable<PlayerHealth> players)
    {
        List<PlayerHealth> validPlayers = players
            .Where(player => player != null && player.HasTeamAssigned)
            .ToList();

        int teamMask = CreateRoundEndTeamMask(validPlayers);

        if (teamMask == 0)
        {
            return 0;
        }

        int highestPoint = int.MinValue;

        UpdateHighestPointIfTeamExists(validPlayers, teamMask, TeamColor.Red, ref highestPoint);
        UpdateHighestPointIfTeamExists(validPlayers, teamMask, TeamColor.Blue, ref highestPoint);
        UpdateHighestPointIfTeamExists(validPlayers, teamMask, TeamColor.Green, ref highestPoint);
        UpdateHighestPointIfTeamExists(validPlayers, teamMask, TeamColor.Yellow, ref highestPoint);

        int winnerTeamMask = 0;

        AddWinnerTeamIfHighest(validPlayers, teamMask, TeamColor.Red, highestPoint, ref winnerTeamMask);
        AddWinnerTeamIfHighest(validPlayers, teamMask, TeamColor.Blue, highestPoint, ref winnerTeamMask);
        AddWinnerTeamIfHighest(validPlayers, teamMask, TeamColor.Green, highestPoint, ref winnerTeamMask);
        AddWinnerTeamIfHighest(validPlayers, teamMask, TeamColor.Yellow, highestPoint, ref winnerTeamMask);

        return winnerTeamMask;
    }

    public void LogMatchResult()
    {
        if (points.Count == 0)
        {
            Debug.Log("Match finished, but no players were registered.");
            return;
        }

        int highestPoint = points.Values.Max();

        List<PlayerHealth> winners = points
            .Where(pair => pair.Value == highestPoint)
            .Select(pair => pair.Key)
            .Where(player => player != null)
            .ToList();

        if (winners.Count == 1)
        {
            Debug.Log($"Match Winner: {winners[0].gameObject.name}, Point: {highestPoint}");
        }
        else
        {
            string winnerNames = string.Join(", ", winners.Select(player => player.gameObject.name));
            Debug.Log($"Match Draw: {winnerNames}, Point: {highestPoint}");
        }
    }

    private void UpdateHighestPointIfTeamExists(
        IEnumerable<PlayerHealth> players,
        int teamMask,
        TeamColor team,
        ref int highestPoint
    )
    {
        if (!HasTeamInMask(teamMask, team))
        {
            return;
        }

        highestPoint = Mathf.Max(highestPoint, GetPointForTeam(players, team));
    }

    private void AddWinnerTeamIfHighest(
        IEnumerable<PlayerHealth> players,
        int teamMask,
        TeamColor team,
        int highestPoint,
        ref int winnerTeamMask
    )
    {
        if (!HasTeamInMask(teamMask, team))
        {
            return;
        }

        if (GetPointForTeam(players, team) != highestPoint)
        {
            return;
        }

        winnerTeamMask |= 1 << (int)team;
    }

    private bool HasTeamInMask(int teamMask, TeamColor team)
    {
        return (teamMask & (1 << (int)team)) != 0;
    }
}