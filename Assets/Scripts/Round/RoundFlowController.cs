using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class RoundFlowController
{
    private readonly MonoBehaviour coroutineRunner;

    private readonly Func<RoundManager.GamePhase> getPhase;
    private readonly Action<RoundManager.GamePhase> setPhase;

    private readonly Func<int> getCurrentRound;
    private readonly Action<int> setCurrentRound;

    private readonly Func<List<PlayerHealth>> getValidPlayers;
    private readonly Func<int> getRegisteredPlayerCount;

    private readonly Func<bool> countDebugSpectatorDummiesAsAlive;

    private readonly Func<int> getMaxRoundCount;
    private readonly Func<float> getNextRoundDelay;
    private readonly Func<float> getReturnToWaitingDelay;

    private readonly RoundScoreCalculator scoreCalculator;
    private readonly RoundReadyController readyController;
    private readonly RoundSkillSelectionController skillSelectionController;
    private readonly RoundSpawnController spawnController;
    private readonly RoundHealthItemController healthItemController;
    private readonly RoundProjectileCleaner projectileCleaner;

    private readonly Action<
        int,
        int,
        int,
        int,
        int,
        int,
        int
    > showRoundEndUIRpc;

    private readonly Action hideRoundEndUIRpc;
    private readonly Action<int> showFinalResultUIRpc;
    private readonly Action hideFinalResultUIRpc;

    private readonly Action showWaitingRoomUI;
    private readonly Action logReadyStates;

    public RoundFlowController(
        MonoBehaviour coroutineRunner,
        Func<RoundManager.GamePhase> getPhase,
        Action<RoundManager.GamePhase> setPhase,
        Func<int> getCurrentRound,
        Action<int> setCurrentRound,
        Func<List<PlayerHealth>> getValidPlayers,
        Func<int> getRegisteredPlayerCount,
        Func<bool> countDebugSpectatorDummiesAsAlive,
        Func<int> getMaxRoundCount,
        Func<float> getNextRoundDelay,
        Func<float> getReturnToWaitingDelay,
        RoundScoreCalculator scoreCalculator,
        RoundReadyController readyController,
        RoundSkillSelectionController skillSelectionController,
        RoundSpawnController spawnController,
        RoundHealthItemController healthItemController,
        RoundProjectileCleaner projectileCleaner,
        Action<int, int, int, int, int, int, int> showRoundEndUIRpc,
        Action hideRoundEndUIRpc,
        Action<int> showFinalResultUIRpc,
        Action hideFinalResultUIRpc,
        Action showWaitingRoomUI,
        Action logReadyStates
    )
    {
        this.coroutineRunner = coroutineRunner;

        this.getPhase = getPhase;
        this.setPhase = setPhase;

        this.getCurrentRound = getCurrentRound;
        this.setCurrentRound = setCurrentRound;

        this.getValidPlayers = getValidPlayers;
        this.getRegisteredPlayerCount = getRegisteredPlayerCount;

        this.countDebugSpectatorDummiesAsAlive = countDebugSpectatorDummiesAsAlive;

        this.getMaxRoundCount = getMaxRoundCount;
        this.getNextRoundDelay = getNextRoundDelay;
        this.getReturnToWaitingDelay = getReturnToWaitingDelay;

        this.scoreCalculator = scoreCalculator;
        this.readyController = readyController;
        this.skillSelectionController = skillSelectionController;
        this.spawnController = spawnController;
        this.healthItemController = healthItemController;
        this.projectileCleaner = projectileCleaner;

        this.showRoundEndUIRpc = showRoundEndUIRpc;
        this.hideRoundEndUIRpc = hideRoundEndUIRpc;
        this.showFinalResultUIRpc = showFinalResultUIRpc;
        this.hideFinalResultUIRpc = hideFinalResultUIRpc;

        this.showWaitingRoomUI = showWaitingRoomUI;
        this.logReadyStates = logReadyStates;
    }

    public void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        if (deadPlayer == null)
        {
            return;
        }

        Debug.Log($"[RoundManager] HandlePlayerDied: {deadPlayer.gameObject.name}");

        if (getPhase() != RoundManager.GamePhase.RoundPlaying)
        {
            Debug.Log($"[RoundManager] Death ignored. Current phase={getPhase()}");
            return;
        }

        List<PlayerHealth> alivePlayers = getValidPlayers()
            .Where(player => !player.IsDead)
            .ToList();

        int debugAliveDummyCount = countDebugSpectatorDummiesAsAlive()
            ? CountAliveDebugSpectatorDummies()
            : 0;

        int aliveCountForRoundEnd = alivePlayers.Count + debugAliveDummyCount;

        Debug.Log(
            $"[RoundManager] Alive Count: {alivePlayers.Count} / Registered Count: {getRegisteredPlayerCount()}, " +
            $"DebugAliveDummyCount={debugAliveDummyCount}, " +
            $"AliveCountForRoundEnd={aliveCountForRoundEnd}"
        );

        foreach (PlayerHealth player in alivePlayers)
        {
            Debug.Log($"[RoundManager] Alive: {player.gameObject.name}");
        }

        if (aliveCountForRoundEnd > 1)
        {
            return;
        }

        PlayerHealth roundWinner = alivePlayers.Count == 1 ? alivePlayers[0] : null;

        coroutineRunner.StartCoroutine(EndRoundCoroutine(roundWinner));
    }

    private IEnumerator EndRoundCoroutine(PlayerHealth roundWinner)
    {
        Debug.Log("[RoundManager] EndRoundCoroutine started");

        setPhase(RoundManager.GamePhase.RoundEnding);

        healthItemController.ClearHealthItems();

        int currentRound = getCurrentRound();

        if (roundWinner != null)
        {
            scoreCalculator.AddPoint(roundWinner);

            Debug.Log(
                $"{roundWinner.gameObject.name} wins Round {currentRound}. " +
                $"Point: {scoreCalculator.GetPoint(roundWinner)}"
            );
        }
        else
        {
            Debug.Log($"Round {currentRound} ended with no winner.");
        }

        List<PlayerHealth> validPlayers = getValidPlayers();

        showRoundEndUIRpc(
            currentRound,
            scoreCalculator.GetWinnerTeamIndex(roundWinner),
            scoreCalculator.CreateRoundEndTeamMask(validPlayers),
            scoreCalculator.GetPointForTeam(validPlayers, RoundManager.TeamColor.Red),
            scoreCalculator.GetPointForTeam(validPlayers, RoundManager.TeamColor.Blue),
            scoreCalculator.GetPointForTeam(validPlayers, RoundManager.TeamColor.Green),
            scoreCalculator.GetPointForTeam(validPlayers, RoundManager.TeamColor.Yellow)
        );

        yield return new WaitForSeconds(getNextRoundDelay());

        hideRoundEndUIRpc();

        projectileCleaner.DespawnProjectiles();

        if (currentRound >= getMaxRoundCount())
        {
            yield return coroutineRunner.StartCoroutine(FinishMatchAndReturnToWaitingCoroutine());
            yield break;
        }

        setCurrentRound(currentRound + 1);

        skillSelectionController.BeginSkillSelectionForRound(getCurrentRound());
    }

    private IEnumerator FinishMatchAndReturnToWaitingCoroutine()
    {
        setPhase(RoundManager.GamePhase.MatchFinished);

        healthItemController.ClearHealthItems();
        projectileCleaner.DespawnProjectiles();
        skillSelectionController.StopSkillSelectionCoroutines();

        scoreCalculator.LogMatchResult();

        showFinalResultUIRpc(
            scoreCalculator.CreateFinalWinnerTeamMask(getValidPlayers())
        );

        Debug.Log(
            $"[RoundManager] Match finished. Returning to waiting state in {getReturnToWaitingDelay()} seconds."
        );

        yield return new WaitForSeconds(getReturnToWaitingDelay());

        hideFinalResultUIRpc();

        ResetMatchStateToWaiting();
    }

    private void ResetMatchStateToWaiting()
    {
        hideFinalResultUIRpc();

        healthItemController.ClearHealthItems();
        projectileCleaner.DespawnProjectiles();
        skillSelectionController.StopSkillSelectionCoroutines();

        setCurrentRound(1);

        List<PlayerHealth> validPlayers = getValidPlayers();

        scoreCalculator.ResetPoints(validPlayers);
        readyController.ResetReadyStates(validPlayers);
        skillSelectionController.ResetSelectionStates(validPlayers);

        foreach (PlayerHealth player in validPlayers)
        {
            PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

            if (skillController != null)
            {
                skillController.ResetSkillHistory();
            }
        }

        spawnController.RespawnAllPlayersWithoutOverlap(validPlayers);

        setPhase(RoundManager.GamePhase.WaitingForReady);

        showWaitingRoomUI();

        Debug.Log("[RoundManager] Returned to waiting state.");
        Debug.Log("[RoundManager] Press Enter or ZL to ready.");

        logReadyStates();
    }

    private int CountAliveDebugSpectatorDummies()
    {
        PlayerHealth[] allPlayers = UnityEngine.Object.FindObjectsByType<PlayerHealth>(
            FindObjectsSortMode.None
        );

        int count = 0;
        List<PlayerHealth> registeredPlayers = getValidPlayers();

        foreach (PlayerHealth player in allPlayers)
        {
            if (player == null)
            {
                continue;
            }

            if (registeredPlayers.Contains(player))
            {
                continue;
            }

            if (player.IsDead)
            {
                continue;
            }

            if (!player.gameObject.name.StartsWith("DebugSpectatorDummy"))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}