using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ラウンドごとの候補生成、参加者の2段階の選択、時間切れ時の補完を管理する。
/// StateAuthority側で選択を確定してRoundStartControllerへ引き継ぎ、各端末には候補をRPCで配布する。
/// </summary>
public sealed class RoundSkillSelectionController
{
    private readonly MonoBehaviour coroutineRunner;

    private readonly Func<RoundManager.GamePhase> getPhase;
    private readonly Action<RoundManager.GamePhase> setPhase;

    private readonly Func<int> getCurrentRound;
    private readonly Action<int> setCurrentRound;

    private readonly Func<List<PlayerHealth>> getValidPlayers;
    private readonly Func<PlayerHealth, bool> containsPlayer;

    private readonly IReadOnlyList<PlayerSkillType> selectableRoundSkills;
    private readonly int requiredSkillSelectionCount;

    private readonly Func<float> getSkillSelectionDuration;

    private readonly RoundSkillOptionBuilder skillOptionBuilder;
    private readonly RoundSpawnController spawnController;
    private readonly RoundHealthItemController healthItemController;
    private readonly RoundProjectileCleaner projectileCleaner;
    private readonly RoundDebugLogger debugLogger;

    private readonly Action hideWaitingRoomUI;
    private readonly Action<PlayerRef, int> notifySkillSelectionProgressRpc;

    private readonly Action<
        PlayerSkillType,
        PlayerSkillType,
        PlayerSkillType,
        PlayerSkillType,
        PlayerSkillType,
        PlayerSkillType,
        PlayerSkillType,
        PlayerSkillType
    > setSkillOptionsRpc;

    private readonly Action startRoundAfterSkillSelection;
    private readonly Action stopStartRoundCoroutine;

    // 候補はNetworkedプロパティではなくRPCで配布した端末内配列。選択数と確定内容は進行側の辞書で管理する。
    private readonly PlayerSkillType[] currentSkillOptions;
    private readonly PlayerSkillType[] secondSkillOptions;

    private readonly Dictionary<PlayerHealth, int> skillSelectionCounts = new();
    private readonly Dictionary<PlayerHealth, PlayerSkillType> selectedSkills = new();
    private readonly Dictionary<PlayerHealth, PlayerSkillType> selectedSecondSkills = new();

    private Coroutine skillSelectionCoroutine;

    public RoundSkillSelectionController(
        MonoBehaviour coroutineRunner,
        Func<RoundManager.GamePhase> getPhase,
        Action<RoundManager.GamePhase> setPhase,
        Func<int> getCurrentRound,
        Action<int> setCurrentRound,
        Func<List<PlayerHealth>> getValidPlayers,
        Func<PlayerHealth, bool> containsPlayer,
        IReadOnlyList<PlayerSkillType> selectableRoundSkills,
        int skillOptionSlotCount,
        int requiredSkillSelectionCount,
        Func<float> getSkillSelectionDuration,
        RoundSkillOptionBuilder skillOptionBuilder,
        RoundSpawnController spawnController,
        RoundHealthItemController healthItemController,
        RoundProjectileCleaner projectileCleaner,
        RoundDebugLogger debugLogger,
        Action hideWaitingRoomUI,
        Action<PlayerRef, int> notifySkillSelectionProgressRpc,
        Action<
            PlayerSkillType,
            PlayerSkillType,
            PlayerSkillType,
            PlayerSkillType,
            PlayerSkillType,
            PlayerSkillType,
            PlayerSkillType,
            PlayerSkillType
        > setSkillOptionsRpc,
        Action startRoundAfterSkillSelection,
        Action stopStartRoundCoroutine
    )
    {
        this.coroutineRunner = coroutineRunner;

        this.getPhase = getPhase;
        this.setPhase = setPhase;

        this.getCurrentRound = getCurrentRound;
        this.setCurrentRound = setCurrentRound;

        this.getValidPlayers = getValidPlayers;
        this.containsPlayer = containsPlayer;

        this.selectableRoundSkills = selectableRoundSkills;
        this.requiredSkillSelectionCount = requiredSkillSelectionCount;

        this.getSkillSelectionDuration = getSkillSelectionDuration;

        this.skillOptionBuilder = skillOptionBuilder;
        this.spawnController = spawnController;
        this.healthItemController = healthItemController;
        this.projectileCleaner = projectileCleaner;
        this.debugLogger = debugLogger;

        this.hideWaitingRoomUI = hideWaitingRoomUI;
        this.notifySkillSelectionProgressRpc = notifySkillSelectionProgressRpc;
        this.setSkillOptionsRpc = setSkillOptionsRpc;

        this.startRoundAfterSkillSelection = startRoundAfterSkillSelection;
        this.stopStartRoundCoroutine = stopStartRoundCoroutine;

        currentSkillOptions = new PlayerSkillType[skillOptionSlotCount];
        secondSkillOptions = new PlayerSkillType[skillOptionSlotCount];
    }

    public int SkillOptionSlotCount => currentSkillOptions.Length;

    private bool IsSkillSelecting =>
        getPhase() == RoundManager.GamePhase.SkillSelecting;

    public PlayerSkillType GetSkillOption(int slotIndex)
    {
        return GetSkillOption(slotIndex, 0);
    }

    /// <summary>
    /// 0始まりの候補枠を返す。selectionIndexが0以下なら1回目、それ以外は2回目の候補を参照する。
    /// 範囲外の枠はNoneを返す。
    /// </summary>
    public PlayerSkillType GetSkillOption(int slotIndex, int selectionIndex)
    {
        if (slotIndex < 0 || slotIndex >= currentSkillOptions.Length)
        {
            return PlayerSkillType.None;
        }

        PlayerSkillType[] targetOptions = selectionIndex <= 0
            ? currentSkillOptions
            : secondSkillOptions;

        return targetOptions[slotIndex];
    }

    public void RegisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        skillSelectionCounts[player] = 0;
        selectedSkills[player] = PlayerSkillType.None;
        selectedSecondSkills[player] = PlayerSkillType.None;
    }

    public void UnregisterPlayer(PlayerHealth player)
    {
        if (player == null)
        {
            return;
        }

        skillSelectionCounts.Remove(player);
        selectedSkills.Remove(player);
        selectedSecondSkills.Remove(player);
    }

    public void ResetSelectionStates(IEnumerable<PlayerHealth> players)
    {
        foreach (PlayerHealth player in players)
        {
            if (player == null)
            {
                continue;
            }

            skillSelectionCounts[player] = 0;
            selectedSkills[player] = PlayerSkillType.None;
            selectedSecondSkills[player] = PlayerSkillType.None;
        }
    }

    public int GetSkillSelectionCount(PlayerHealth player)
    {
        if (player == null)
        {
            return 0;
        }

        return skillSelectionCounts.TryGetValue(player, out int count)
            ? count
            : 0;
    }

    public PlayerSkillType GetFirstSelectedSkill(PlayerHealth player)
    {
        if (player == null)
        {
            return PlayerSkillType.None;
        }

        return selectedSkills.TryGetValue(player, out PlayerSkillType skill)
            ? skill
            : PlayerSkillType.None;
    }

    public PlayerSkillType GetSecondSelectedSkill(PlayerHealth player)
    {
        if (player == null)
        {
            return PlayerSkillType.None;
        }

        return selectedSecondSkills.TryGetValue(player, out PlayerSkillType skill)
            ? skill
            : PlayerSkillType.None;
    }

    /// <summary>
    /// 進行側で古い選択・開始待ちを停止し、候補配布、残弾・アイテムの撤去、全員復活、選択初期化を行う。
    /// 新しい選択フェーズの時間切れ監視を開始する。
    /// </summary>
    public void BeginSkillSelectionForRound(int roundNumber)
    {
        StopSkillSelectionCoroutines();

        hideWaitingRoomUI();

        setCurrentRound(roundNumber);

        BuildSkillOptionsForRound();

        setPhase(RoundManager.GamePhase.SkillSelecting);

        healthItemController.ClearHealthItems();
        projectileCleaner.DespawnProjectiles();

        spawnController.RespawnAllPlayersWithoutOverlap(getValidPlayers());

        ResetSelectionStates(getValidPlayers());

        Debug.Log($"[RoundSkillSelectionController] Round {getCurrentRound()} Skill Selection Start.");
        Debug.Log($"[RoundSkillSelectionController] Select skill within {getSkillSelectionDuration()} seconds.");
        Debug.Log("[RoundSkillSelectionController] During skill selection, players cannot move.");

        skillSelectionCoroutine = coroutineRunner.StartCoroutine(SkillSelectionTimeoutCoroutine());
    }

    /// <summary>
    /// 進行側で登録参加者の現在の選択段階に対応する候補を受理する。
    /// フェーズ・範囲・空枠・同一スキルの重複を確認し、全員完了なら開始待機へ引き継ぐ。
    /// </summary>
    public void SelectSkillBySlot(PlayerHealth player, int slotIndex)
    {
        if (!IsSkillSelecting)
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        if (!containsPlayer(player))
        {
            Debug.LogWarning($"[RoundSkillSelectionController] Skill selection ignored. Player is not registered: {player.gameObject.name}");
            return;
        }

        int selectionIndex = GetSkillSelectionCount(player);

        if (selectionIndex >= requiredSkillSelectionCount)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= currentSkillOptions.Length)
        {
            Debug.LogWarning($"[RoundSkillSelectionController] Invalid skill slot: {slotIndex + 1}");
            return;
        }

        PlayerSkillType skill = GetSkillOption(slotIndex, selectionIndex);

        if (skill == PlayerSkillType.None)
        {
            Debug.LogWarning($"[RoundSkillSelectionController] Empty skill slot: {slotIndex + 1}");
            return;
        }

        SelectSkill(player, skill, false);
    }

    public void HandleRequestSelectSkillBySlot(PlayerRef playerRef, int slotIndex)
    {
        if (!IsSkillSelecting)
        {
            Debug.Log(
                $"[RoundSkillSelectionController] Skill RPC ignored. " +
                $"Phase={getPhase()}, PlayerRef={playerRef}, Slot={slotIndex + 1}"
            );
            return;
        }

        PlayerHealth player = FindRegisteredPlayerByRef(playerRef);

        if (player == null)
        {
            Debug.LogWarning(
                $"[RoundSkillSelectionController] Skill RPC ignored. Player was not found. " +
                $"PlayerRef={playerRef}, Slot={slotIndex + 1}"
            );
            return;
        }

        int beforeCount = GetSkillSelectionCount(player);

        SelectSkillBySlot(player, slotIndex);

        int afterCount = GetSkillSelectionCount(player);

        Debug.Log(
            $"[RoundSkillSelectionController] Skill RPC processed. " +
            $"Player={player.name}, PlayerRef={playerRef}, Slot={slotIndex + 1}, " +
            $"Count={beforeCount}->{afterCount}"
        );

        notifySkillSelectionProgressRpc(playerRef, afterCount);
    }

    public void ApplySkillOptionsFromNetwork(
        PlayerSkillType slot0,
        PlayerSkillType slot1,
        PlayerSkillType slot2,
        PlayerSkillType slot3,
        PlayerSkillType secondSlot0,
        PlayerSkillType secondSlot1,
        PlayerSkillType secondSlot2,
        PlayerSkillType secondSlot3
    )
    {
        currentSkillOptions[0] = slot0;
        currentSkillOptions[1] = slot1;
        currentSkillOptions[2] = slot2;
        currentSkillOptions[3] = slot3;

        secondSkillOptions[0] = secondSlot0;
        secondSkillOptions[1] = secondSlot1;
        secondSkillOptions[2] = secondSlot2;
        secondSkillOptions[3] = secondSlot3;

        Debug.Log(
            $"[RoundSkillSelectionController] First Skill Options: " +
            $"1={slot0}, 2={slot1}, 3={slot2}"
        );

        Debug.Log(
            $"[RoundSkillSelectionController] Second Skill Options: " +
            $"1={secondSlot0}, 2={secondSlot1}, 3={secondSlot2}, 4={secondSlot3}"
        );
    }

    /// <summary>
    /// 有効な参加者が1人以上いて全員の選択が揃った場合に、選択タイムアウトを止めて開始待機を依頼する。
    /// </summary>
    public void TryFinishSkillSelection()
    {
        if (!IsSkillSelecting)
        {
            return;
        }

        List<PlayerHealth> validPlayers = getValidPlayers();

        if (validPlayers.Count == 0)
        {
            return;
        }

        foreach (PlayerHealth player in validPlayers)
        {
            int selectedCount = GetSkillSelectionCount(player);

            if (selectedCount < requiredSkillSelectionCount)
            {
                return;
            }
        }

        StopSkillSelectionCoroutineOnly();

        startRoundAfterSkillSelection();
    }

    public void StopSkillSelectionCoroutineOnly()
    {
        if (skillSelectionCoroutine == null)
        {
            return;
        }

        coroutineRunner.StopCoroutine(skillSelectionCoroutine);
        skillSelectionCoroutine = null;
    }

    /// <summary>
    /// 選択タイムアウトとRoundStartControllerの開始待機の両方を停止し、前フェーズの遅延処理を残さない。
    /// </summary>
    public void StopSkillSelectionCoroutines()
    {
        StopSkillSelectionCoroutineOnly();
        stopStartRoundCoroutine();
    }

    private void BuildSkillOptionsForRound()
    {
        skillOptionBuilder.BuildSkillOptions(
            selectableRoundSkills,
            currentSkillOptions,
            secondSkillOptions
        );

        setSkillOptionsRpc(
            currentSkillOptions[0],
            currentSkillOptions[1],
            currentSkillOptions[2],
            currentSkillOptions[3],
            secondSkillOptions[0],
            secondSkillOptions[1],
            secondSkillOptions[2],
            secondSkillOptions[3]
        );
    }

    private PlayerHealth FindRegisteredPlayerByRef(PlayerRef playerRef)
    {
        List<PlayerHealth> validPlayers = getValidPlayers();

        foreach (PlayerHealth player in validPlayers)
        {
            if (player == null || player.Object == null)
            {
                continue;
            }

            if (player.Object.InputAuthority == playerRef)
            {
                return player;
            }
        }

        return null;
    }

    private void SelectSkill(PlayerHealth player, PlayerSkillType skill, bool isAutoSelect)
    {
        if (player == null)
        {
            return;
        }

        int selectionIndex = GetSkillSelectionCount(player);

        if (selectionIndex >= requiredSkillSelectionCount)
        {
            return;
        }

        PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();

        if (skillController == null)
        {
            Debug.LogWarning($"[RoundSkillSelectionController] PlayerSkillController was not found: {player.gameObject.name}");
            skill = PlayerSkillType.None;
        }
        if (selectionIndex > 0 &&
            skill != PlayerSkillType.None &&
            GetFirstSelectedSkill(player) == skill)
        {
            Debug.LogWarning($"[RoundSkillSelectionController] Same skill selected twice in same round: {skill}");
            return;
        }

        if (selectionIndex == 0)
        {
            selectedSkills[player] = skill;
        }
        else
        {
            selectedSecondSkills[player] = skill;
        }

        int newSelectionCount = selectionIndex + 1;
        skillSelectionCounts[player] = newSelectionCount;

        Debug.Log(
            $"[RoundSkillSelectionController] Skill Selected: {player.gameObject.name}, " +
            $"Index={newSelectionCount}/{requiredSkillSelectionCount}, " +
            $"Skill={skill}, Auto={isAutoSelect}"
        );

        LogSkillSelectionStates();
        TryFinishSkillSelection();
    }

    private void AutoSelectMissingSkills()
    {
        List<PlayerHealth> validPlayers = getValidPlayers();

        foreach (PlayerHealth player in validPlayers)
        {
            if (player == null)
            {
                continue;
            }

            while (GetSkillSelectionCount(player) < requiredSkillSelectionCount)
            {
                int selectionIndex = GetSkillSelectionCount(player);
                PlayerSkillType autoSkill = GetAutoSelectableSkill(player, selectionIndex);

                SelectSkill(player, autoSkill, true);
            }
        }
    }

    private PlayerSkillType GetAutoSelectableSkill(PlayerHealth player, int selectionIndex)
    {
        if (player == null)
        {
            return PlayerSkillType.None;
        }

        for (int i = 0; i < SkillOptionSlotCount; i++)
        {
            PlayerSkillType skill = GetSkillOption(i, selectionIndex);

            if (skill == PlayerSkillType.None)
            {
                continue;
            }

            if (selectionIndex > 0 && GetFirstSelectedSkill(player) == skill)
            {
                continue;
            }

            return skill;
        }

        return PlayerSkillType.None;
    }

    private IEnumerator SkillSelectionTimeoutCoroutine()
    {
        yield return new WaitForSeconds(getSkillSelectionDuration());

        if (!IsSkillSelecting)
        {
            yield break;
        }

        Debug.Log("[RoundSkillSelectionController] Skill selection timeout. Auto selecting missing skills.");

        AutoSelectMissingSkills();
        TryFinishSkillSelection();
    }

    private void LogSkillSelectionStates()
    {
        debugLogger.LogSkillSelectionStates(
            getValidPlayers(),
            this,
            requiredSkillSelectionCount
        );
    }
}