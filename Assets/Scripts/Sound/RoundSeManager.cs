using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RoundSeManager : MonoBehaviour
{
    public static RoundSeManager Instance { get; private set; }

    public enum AllRoundsEndSeTiming
    {
        [InspectorName("最終ラウンド決着時に『全ラウンド終了SE』を鳴らす（通常ラウンド終了SEの代わり）")]
        OnFinalRoundEndedInsteadOfNormal,

        [InspectorName("最終リザルト画面表示時に『全ラウンド終了SE』を鳴らす（決着時は通常ラウンド終了SE）")]
        OnFinalResultShown,

        [InspectorName("最終ラウンド決着時と最終リザルト表示時の両方で鳴らす")]
        Both
    }

    [Header("Audio Source (2D Overall SE)")]
    [SerializeField] private AudioSource seAudioSource;

    [Header("CountDown & Start SE (全体)")]
    [Tooltip("最初のカウントダウンSE（3, 2, 1）")]
    [SerializeField] private SoundCue countdownSe;
    [Tooltip("ラウンドスタートSE（Round X Start!）")]
    [SerializeField] private SoundCue roundStartSe;

    [Header("Player Defeated SE (全体)")]
    [Tooltip("プレイヤーが撃破された時に全体に流れるSE")]
    [SerializeField] private SoundCue playerDefeatedSe;

    [Header("Round / Match End SE (全体)")]
    [Tooltip("各ラウンドが終了した時に全体に流れるSE")]
    [SerializeField] private SoundCue roundEndSe;
    [Tooltip("全ラウンドが終了した時に全体に流れる、より終了感のあるSE")]
    [SerializeField] private SoundCue allRoundsEndSe;
    [Tooltip("全ラウンド終了SEを鳴らすタイミングの設定")]
    [SerializeField] private AllRoundsEndSeTiming allRoundsEndSeTiming = AllRoundsEndSeTiming.OnFinalRoundEndedInsteadOfNormal;

    [Header("References (未設定でも自動検索されます)")]
    [SerializeField] private BattleStartUI battleStartUI;
    [SerializeField] private RoundManager roundManager;

    private bool isSubscribedToBattleStartUI;
    private bool isSubscribedToRoundManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (seAudioSource == null)
        {
            seAudioSource = GetComponent<AudioSource>();
        }

        if (seAudioSource != null)
        {
            seAudioSource.playOnAwake = false;
            seAudioSource.spatialBlend = 0.0f; // 全体SEのため2D再生
        }
    }

    private void Update()
    {
        EnsureSubscriptions();
    }

    private void OnDestroy()
    {
        UnsubscribeAll();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureSubscriptions()
    {
        if (!isSubscribedToBattleStartUI)
        {
            if (battleStartUI == null)
            {
                battleStartUI = FindFirstObjectByType<BattleStartUI>(FindObjectsInactive.Include);
            }
            if (battleStartUI != null)
            {
                battleStartUI.OnCountdownTick += HandleCountdownTick;
                battleStartUI.OnRoundStartAnnounced += HandleRoundStartAnnounced;
                isSubscribedToBattleStartUI = true;
            }
        }

        if (!isSubscribedToRoundManager)
        {
            if (roundManager == null)
            {
                roundManager = RoundManager.Instance != null
                    ? RoundManager.Instance
                    : FindFirstObjectByType<RoundManager>();
            }
            if (roundManager != null)
            {
                roundManager.OnRoundEnded += HandleRoundEnded;
                roundManager.OnAllRoundsEnded += HandleAllRoundsEnded;
                isSubscribedToRoundManager = true;
            }
        }
    }

    private void UnsubscribeAll()
    {
        if (battleStartUI != null && isSubscribedToBattleStartUI)
        {
            battleStartUI.OnCountdownTick -= HandleCountdownTick;
            battleStartUI.OnRoundStartAnnounced -= HandleRoundStartAnnounced;
            isSubscribedToBattleStartUI = false;
        }

        if (roundManager != null && isSubscribedToRoundManager)
        {
            roundManager.OnRoundEnded -= HandleRoundEnded;
            roundManager.OnAllRoundsEnded -= HandleAllRoundsEnded;
            isSubscribedToRoundManager = false;
        }
    }

    // 1. 最初のカウントダウンSE（全体）
    private void HandleCountdownTick(int count)
    {
        countdownSe?.PlayOneShot(seAudioSource);
    }

    // 2. スタートSE（全体）
    private void HandleRoundStartAnnounced(int roundNumber)
    {
        roundStartSe?.PlayOneShot(seAudioSource);
    }

    // 3. プレイヤーが撃破された時のSE（全体）
    public void NotifyPlayerDefeated(PlayerHealth deadPlayer)
    {
        if (RoundManager.Instance != null)
        {
            // 待機中やスキル選択中の状態リセットでは鳴らさない
            if (RoundManager.Instance.IsWaitingForReady || RoundManager.Instance.IsSkillSelecting)
            {
                return;
            }
        }

        playerDefeatedSe?.PlayOneShot(seAudioSource);
    }

    // 4. ラウンド終了時のSE（全体）
    private void HandleRoundEnded(int completedRound, bool isFinalRound)
    {
        if (isFinalRound)
        {
            if (allRoundsEndSeTiming == AllRoundsEndSeTiming.OnFinalRoundEndedInsteadOfNormal ||
                allRoundsEndSeTiming == AllRoundsEndSeTiming.Both)
            {
                if (allRoundsEndSe != null && allRoundsEndSe.IsValid)
                {
                    allRoundsEndSe.PlayOneShot(seAudioSource);
                    return;
                }
            }
        }

        roundEndSe?.PlayOneShot(seAudioSource);
    }

    // 5. 全ラウンド終了時のSE（全体）
    private void HandleAllRoundsEnded(int winnerTeamMask)
    {
        if (allRoundsEndSeTiming == AllRoundsEndSeTiming.OnFinalResultShown ||
            allRoundsEndSeTiming == AllRoundsEndSeTiming.Both)
        {
            allRoundsEndSe?.PlayOneShot(seAudioSource);
        }
    }
}