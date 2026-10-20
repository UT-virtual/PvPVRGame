using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BgmManager : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip waitingBgm;
    [SerializeField] private AudioClip battleBgm;

    [Header("Climax (Last 2 Players) BGM")]
    [Tooltip("プレイヤーが残り2人になった時に流す緊張感のあるBGM")]
    [SerializeField] private AudioClip lastTwoPlayersBgm;
    [Tooltip("ON: 3人以上で開始して残り2人に減った時のみ緊張BGMにする / OFF: 最初から2人の場合も緊張BGMにする")]
    [SerializeField] private bool onlyWhenReducedFromThreeOrMore = false;
    [Tooltip("緊張BGMに切り替わる生存プレイヤー数（デフォルト: 2）")]
    [SerializeField] private int climaxPlayerCountThreshold = 2;

    [Header("Volume Settings")]
    [SerializeField, Range(0.0f, 1.0f)] private float waitingBgmVolume = 1.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float battleBgmVolume = 1.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float lastTwoPlayersBgmVolume = 1.0f;

    private int maxAlivePlayersInCurrentRound = 0;
    private bool wasRoundPlaying = false;
    private bool isClimaxBgmTriggered = false;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource != null)
        {
            audioSource.loop = true;
        }
    }

    private void Update()
    {
        if (RoundManager.Instance == null || !RoundManager.Instance.IsNetworkReady)
        {
            return;
        }

        // 待機中
        if (RoundManager.Instance.IsWaitingForReady)
        {
            ResetRoundTracking();
            PlayBgm(waitingBgm, waitingBgmVolume);
            return;
        }

        // スキル選択中・ラウンド開始演出中はラウンド追跡をリセット
        if (RoundManager.Instance.IsSkillSelecting)
        {
            ResetRoundTracking();
            return;
        }

        // バトル進行中
        if (RoundManager.Instance.IsRoundPlaying)
        {
            UpdateBattleBgm();
        }
    }

    private void UpdateBattleBgm()
    {
        int aliveCount = CountAliveBattlePlayers();

        if (!wasRoundPlaying)
        {
            wasRoundPlaying = true;
            maxAlivePlayersInCurrentRound = aliveCount;
            isClimaxBgmTriggered = false;
        }
        else if (aliveCount > maxAlivePlayersInCurrentRound)
        {
            maxAlivePlayersInCurrentRound = aliveCount;
        }

        bool canTriggerClimax = !onlyWhenReducedFromThreeOrMore || maxAlivePlayersInCurrentRound > climaxPlayerCountThreshold;

        if (canTriggerClimax && aliveCount == climaxPlayerCountThreshold && lastTwoPlayersBgm != null)
        {
            isClimaxBgmTriggered = true;
        }

        if (isClimaxBgmTriggered && lastTwoPlayersBgm != null)
        {
            PlayBgm(lastTwoPlayersBgm, lastTwoPlayersBgmVolume);
        }
        else
        {
            PlayBgm(battleBgm, battleBgmVolume);
        }
    }

    private int CountAliveBattlePlayers()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        int aliveCount = 0;

        foreach (PlayerHealth player in players)
        {
            if (player == null || player.Object == null || !player.Object.IsValid)
            {
                continue;
            }

            // 試合に参加しているプレイヤー（チーム割当済み）のみをカウント
            if (!player.HasTeamAssigned)
            {
                continue;
            }

            if (!player.IsDead)
            {
                aliveCount++;
            }
        }

        return aliveCount;
    }

    private void ResetRoundTracking()
    {
        wasRoundPlaying = false;
        maxAlivePlayersInCurrentRound = 0;
        isClimaxBgmTriggered = false;
    }

    private void PlayBgm(AudioClip newClip, float volume = 1.0f)
    {
        if (audioSource == null || newClip == null)
        {
            return;
        }

        if (audioSource.clip == newClip && audioSource.isPlaying)
        {
            audioSource.volume = volume;
            return;
        }

        audioSource.clip = newClip;
        audioSource.volume = volume;
        audioSource.Play();
    }
}