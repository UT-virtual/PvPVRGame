using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Fusion;

/// <summary>
/// 接続先のPlayerHealthの変更イベントからHPバーを更新し、表示値の補間と揺れを行う。
/// ローカルプレイヤーの探索と、観戦先への接続切替に対応する。
/// </summary>
public class HealthBar : MonoBehaviour
{
    [SerializeField] private float maxHealth = 10.0f;
    [SerializeField] private PlayerHealth playerHealth;

    private float currentHealth;
    private float displayHealth;

    [SerializeField] private float healthLerpSpeed = 0.15f;

    public Slider HealthSlider;
    private Image fillImage;

    public RectTransform barTransform;
    public float shakeAmount = 5f;
    public float shakeDuration = 0.1f;

    private Vector3 originalPos;
    private float shakeTimer = 0f;
    private Coroutine findLocalPlayerCoroutine;

    private void Awake()
    {
        currentHealth = maxHealth;
        displayHealth = maxHealth;

        if (HealthSlider != null)
        {
            HealthSlider.maxValue = maxHealth;
            HealthSlider.value = displayHealth;

            if (HealthSlider.fillRect != null)
            {
                fillImage = HealthSlider.fillRect.GetComponent<Image>();
            }
        }

        if (barTransform != null)
        {
            originalPos = barTransform.localPosition;
        }
    }

    private void Start()
    {
        if (playerHealth != null)
        {
            SetupPlayerHealth(playerHealth);
            return;
        }

        findLocalPlayerCoroutine = StartCoroutine(FindLocalPlayer());
    }

    private IEnumerator FindLocalPlayer()
    {
        while (playerHealth == null)
        {
            PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

            foreach (PlayerHealth health in players)
            {
                NetworkObject netObj = health.GetComponent<NetworkObject>();

                if (netObj != null && netObj.HasInputAuthority)
                {
                    SetupPlayerHealth(health);
                    Debug.Log($"[HealthBar] Local Player Found: {health.name}");
                    yield break;
                }
            }

            yield return null;
        }

        findLocalPlayerCoroutine = null;
    }

    /// <summary>
    /// 以前のHP購読とローカル探索を解除し、新しい接続先の値を揺れなしで表示へ反映する。nullなら表示値を消去する。
    /// </summary>
    public void SetupPlayerHealth(PlayerHealth health)
    {
        if (findLocalPlayerCoroutine != null)
        {
            StopCoroutine(findLocalPlayerCoroutine);
            findLocalPlayerCoroutine = null;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateBar;
        }

        playerHealth = health;

        if (playerHealth == null)
        {
            currentHealth = displayHealth = 0f;
            ApplySliderValue(0f);
            return;
        }

        playerHealth.OnHealthChanged -= UpdateBar;
        playerHealth.OnHealthChanged += UpdateBar;

        UpdateBar(playerHealth.CurrentHealth, playerHealth.MaxHealth, false);

        Debug.Log(
            $"[HealthBar] Bound to {playerHealth.name}. " +
            $"HP={playerHealth.CurrentHealth}/{playerHealth.MaxHealth}"
        );
    }

    private void Update()
    {
        if (currentHealth <= 0.0f)
        {
            displayHealth = 0.0f;
        }
        else if (currentHealth <= 1.0f)
        {
            // HPが1以下なら補間せず、正確な値を表示する
            displayHealth = currentHealth;
        }
        else
        {
            displayHealth = Mathf.Lerp(
                displayHealth,
                currentHealth,
                healthLerpSpeed
            );

            if (Mathf.Abs(displayHealth - currentHealth) < 0.01f)
            {
                displayHealth = currentHealth;
            }
        }

        ApplySliderValue(displayHealth);

        if (shakeTimer > 0)
        {
            if (barTransform != null)
            {
                barTransform.localPosition =
                    originalPos +
                    (Vector3)Random.insideUnitCircle * shakeAmount;
            }

            shakeTimer -= Time.deltaTime;

            if (shakeTimer <= 0 && barTransform != null)
            {
                barTransform.localPosition = originalPos;
            }
        }
    }

    private void UpdateBar(float current, float max)
    {
        UpdateBar(current, max, true);
    }

    private void UpdateBar(float current, float max, bool shake)
    {
        maxHealth = max;
        currentHealth = Mathf.Clamp(current, 0.0f, maxHealth);

        if (currentHealth <= 1.0f)
        {
            displayHealth = currentHealth;
        }

        ApplySliderValue(displayHealth);

        if (shake)
        {
            shakeTimer = shakeDuration;
        }

    }

    private void OnDestroy()
    {
        if (findLocalPlayerCoroutine != null)
        {
            StopCoroutine(findLocalPlayerCoroutine);
            findLocalPlayerCoroutine = null;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateBar;
        }
    }

    private void ApplySliderValue(float value)
    {
        float clampedValue = Mathf.Clamp(value, 0.0f, maxHealth);

        if (HealthSlider != null)
        {
            HealthSlider.minValue = 0f;
            HealthSlider.maxValue = maxHealth;
            HealthSlider.value = clampedValue;

        }

        if (fillImage != null)
        {
            fillImage.enabled = clampedValue > 0.001f;
        }
    }
}