
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

    private readonly Action<int, int, int, int, int, int, int> showRoundEndUIRpc;
    private readonly Action hideRoundEndUIRpc;

    // FINAL DUEL表示RPC
    private readonly Action showFinalDuelUIRpc;

    private readonly Action<int> showFinalResultUIRpc;
    private readonly Action hideFinalResultUIRpc;

    private readonly Action showWaitingRoomUI;
    private readonly Action logReadyStates;
    private readonly Action beginRoundEffect;

    private bool isRoundEnding;
    private bool roundEndCheckPending;
    private Coroutine roundEndCoroutine;

    // ファイナルキル演出の待機時間
    private const float FinalKillResultDelay = 2.0f;

    // このラウンドですでにFINAL DUELを表示したか
    private bool finalDuelShown;

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
        Action showFinalDuelUIRpc,
        Action<int> showFinalResultUIRpc,
        Action hideFinalResultUIRpc,
        Action showWaitingRoomUI,
        Action logReadyStates,
        Action beginRoundEffect
    )
    {
        this.coroutineRunner = coroutineRunner;

        this.getPhase = getPhase;
        this.setPhase = setPhase;

        this.getCurrentRound = getCurrentRound;
        this.setCurrentRound = setCurrentRound;

        this.getValidPlayers = getValidPlayers;
        this.getRegisteredPlayerCount = getRegisteredPlayerCount;

        this.countDebugSpectatorDummiesAsAlive =
            countDebugSpectatorDummiesAsAlive;

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
        this.showFinalDuelUIRpc = showFinalDuelUIRpc;

        this.showFinalResultUIRpc = showFinalResultUIRpc;
        this.hideFinalResultUIRpc = hideFinalResultUIRpc;

        this.showWaitingRoomUI = showWaitingRoomUI;
        this.logReadyStates = logReadyStates;
        this.beginRoundEffect = beginRoundEffect;
    }

    // =========================================================
    // プレイヤー死亡処理
    // =========================================================

    public void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        if (deadPlayer == null)
        {
            return;
        }

        Debug.Log(
            $"[RoundFlowController] HandlePlayerDied: {deadPlayer.gameObject.name}"
        );

        roundEndCheckPending = true;
    }

    public void ResolvePendingDeaths()
    {
        if (!roundEndCheckPending)
        {
            return;
        }

        roundEndCheckPending = false;

        CheckRoundEnd();
    }

    // =========================================================
    // FINAL DUEL判定
    // =========================================================

    private int GetAliveCount()
    {
        List<PlayerHealth> alivePlayers = getValidPlayers()
            .Where(player => player != null && !player.IsDead)
            .ToList();

        int debugAliveDummyCount =
            countDebugSpectatorDummiesAsAlive()
                ? CountAliveDebugSpectatorDummies()
                : 0;

        return alivePlayers.Count + debugAliveDummyCount;
    }

    private void TryShowFinalDuel(int aliveCount)
    {
        if (finalDuelShown)
        {
            return;
        }

        if (aliveCount != 2)
        {
            return;
        }

        finalDuelShown = true;

        Debug.Log(
            "[RoundFlowController] FINAL DUEL started! Alive players = 2"
        );

        showFinalDuelUIRpc?.Invoke();
    }

    // ラウンド開始時に呼び出す
    // 最初から2人で開始する場合にもFINAL DUELを表示する
    public void CheckFinalDuelAtRoundStart()
    {
        if (getPhase() != RoundManager.GamePhase.RoundPlaying)
        {
            return;
        }

        if (isRoundEnding)
        {
            return;
        }

        int aliveCount = GetAliveCount();

        Debug.Log(
            $"[RoundFlowController] Round start FINAL DUEL check: Alive={aliveCount}"
        );

        TryShowFinalDuel(aliveCount);
    }

    // =========================================================
    // ラウンド終了判定
    // =========================================================

    public void EndRoundOnTimeout()
    {
        EndRound(scoreCalculator.GetTimeoutWinner());
    }

    public void CheckRoundEnd()
    {
        RoundManager.GamePhase currentPhase = getPhase();

        if (currentPhase != RoundManager.GamePhase.RoundPlaying)
        {
            Debug.Log(
                $"[RoundFlowController] Death ignored. Current phase={currentPhase}"
            );

            return;
        }

        if (isRoundEnding)
        {
            Debug.Log(
                "[RoundFlowController] Death ignored. Round is already ending."
            );

            return;
        }

        List<PlayerHealth> alivePlayers = getValidPlayers()
            .Where(player => player != null && !player.IsDead)
            .ToList();

        int debugAliveDummyCount =
            countDebugSpectatorDummiesAsAlive()
                ? CountAliveDebugSpectatorDummies()
                : 0;

        int aliveCountForRoundEnd =
            alivePlayers.Count + debugAliveDummyCount;

        Debug.Log(
            $"[RoundFlowController] Alive Count: {alivePlayers.Count} / " +
            $"Registered Count: {getRegisteredPlayerCount()}, " +
            $"DebugAliveDummyCount={debugAliveDummyCount}, " +
            $"AliveCountForRoundEnd={aliveCountForRoundEnd}"
        );

        foreach (PlayerHealth player in alivePlayers)
        {
            Debug.Log(
                $"[RoundFlowController] Alive: {player.gameObject.name}"
            );
        }

        // 生存人数が2人ならFINAL DUELを表示
        TryShowFinalDuel(aliveCountForRoundEnd);

        // 生存者が2人以上ならラウンドを続行
        if (aliveCountForRoundEnd > 1)
        {
            return;
        }

        PlayerHealth roundWinner = alivePlayers.Count == 1
            ? alivePlayers[0]
            : null;

        EndRound(roundWinner);
    }

    // =========================================================
    // ラウンド終了
    // =========================================================

    private void EndRound(PlayerHealth roundWinner)
    {
        if (
            isRoundEnding ||
            getPhase() != RoundManager.GamePhase.RoundPlaying
        )
        {
            return;
        }

        isRoundEnding = true;

        setPhase(RoundManager.GamePhase.RoundEnding);

        roundEndCoroutine = coroutineRunner.StartCoroutine(
            EndRoundCoroutine(roundWinner)
        );
    }

    private IEnumerator EndRoundCoroutine(PlayerHealth roundWinner)
    {
        Debug.Log(
            "[RoundFlowController] EndRoundCoroutine started"
        );

        healthItemController.ClearHealthItems();

        int completedRound = getCurrentRound();

        // ファイナルキルの場合、撃破UIが表示される時間を確保する
        yield return new WaitForSeconds(FinalKillResultDelay);

        if (roundWinner != null)
        {
            scoreCalculator.AddPoint(roundWinner);

            Debug.Log(
                $"{roundWinner.gameObject.name} wins Round {completedRound}. " +
                $"Point: {scoreCalculator.GetPoint(roundWinner)}"
            );
        }
        else
        {
            Debug.Log(
                $"Round {completedRound} ended with no winner."
            );
        }

        List<PlayerHealth> validPlayers = getValidPlayers();

        showRoundEndUIRpc(
            completedRound,
            scoreCalculator.GetWinnerTeamIndex(roundWinner),
            scoreCalculator.CreateRoundEndTeamMask(validPlayers),
            scoreCalculator.GetPointForTeam(validPlayers, TeamColor.Red),
            scoreCalculator.GetPointForTeam(validPlayers, TeamColor.Blue),
            scoreCalculator.GetPointForTeam(validPlayers, TeamColor.Green),
            scoreCalculator.GetPointForTeam(validPlayers, TeamColor.Yellow)
        );

        yield return new WaitForSeconds(getNextRoundDelay());

        hideRoundEndUIRpc();

        projectileCleaner.DespawnProjectiles();

        if (completedRound >= getMaxRoundCount())
        {
            yield return FinishMatchAndReturnToWaitingCoroutine();

            yield break;
        }

        setCurrentRound(completedRound + 1);

        // 次のラウンドではFINAL DUELを再表示できるようにする
        finalDuelShown = false;

        beginRoundEffect();

        isRoundEnding = false;
        roundEndCoroutine = null;
    }

    // =========================================================
    // 試合終了
    // =========================================================

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
            $"[RoundFlowController] Match finished. " +
            $"Returning to waiting state in {getReturnToWaitingDelay()} seconds."
        );

        yield return new WaitForSeconds(
            getReturnToWaitingDelay()
        );

        ResetMatchStateToWaiting();

        roundEndCoroutine = null;
    }

    // =========================================================
    // 進行停止・リセット
    // =========================================================

    public void StopProgression()
    {
        roundEndCheckPending = false;

        if (roundEndCoroutine != null)
        {
            coroutineRunner.StopCoroutine(roundEndCoroutine);

            roundEndCoroutine = null;
        }

        skillSelectionController.StopSkillSelectionCoroutines();
    }

    public void AbortMatchToWaiting()
    {
        StopProgression();

        ResetMatchStateToWaiting();
    }

    private void ResetMatchStateToWaiting()
    {
        setPhase(RoundManager.GamePhase.WaitingForReady);

        hideRoundEndUIRpc();

        hideFinalResultUIRpc();

        healthItemController.ClearHealthItems();

        projectileCleaner.DespawnProjectiles();

        skillSelectionController.StopSkillSelectionCoroutines();

        setCurrentRound(1);

        // 次の試合にフラグを持ち越さない
        finalDuelShown = false;

        List<PlayerHealth> validPlayers = getValidPlayers();

        scoreCalculator.ResetPoints(validPlayers);

        readyController.ResetReadyStates(validPlayers);

        skillSelectionController.ResetSelectionStates(validPlayers);

        foreach (PlayerHealth player in validPlayers)
        {
            if (player == null)
            {
                continue;
            }

            PlayerSkillController skillController =
                player.GetComponent<PlayerSkillController>();

            if (skillController != null)
            {
                skillController.ResetSkillHistory();
            }
        }

        spawnController.RespawnAllPlayersWithoutOverlap(validPlayers);

        showWaitingRoomUI();

        isRoundEnding = false;

        Debug.Log(
            "[RoundFlowController] Returned to waiting state."
        );

        Debug.Log(
            "[RoundFlowController] Press Enter or ZL to ready."
        );

        logReadyStates();
    }

    // =========================================================
    // デバッグ用ダミー生存人数
    // =========================================================

    private int CountAliveDebugSpectatorDummies()
    {
        PlayerHealth[] allPlayers =
            UnityEngine.Object.FindObjectsByType<PlayerHealth>(
                FindObjectsSortMode.None
            );

        int count = 0;

        HashSet<PlayerHealth> registeredPlayers =
            new(getValidPlayers());

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

            if (
                !player.gameObject.name.StartsWith(
                    "DebugSpectatorDummy"
                )
            )
            {
                continue;
            }

            count++;
        }

        return count;
    }
}
