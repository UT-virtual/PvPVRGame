
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static RoundManager;

public class RoundEffectUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject imageObject;
    [SerializeField] private Image effectImage;

    [Header("Effect Images")]
    [SerializeField] private Sprite banJumpSprite;
    [SerializeField] private Sprite health1Sprite;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        imageObject.SetActive(false);
    }

    public void ShowEffect(RoundEffectType effect, float duration)
    {
        Sprite sprite = GetSprite(effect);

        if (sprite == null)
        {
            Debug.LogWarning($"Round effect image not set: {effect}");
            imageObject.SetActive(false);
            return;
        }

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        effectImage.sprite = sprite;

        // アニメーション開始時は小さくする
        effectImage.rectTransform.localScale = Vector3.one * 0.5f;

        imageObject.SetActive(true);

        hideCoroutine = StartCoroutine(
            PlayAnimationAndHide(duration)
        );
    }

    private IEnumerator PlayAnimationAndHide(float duration)
    {
        RectTransform rect = effectImage.rectTransform;
        float elapsed = 0f;

        // ① 小さい状態から一気に拡大
        float expandTime = 0.12f;

        while (elapsed < expandTime)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / expandTime);
            rect.localScale = Vector3.one *
                Mathf.Lerp(0.5f, 1.2f, t);

            yield return null;
        }

        // ② 少し縮んで通常サイズに戻る
        float settleTime = 0.12f;
        elapsed = 0f;

        while (elapsed < settleTime)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / settleTime);
            rect.localScale = Vector3.one *
                Mathf.Lerp(1.2f, 1.0f, t);

            yield return null;
        }

        // ③ 通常サイズで表示する時間
        float fadeOutTime = 0.35f;
        float remaining = Mathf.Max(
            0f, duration - expandTime - settleTime - fadeOutTime
        );

        yield return new WaitForSecondsRealtime(remaining);

        // ④ 徐々に透明にする
        float fadeElapsed = 0f;
        Color color = effectImage.color;
        float startAlpha = color.a;

        while (fadeElapsed < fadeOutTime)
        {
            fadeElapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(fadeElapsed / fadeOutTime);

            color.a = Mathf.Lerp(startAlpha, 0f, t);
            effectImage.color = color;

            yield return null;
        }

        // ⑤ 完全に非表示にする
        color.a = startAlpha;
        effectImage.color = color;

        imageObject.SetActive(false);
        hideCoroutine = null;
    }

    private Sprite GetSprite(RoundEffectType effect)
    {
        switch (effect)
        {
            case RoundEffectType.BanJump:
                return banJumpSprite;

            case RoundEffectType.Health1:
                return health1Sprite;

            default:
                return null;
        }
    }

}