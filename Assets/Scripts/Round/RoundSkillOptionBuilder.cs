using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 重複とNoneを除いた候補から、1回目と2回目で重ならない選択肢を抽選する。
/// 候補不足の枠はNoneのまま残す。
/// </summary>
public sealed class RoundSkillOptionBuilder
{
    public void BuildSkillOptions(
        IReadOnlyList<PlayerSkillType> availableRoundSkills,
        PlayerSkillType[] firstOptions,
        PlayerSkillType[] secondOptions
    )
    {
        List<PlayerSkillType> candidates = availableRoundSkills
            .Where(skill => skill != PlayerSkillType.None)
            .Distinct()
            .ToList();

        FillSkillOptionArray(firstOptions, candidates);

        HashSet<PlayerSkillType> firstOptionSet = firstOptions
            .Where(skill => skill != PlayerSkillType.None)
            .ToHashSet();

        List<PlayerSkillType> secondCandidates = candidates
            .Where(skill => !firstOptionSet.Contains(skill))
            .ToList();

        FillSkillOptionArray(secondOptions, secondCandidates);
    }

    private void FillSkillOptionArray(
        PlayerSkillType[] targetOptions,
        List<PlayerSkillType> candidates
    )
    {
        if (targetOptions == null)
        {
            return;
        }

        for (int i = 0; i < targetOptions.Length; i++)
        {
            targetOptions[i] = PlayerSkillType.None;
        }

        if (candidates == null || candidates.Count == 0)
        {
            return;
        }

        List<PlayerSkillType> shuffledCandidates = candidates
            .Where(skill => skill != PlayerSkillType.None)
            .Distinct()
            .ToList();

        ShuffleSkillList(shuffledCandidates);

        for (int i = 0; i < targetOptions.Length; i++)
        {
            targetOptions[i] = i < shuffledCandidates.Count
                ? shuffledCandidates[i]
                : PlayerSkillType.None;
        }
    }

    private void ShuffleSkillList(List<PlayerSkillType> list)
    {
        if (list == null)
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            PlayerSkillType temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}