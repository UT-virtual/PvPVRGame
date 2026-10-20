using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// RoundStartingへの遷移に合わせてカウントダウンと開始文言の演出を再生する。
/// フェーズ遷移はRoundStartControllerが行い、開始文言は同期フェーズが戦闘へ移った後に表示する。
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

        // ローカルで3秒を数え直さず、同期された開始期限から残りの表示を決める。
        float interval = Mathf.Max(0.01f, countDuration);
        while (roundManager.IsRoundStarting)
        {
            float remaining = roundManager.RoundStartRemainingSeconds;
            countText.text = Mathf.Max(1, Mathf.CeilToInt(remaining / interval)).ToString();
            float normalized = 1f - (remaining % interval) / interval;
            countText.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.5f, normalized);
            Color color = countText.color;
            color.a = Mathf.Lerp(1f, 0f, normalized);
            countText.color = color;
            yield return null;
        }

        if (!roundManager.IsRoundPlaying)
        {
            canvasGroup.alpha = 0.0f;
            yield break;
        }

        yield return StartCoroutine(AnimateText("Round " + roundManager.CurrentRound + " Start!", engageDuration));

        float t = 0f;

        while (t < 0.5f)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.5f);
            yield return null;
        }


    }

    private IEnumerator AnimateText(string text, float duration, bool isCountdown = false)
    {
        countText.text = text;

        float t = 0f;

        while (t < duration && (!isCountdown || roundManager.IsRoundStarting))
        {
            t += Time.deltaTime;

            float normalized = t / duration;

            float scale = Mathf.Lerp(0.5f, 1.5f, normalized);
            countText.transform.localScale = Vector3.one * scale;

            Color color = countText.color;
            color.a = Mathf.Lerp(1f, 0f, normalized);
            countText.color = color;

            yield return null;
        }

        // 次の表示のために透明度を戻す
        Color reset = countText.color;
        reset.a = 1f;
        countText.color = reset;
    }
}