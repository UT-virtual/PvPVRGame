
using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 同期されたラウンド開始時刻に合わせてカウントダウンを表示する。
/// カウントダウンと戦闘開始時にSE通知イベントを発行する。
/// </summary>
public class BattleStartUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Settings")]
    [SerializeField] private float countDuration = 1f;
    [SerializeField] private float engageDuration = 1.5f;
    [SerializeField] private RoundManager roundManager;

    public event Action<int> OnCountdownTick;
    public event Action<int> OnRoundStartAnnounced;

    public IEnumerator PlaySequence()
    {
        if (roundManager == null)
        {
            roundManager = RoundManager.Instance;
        }

        if (roundManager == null || canvasGroup == null || countText == null)
        {
            Debug.LogWarning("[BattleStartUI] Required references are missing.");
            yield break;
        }

        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        float interval = Mathf.Max(0.01f, countDuration);
        int lastAnnouncedCountdownNumber = int.MaxValue;

        // ネットワークで同期された残り時間に基づいて表示する。
        while (roundManager.IsRoundStarting)
        {
            float remainingSeconds = roundManager.RoundStartRemainingSeconds;
            int countdownNumber = Mathf.Max(
                1,
                Mathf.CeilToInt(remainingSeconds / interval)
            );

            countText.text = countdownNumber.ToString();

            // 同期時刻の補正で表示が戻っても、同じSEを繰り返さない。
            if (countdownNumber < lastAnnouncedCountdownNumber)
            {
                OnCountdownTick?.Invoke(countdownNumber);
                lastAnnouncedCountdownNumber = countdownNumber;
            }

            float normalized = 1f - (remainingSeconds % interval) / interval;

            countText.transform.localScale =
                Vector3.one * Mathf.Lerp(0.5f, 1.5f, normalized);

            Color color = countText.color;
            color.a = Mathf.Lerp(1f, 0f, normalized);
            countText.color = color;

            yield return null;
        }

        // 戦闘以外のフェーズに移った場合は開始SEを鳴らさない。
        if (!roundManager.IsRoundPlaying)
        {
            canvasGroup.alpha = 0f;
            yield break;
        }

        OnRoundStartAnnounced?.Invoke(roundManager.CurrentRound);

        yield return StartCoroutine(
            AnimateText(
                "Round " + roundManager.CurrentRound + " Start!",
                engageDuration
            )
        );

        float elapsedSeconds = 0f;

        while (elapsedSeconds < 0.5f)
        {
            elapsedSeconds += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(
                1f,
                0f,
                elapsedSeconds / 0.5f
            );
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    private IEnumerator AnimateText(string text, float duration)
    {
        countText.text = text;

        float elapsedSeconds = 0f;

        while (elapsedSeconds < duration)
        {
            elapsedSeconds += Time.deltaTime;

            float normalized = elapsedSeconds / duration;

            float scale = Mathf.Lerp(0.5f, 1.5f, normalized);
            countText.transform.localScale = Vector3.one * scale;

            Color color = countText.color;
            color.a = Mathf.Lerp(1f, 0f, normalized);
            countText.color = color;

            yield return null;
        }

        Color resetColor = countText.color;
        resetColor.a = 1f;
        countText.color = resetColor;
    }
}
