using System;
using UnityEngine;

public sealed class RoundUIController
{
    private WaitingRoomUI waitingRoomUI;
    private readonly BattleStartUI battleStartUI;
    private RoundEndUI roundEndUI;
    private FinalResultUI finalResultUI;
    private readonly Func<bool> hasStateAuthority;

    public RoundUIController(
        WaitingRoomUI waitingRoomUI,
        BattleStartUI battleStartUI,
        RoundEndUI roundEndUI,
        FinalResultUI finalResultUI,
        Func<bool> hasStateAuthority
    )
    {
        this.waitingRoomUI = waitingRoomUI;
        this.battleStartUI = battleStartUI;
        this.roundEndUI = roundEndUI;
        this.finalResultUI = finalResultUI;
        this.hasStateAuthority = hasStateAuthority;
    }

    public void ShowWaitingRoomUI()
    {
        if (waitingRoomUI == null)
        {
            waitingRoomUI = UnityEngine.Object.FindFirstObjectByType<WaitingRoomUI>();
        }

        if (waitingRoomUI != null)
        {
            waitingRoomUI.ShowRoomUI();
        }
    }

    public void HideWaitingRoomUI()
    {
        if (waitingRoomUI == null)
        {
            waitingRoomUI = UnityEngine.Object.FindFirstObjectByType<WaitingRoomUI>();
        }

        if (waitingRoomUI != null)
        {
            waitingRoomUI.HideRoomUI();
        }
    }

    public void ShowRoundEndUI(
        int roundNumber,
        int winnerTeamIndex,
        int teamMask,
        int redWins,
        int blueWins,
        int greenWins,
        int yellowWins
    )
    {
        if (roundEndUI == null)
        {
            roundEndUI = UnityEngine.Object.FindFirstObjectByType<RoundEndUI>();
        }

        if (roundEndUI == null)
        {
            Debug.LogWarning("[RoundManager] RoundEndUI was not found.");
            return;
        }

        roundEndUI.ShowTeamScores(
            roundNumber,
            winnerTeamIndex,
            teamMask,
            redWins,
            blueWins,
            greenWins,
            yellowWins
        );
    }

    public void HideRoundEndUI()
    {
        if (roundEndUI == null)
        {
            roundEndUI = UnityEngine.Object.FindFirstObjectByType<RoundEndUI>();
        }

        if (roundEndUI != null)
        {
            roundEndUI.Hide();
        }
    }

    public void ShowFinalResultUI(int winnerTeamMask)
    {
        if (finalResultUI == null)
        {
            finalResultUI = UnityEngine.Object.FindFirstObjectByType<FinalResultUI>();
        }

        if (finalResultUI == null)
        {
            Debug.LogWarning("[RoundManager] FinalResultUI was not found.");
            return;
        }

        finalResultUI.ShowWinners(winnerTeamMask);
    }

    public void HideFinalResultUI()
    {
        if (finalResultUI == null)
        {
            finalResultUI = UnityEngine.Object.FindFirstObjectByType<FinalResultUI>();
        }

        if (finalResultUI != null)
        {
            finalResultUI.Hide();
        }
    }

    public void ApplyUIForPhase(
        RoundManager.GamePhase targetPhase,
        bool playBattleStartUI
    )
    {
        Debug.Log(
            $"[RoundManager] ApplyUIForPhase: " +
            $"Phase={targetPhase}, " +
            $"PlayBattleStartUI={playBattleStartUI}, " +
            $"HasStateAuthority={hasStateAuthority()}"
        );

        bool shouldShowWaitingRoom =
            targetPhase == RoundManager.GamePhase.WaitingForReady;

        if (shouldShowWaitingRoom)
        {
            ShowWaitingRoomUI();
        }
        else
        {
            HideWaitingRoomUI();
        }

        if (playBattleStartUI && battleStartUI != null)
        {
            battleStartUI.StopAllCoroutines();
            battleStartUI.StartCoroutine(battleStartUI.PlaySequence());
        }
        else if (playBattleStartUI && battleStartUI == null)
        {
            Debug.LogWarning("[RoundManager] BattleStartUI was not found.");
        }
    }
}