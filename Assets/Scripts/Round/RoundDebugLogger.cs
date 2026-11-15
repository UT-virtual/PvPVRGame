using System.Collections.Generic;
using UnityEngine;

public sealed class RoundDebugLogger
{
    public void LogReadyStates(
        IReadOnlyList<PlayerHealth> validPlayers,
        RoundReadyController readyController,
        int minPlayersToStart
    )
    {
        int readyCount = readyController.CountReadyPlayers(validPlayers);

        foreach (PlayerHealth player in validPlayers)
        {
            bool ready = readyController.IsPlayerReady(player);

            Debug.Log(
                $"[RoundManager] ReadyState: " +
                $"{player.gameObject.name}, Ready={ready}"
            );
        }

        Debug.Log(
            $"[RoundManager] Ready Count: " +
            $"{readyCount}/{validPlayers.Count}, RequiredPlayers={minPlayersToStart}"
        );
    }

    public void LogSkillSelectionStates(
        IReadOnlyList<PlayerHealth> validPlayers,
        RoundSkillSelectionController skillSelectionController,
        int requiredSkillSelectionCount
    )
    {
        int completedCount = 0;

        foreach (PlayerHealth player in validPlayers)
        {
            int selectedCount = skillSelectionController.GetSkillSelectionCount(player);
            PlayerSkillType firstSkill = skillSelectionController.GetFirstSelectedSkill(player);
            PlayerSkillType secondSkill = skillSelectionController.GetSecondSelectedSkill(player);

            bool completed = selectedCount >= requiredSkillSelectionCount;

            if (completed)
            {
                completedCount++;
            }

            Debug.Log(
                $"[RoundManager] SkillState: " +
                $"{player.gameObject.name}, " +
                $"Count={selectedCount}/{requiredSkillSelectionCount}, " +
                $"Completed={completed}, " +
                $"First={firstSkill}, Second={secondSkill}"
            );
        }

        Debug.Log(
            $"[RoundManager] Skill Selected Count: " +
            $"{completedCount}/{validPlayers.Count}"
        );
    }

    public void LogSkillSlots(IReadOnlyList<PlayerSkillType> availableRoundSkills)
    {
        Debug.Log("[RoundManager] Skill Slots:");

        for (int i = 0; i < availableRoundSkills.Count; i++)
        {
            Debug.Log($"[RoundManager] Key {i + 1}: {availableRoundSkills[i]}");
        }
    }

    public void LogSpawnPointSettings(List<Transform> availableSpawnPoints)
    {
        Debug.Log($"[RoundManager] Available SpawnPoint Count: {availableSpawnPoints.Count}");

        for (int i = 0; i < availableSpawnPoints.Count; i++)
        {
            Transform spawnPoint = availableSpawnPoints[i];

            Debug.Log(
                $"[RoundManager] SpawnPoint[{i}] " +
                $"Name={spawnPoint.name}, " +
                $"Position={spawnPoint.position}"
            );
        }
    }
}