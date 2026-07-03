using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class BattleStartUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Settings")]
    [SerializeField] private float countDuration = 1f;
    [SerializeField] private float engageDuration = 1.5f;
    [SerializeField] private RoundManager roundManager;

    private void Start()
    {

    }
    public IEnumerator PlaySequence()
    {
        gameObject.SetActive(true);

        canvasGroup.alpha = 1f;

        // 3 → 2 → 1
        for (int i = 3; i >= 1; i--)
        {
            yield return StartCoroutine(AnimateText(i.ToString()));
        }

        // ENGAGE表示
        yield return StartCoroutine(AnimateText("Round " + roundManager.currentRound + " Start!"));

        // フェードアウト
        float t = 0f;

        while (t < 0.5f)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.5f);
            yield return null;
        }


    }

    private IEnumerator AnimateText(string text)
    {
        countText.text = text;

        float duration = (text == "ENGAGE") ?
            engageDuration : countDuration;

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float normalized = t / duration;

            // 大きさを変化
            float scale = Mathf.Lerp(0.5f, 1.5f, normalized);
            countText.transform.localScale = Vector3.one * scale;

            // 透明度を変化
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