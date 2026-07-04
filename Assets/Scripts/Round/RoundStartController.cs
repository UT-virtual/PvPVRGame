using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class RoundStartController
{
    private readonly MonoBehaviour coroutineRunner;

    private readonly Func<RoundManager.GamePhase> getPhase;
    private readonly Action<RoundManager.GamePhase> setPhase;

    private readonly Func<int> getCurrentRound;
    private readonly Func<List<PlayerHealth>> getValidPlayers;

    private readonly Func<float> getStartAfterAllSkillsSelectedDelay;

    private readonly Func<PlayerHealth, PlayerSkillType> getFirstSelectedSkill;
    private readonly Func<PlayerHealth, PlayerSkillType> getSecondSelectedSkill;

    private readonly RoundHealthItemController healthItemController;

    private Coroutine startRoundCoroutine;

    public RoundStartController(
        MonoBehaviour coroutineRunner,
        Func<RoundManager.GamePhase> getPhase,
        Action<RoundManager.GamePhase> setPhase,
        Func<int> getCurrentRound,
        Func<List<PlayerHealth>> getValidPlayers,
        Func<float> getStartAfterAllSkillsSelectedDelay,
        Func<PlayerHealth, PlayerSkillType> getFirstSelectedSkill,
        Func<PlayerHealth, PlayerSkillType> getSecondSelectedSkill,
        RoundHealthItemController healthItemController
    )
    {
        this.coroutineRunner = coroutineRunner;

        this.getPhase = getPhase;
        this.setPhase = setPhase;

        this.getCurrentRound = getCurrentRound;
        this.getValidPlayers = getValidPlayers;

        this.getStartAfterAllSkillsSelectedDelay = getStartAfterAllSkillsSelectedDelay;

        this.getFirstSelectedSkill = getFirstSelectedSkill;
        this.getSecondSelectedSkill = getSecondSelectedSkill;

        this.healthItemController = healthItemController;
    }

    public void StartRoundAfterSkillSelection()
    {
        if (getPhase() != RoundManager.GamePhase.SkillSelecting)
        {
            Debug.Log($"[RoundStartController] Start ignored. Current phase={getPhase()}");
            return;
        }

        if (startRoundCoroutine != null)
        {
            return;
        }

        startRoundCoroutine = coroutineRunner.StartCoroutine(StartRoundAfterSkillSelectionCoroutine());
    }

    public void StopStartRoundCoroutine()
    {
        if (startRoundCoroutine == null)
        {
            return;
        }

        coroutineRunner.StopCoroutine(startRoundCoroutine);
        startRoundCoroutine = null;
    }

    private IEnumerator StartRoundAfterSkillSelectionCoroutine()
    {
        setPhase(RoundManager.GamePhase.RoundStarting);

        ApplySelectedSkillsForCurrentRound();

        Debug.Log(
            $"[RoundStartController] All players selected skills. " +
            $"Round {getCurrentRound()} starts in {getStartAfterAllSkillsSelectedDelay()} seconds."
        );

        yield return new WaitForSeconds(getStartAfterAllSkillsSelectedDelay());

        setPhase(RoundManager.GamePhase.RoundPlaying);
        startRoundCoroutine = null;

        healthItemController.SetupHealthItemsForCurrentPlayers();

        Debug.Log($"[RoundStartController] Round {getCurrentRound()} Start");
    }

    private void ApplySelectedSkillsForCurrentRound()
    {
        List<PlayerHealth> validPlayers = getValidPlayers();

        foreach (PlayerHealth player in validPlayers)
        {
            if (player == null)
            {
                continue;
            }

            PlayerSkillType selectedFirstSkill = getFirstSelectedSkill(player);
            PlayerSkillType selectedSecondSkill = getSecondSelectedSkill(player);

            PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

            if (skillController == null)
            {
                Debug.LogWarning($"[RoundStartController] PlayerSkillController was not found: {player.gameObject.name}");
                continue;
            }

            skillController.PrepareForRound(selectedFirstSkill, selectedSecondSkill);
        }
    }
}