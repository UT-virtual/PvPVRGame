using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BgmManager : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip waitingBgm;
    [SerializeField] private AudioClip battleBgm;

    private void Update()
    {
        // RoundManagerが存在しない場合は何もしない
        if (RoundManager.Instance == null)
        {
            return;
        }

        // 待機画面のBGM再生判定
        if (RoundManager.Instance.IsWaitingForReady)
        {
            PlayBgm(waitingBgm);
        }
        // 戦闘画面のBGM再生判定
        else if (RoundManager.Instance.IsRoundPlaying)
        {
            PlayBgm(battleBgm);
        }
    }

    private void PlayBgm(AudioClip newClip)
    {
        // 既に指定されたBGMが再生中の場合は処理をスキップ（毎フレームの再生リセットを防止）
        if (audioSource.clip == newClip)
        {
            return;
        }

        audioSource.clip = newClip;
        audioSource.Play();
    }
}