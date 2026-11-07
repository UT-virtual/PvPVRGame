using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>
/// StateAuthority側でラウンド別の撃破数・実効与ダメージと試合全体の勝利数を集計する。
/// ネットワーク通知はRoundFlowControllerとRoundManagerに委ねる。
/// </summary>
public sealed class RoundScoreCalculator
{
    private readonly Dictionary<PlayerHealth, int> points = new();
    private readonly Dictionary<PlayerRef, RoundDamageEntry> roundDamage = new();
    private readonly Dictionary<PlayerRef, PlayerHealth> roundPlayers = new();
    private readonly HashSet<PlayerRef> recordedDeaths = new();

    /// <summary>
    /// 戦闘開始時の対戦者だけを記録する。観戦者は含めず、前ラウンドの撃破数・与ダメージを破棄する。
    /// </summary>
    public void BeginRoundDamage(IEnumerable<PlayerHealth> players)
    {
        roundDamage.Clear();
        roundPlayers.Clear();
        recordedDeaths.Clear();
        foreach (PlayerHealth player in players)
        {
            if (player == null || player.Object == null || player.Object.InputAuthority == PlayerRef.None)
            {
                continue;
            }

            PlayerRef playerRef = player.Object.InputAuthority;
            roundPlayers[playerRef] = player;
            roundDamage[playerRef] = new RoundDamageEntry
            {
                Player = playerRef,
                TeamIndex = player.NetworkedTeamIndex,
                Health = player.CurrentHealth,
                MaxHealth = player.MaxHealth,
                IsAlive = !player.IsDead
            };
        }
    }

    /// <summary>
    /// HP減少の確定経路からだけ呼ぶ。軽減前の値やオーバーキル分をここへ渡さない。
    /// lethalがtrueなら、そのHP減少で倒した相手を一度だけ撃破数へ加える。
    /// </summary>
    public void RecordDamage(PlayerRef attacker, PlayerRef victim, float effectiveDamage, bool lethal = false)
    {
        if (attacker == victim || effectiveDamage <= 0f ||
            !roundDamage.TryGetValue(attacker, out RoundDamageEntry entry) || !roundDamage.ContainsKey(victim))
        {
            return;
        }

        entry.Damage += effectiveDamage;
        if (lethal && recordedDeaths.Add(victim)) entry.Kills++;
        roundDamage[attacker] = entry;
    }

    public void CopyRoundDamage(List<RoundDamageEntry> destination)
    {
        destination.Clear();
        foreach (var pair in roundPlayers)
        {
            RoundDamageEntry entry = roundDamage[pair.Key];
            PlayerHealth player = pair.Value;
            bool connected = player != null && player.Object != null;
            entry.IsAlive = connected && !player.IsDead;
            entry.Health = connected ? player.CurrentHealth : 0f;
            destination.Add(entry);
        }

        RoundDamageRanking.Sort(destination);
    }

    /// <summary>
    /// 生存者の撃破数、与ダメージ、残りHPを比較する。最大値が複数人なら勝者なしとして返す。
    /// </summary>
    public PlayerHealth GetTimeoutWinner()
    {
        PlayerHealth winner = null;
        RoundDamageEntry highest = default;
        bool tied = false;
        foreach (var pair in roundPlayers)
        {
            PlayerHealth player = pair.Value;
            if (player == null || player.Object == null || player.IsDead)
            {
                continue;
            }

            RoundDamageEntry entry = roundDamage[pair.Key];
            entry.Health = player.CurrentHealth;
            int order = winner == null ? -1 : RoundDamageRanking.CompareScore(entry, highest);
            if (order < 0)
            {
                winner = player;
                highest = entry;
                tied = false;
            }
            else if (order == 0)
            {
                tied = true;
            }
        }

        return tied ? null : winner;
    }

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
        // 退出者の与ダメージは残し、キャラクター参照だけ外して勝者候補から除く。
        foreach (PlayerRef playerRef in new List<PlayerRef>(roundPlayers.Keys))
        {
            if (roundPlayers[playerRef] == player)
            {
                roundPlayers[playerRef] = null;
            }
        }
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

    /// <summary>
    /// 参加中のチームの合計勝利数を比較し、最多の全チームをビットマスクで返す。
    /// ビット位置はTeamColorの数値で、対象チームがなければ0を返す。
    /// </summary>
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
