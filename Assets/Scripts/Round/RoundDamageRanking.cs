using System.Collections.Generic;

/// <summary>
/// 撃破数、与ダメージ、残りHPの順で順位を比較する。表示順と時間切れの勝者判定で同じ基準を使う。
/// 死亡者も順位には含めるが、勝者を選ぶ際は呼び出し側で生存者に絞る。
/// </summary>
public static class RoundDamageRanking
{
    public static int CompareScore(RoundDamageEntry first, RoundDamageEntry second)
    {
        int killOrder = second.Kills.CompareTo(first.Kills);
        if (killOrder != 0) return killOrder;
        int damageOrder = second.Damage.CompareTo(first.Damage);
        return damageOrder != 0 ? damageOrder : second.Health.CompareTo(first.Health);
    }

    public static void Sort(List<RoundDamageEntry> entries)
    {
        entries.Sort((first, second) =>
        {
            int order = CompareScore(first, second);
            // 同順位内の表示順だけ色で固定し、勝敗の同点判定には使用しない。
            return order != 0 ? order : first.TeamIndex.CompareTo(second.TeamIndex);
        });
    }

    /// <summary>
    /// ソート済み一覧の順位を返す。同点は同順位とし、次の順位を人数分進める。
    /// </summary>
    public static int GetRank(IReadOnlyList<RoundDamageEntry> entries, int index)
    {
        while (index > 0 && CompareScore(entries[index - 1], entries[index]) == 0)
        {
            index--;
        }

        return index + 1;
    }
}
