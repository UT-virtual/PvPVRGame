using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class KillNotificationUI : MonoBehaviour
{
    [SerializeField] private Image killImage;
    [SerializeField] private float displayDuration = 2.0f;
    [SerializeField] private float fadeDuration = 0.5f;

    private Coroutine displayCoroutine;

    private void Awake()
    {
        HideImage();
    }

    public void ShowKillNotification()
    {
        if (killImage == null)
        {
            return;
        }

        // 表示処理が重複した場合は前の処理を停止
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
        }

        killImage.enabled = true;

        Color color = killImage.color;
        color.a = 1f;
        killImage.color = color;

        displayCoroutine = StartCoroutine(DisplayRoutine());
    }

    private IEnumerator DisplayRoutine()
    {
        yield return new WaitForSeconds(displayDuration);

        float elapsed = 0f;
        Color color = killImage.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            killImage.color = color;

            yield return null;
        }

        HideImage();
        displayCoroutine = null;
    }

    public void HideKillNotification()
    {
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }

        HideImage();
    }

    private void HideImage()
    {
        if (killImage == null)
        {
            return;
        }

        Color color = killImage.color;
        color.a = 0f;
        killImage.color = color;

        killImage.enabled = false;
    }
}