
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FinalDuelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image finalDuelImage;

    [Header("Display Settings")]
    [SerializeField] private float displayDuration = 3.0f;
    [SerializeField] private float fadeDuration = 0.5f;

    private Coroutine displayCoroutine;

    private void Awake()
    {
        if (finalDuelImage != null)
        {
            Color color = finalDuelImage.color;
            color.a = 0f;
            finalDuelImage.color = color;
            finalDuelImage.enabled = false;
        }
    }

    public void ShowFinalDuel()
    {
        if (finalDuelImage == null)
        {
            Debug.LogWarning("[FinalDuelUI] Image is not assigned.");
            return;
        }

        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }

        finalDuelImage.enabled = true;

        Color color = finalDuelImage.color;
        color.a = 1f;
        finalDuelImage.color = color;

        displayCoroutine = StartCoroutine(DisplayRoutine());
    }

    private IEnumerator DisplayRoutine()
    {
        finalDuelImage.enabled = true;

        Color color = finalDuelImage.color;
        color.a = 1f;
        finalDuelImage.color = color;

        // 指定時間表示
        yield return new WaitForSeconds(displayDuration);

        // フェードアウト
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            color.a = 1f - Mathf.Clamp01(
                elapsed / fadeDuration
            );

            finalDuelImage.color = color;

            yield return null;
        }

        color.a = 0f;
        finalDuelImage.color = color;

        finalDuelImage.enabled = false;
        displayCoroutine = null;
    }
}
