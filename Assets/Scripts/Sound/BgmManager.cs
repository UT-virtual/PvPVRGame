using UnityEngine;

/// <summary>
/// RoundManagerの待機・戦闘フェーズに応じてBGMを切り替える。
/// それ以外のフェーズでは現在の再生を維持する。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BgmManager : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip waitingBgm;
    [SerializeField] private AudioClip battleBgm;

    private void Update()
    {
        if (RoundManager.Instance == null)
        {
            return;
        }

        if (RoundManager.Instance.IsWaitingForReady)
        {
            PlayBgm(waitingBgm);
        }
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