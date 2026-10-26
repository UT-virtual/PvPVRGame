using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 死亡通知からラウンド終了を判定し、得点確定、結果通知、次の選択または試合終了後の待機復帰を進める。
/// RoundManagerのStateAuthority側の進行経路から使用し、終了処理の重複を抑止する。
/// </summary>
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

    private readonly Action beginRoundEffect;

    private bool isRoundEnding;
    private Coroutine roundEndCoroutine;

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
        this.beginRoundEffect = beginRoundEffect;
    }

    /// <summary>
    /// 戦闘中の死亡を受け、生存人数が1以下なら終了フェーズへ移る。
    /// 終了フラグをコルーチン開始前に立て、同じラウンドの得点加算と結果通知の重複を防ぐ。
    /// </summary>
    public void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        if (deadPlayer == null)
        {
            return;
        }

        Debug.Log($"[RoundFlowController] HandlePlayerDied: {deadPlayer.gameObject.name}");

        CheckRoundEnd();
    }

    /// <summary>
    /// 死亡・退出後の参加者一覧で終了条件を再判定する。終了開始済みなら何もしない。
    /// </summary>
    public void CheckRoundEnd()
    {
        RoundManager.GamePhase currentPhase = getPhase();

        if (currentPhase != RoundManager.GamePhase.RoundPlaying)
        {
            Debug.Log($"[RoundFlowController] Death ignored. Current phase={currentPhase}");
            return;
        }

        if (isRoundEnding)
        {
            Debug.Log("[RoundFlowController] Death ignored. Round is already ending.");
            return;
        }

        List<PlayerHealth> alivePlayers = getValidPlayers()
            .Where(player => player != null && !player.IsDead)
            .ToList();

        int debugAliveDummyCount = countDebugSpectatorDummiesAsAlive()
            ? CountAliveDebugSpectatorDummies()
            : 0;

        int aliveCountForRoundEnd = alivePlayers.Count + debugAliveDummyCount;

        Debug.Log(
            $"[RoundFlowController] Alive Count: {alivePlayers.Count} / Registered Count: {getRegisteredPlayerCount()}, " +
            $"DebugAliveDummyCount={debugAliveDummyCount}, " +
            $"AliveCountForRoundEnd={aliveCountForRoundEnd}"
        );

        foreach (PlayerHealth player in alivePlayers)
        {
            Debug.Log($"[RoundFlowController] Alive: {player.gameObject.name}");
        }

        if (aliveCountForRoundEnd > 1)
        {
            return;
        }

        PlayerHealth roundWinner = alivePlayers.Count == 1 ? alivePlayers[0] : null;

        isRoundEnding = true;
        setPhase(RoundManager.GamePhase.RoundEnding);

        roundEndCoroutine = coroutineRunner.StartCoroutine(EndRoundCoroutine(roundWinner));
    }

    private IEnumerator EndRoundCoroutine(PlayerHealth roundWinner)
    {
        Debug.Log("[RoundFlowController] EndRoundCoroutine started");

        healthItemController.ClearHealthItems();

        int completedRound = getCurrentRound();

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
            Debug.Log($"Round {completedRound} ended with no winner.");
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

        beginRoundEffect();

        isRoundEnding = false;
        roundEndCoroutine = null;
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
            $"[RoundFlowController] Match finished. Returning to waiting state in {getReturnToWaitingDelay()} seconds."
        );

        yield return new WaitForSeconds(getReturnToWaitingDelay());

        ResetMatchStateToWaiting();
        roundEndCoroutine = null;
    }

    /// <summary>
    /// 遅延中のラウンド終了と選択・開始待機を停止する。Despawn時には状態を再初期化しない。
    /// </summary>
    public void StopProgression()
    {
        if (roundEndCoroutine != null)
        {
            coroutineRunner.StopCoroutine(roundEndCoroutine);
            roundEndCoroutine = null;
        }

        skillSelectionController.StopSkillSelectionCoroutines();
    }

    /// <summary>
    /// 人数不足で試合を中断し、残った参加者の得点・準備・スキルを初期化して待機へ戻す。
    /// </summary>
    public void AbortMatchToWaiting()
    {
        StopProgression();
        ResetMatchStateToWaiting();
    }

    private void ResetMatchStateToWaiting()
    {
        // 復活・スキル解除に伴うイベントが、終了判定へ再入しないよう先にフェーズを閉じる。
        setPhase(RoundManager.GamePhase.WaitingForReady);
        hideRoundEndUIRpc();
        hideFinalResultUIRpc();

        healthItemController.ClearHealthItems();
        projectileCleaner.DespawnProjectiles();
        skillSelectionController.StopSkillSelectionCoroutines();

        setCurrentRound(1);

        List<PlayerHealth> validPlayers = getValidPlayers();

        // 待機への切替と同じ処理内で、次の試合に持ち越せない状態を消去する。
        scoreCalculator.ResetPoints(validPlayers);
        readyController.ResetReadyStates(validPlayers);
        skillSelectionController.ResetSelectionStates(validPlayers);

        foreach (PlayerHealth player in validPlayers)
        {
            if (player == null)
            {
                continue;
            }

            PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

            if (skillController != null)
            {
                skillController.ResetSkillHistory();
            }
        }

        spawnController.RespawnAllPlayersWithoutOverlap(validPlayers);

        showWaitingRoomUI();

        isRoundEnding = false;

        Debug.Log("[RoundFlowController] Returned to waiting state.");
        Debug.Log("[RoundFlowController] Press Enter or ZL to ready.");

        logReadyStates();
    }

    private int CountAliveDebugSpectatorDummies()
    {
        PlayerHealth[] allPlayers = UnityEngine.Object.FindObjectsByType<PlayerHealth>(
            FindObjectsSortMode.None
        );

        int count = 0;
        HashSet<PlayerHealth> registeredPlayers = new(getValidPlayers());

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