using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキル選択の完了を受け、選択内容を各プレイヤーへ設定して開始待機から戦闘へ移行する。
/// RoundManagerのStateAuthority側の進行経路でコルーチンを開始・停止する。
/// </summary>
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
    private readonly Func<bool> hasStartDelayElapsed;

    public RoundStartController(
        MonoBehaviour coroutineRunner,
        Func<RoundManager.GamePhase> getPhase,
        Action<RoundManager.GamePhase> setPhase,
        Func<int> getCurrentRound,
        Func<List<PlayerHealth>> getValidPlayers,
        Func<float> getStartAfterAllSkillsSelectedDelay,
        Func<PlayerHealth, PlayerSkillType> getFirstSelectedSkill,
        Func<PlayerHealth, PlayerSkillType> getSecondSelectedSkill,
        RoundHealthItemController healthItemController,
        Func<bool> hasStartDelayElapsed = null
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
        this.hasStartDelayElapsed = hasStartDelayElapsed;
    }

    /// <summary>
    /// 選択フェーズから1回だけ開始待機へ移り、選択済みスキルを設定する。
    /// 待機時間はこのControllerが管理し、UIの演出終了は待たない。
    /// </summary>
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

        if (hasStartDelayElapsed == null)
        {
            yield return new WaitForSeconds(getStartAfterAllSkillsSelectedDelay());
        }
        else
        {
            // UIと同じネットワーク時刻を使い、描画開始の遅れで戦闘開始時刻を変えない。
            while (!hasStartDelayElapsed())
            {
                yield return null;
            }
        }

        if (getPhase() != RoundManager.GamePhase.RoundStarting)
        {
            startRoundCoroutine = null;
            yield break;
        }

        RoundManager.Instance?.ApplyHealthOneEffect();

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
