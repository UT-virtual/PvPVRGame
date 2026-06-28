using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Fusion;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private float maxHealth = 10.0f;
    [SerializeField] private PlayerHealth playerHealth;

    private float currentHealth;
    private float displayHealth;

    [SerializeField] private float healthLerpSpeed = 0.15f;

    public Slider HealthSlider;

    public RectTransform barTransform;
    public float shakeAmount = 5f;     // 揺れの強さ
    public float shakeDuration = 0.1f; // 揺れる時間

    private Vector3 originalPos;
    private float shakeTimer = 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
        displayHealth = maxHealth;

        if (HealthSlider != null)
        {
            HealthSlider.maxValue = maxHealth;
            HealthSlider.value = displayHealth;
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

        StartCoroutine(FindLocalPlayer());
    }

    private IEnumerator FindLocalPlayer()
    {
        while (playerHealth == null)
        {
            PlayerHealth[] players = FindObjectsOfType<PlayerHealth>();

            foreach (PlayerHealth health in players)
            {
                NetworkObject netObj = health.GetComponent<NetworkObject>();

                if (netObj != null && netObj.HasInputAuthority)
                {
                    SetupPlayerHealth(health);

                    Debug.Log("Local Player Found");
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void SetupPlayerHealth(PlayerHealth health)
    {
        playerHealth = health;

        maxHealth = playerHealth.MaxHealth;
        currentHealth = playerHealth.CurrentHealth;
        displayHealth = currentHealth;

        if (HealthSlider != null)
        {
            HealthSlider.maxValue = maxHealth;
            HealthSlider.value = displayHealth;
        }

        playerHealth.OnHealthChanged += UpdateBar;
    }

    private void Update()
    {
        displayHealth = Mathf.Lerp(
            displayHealth,
            currentHealth,
            healthLerpSpeed
        );

        if (HealthSlider != null)
        {
            HealthSlider.value = displayHealth;
        }

        if (shakeTimer > 0)
        {
            if (barTransform != null)
            {
                barTransform.localPosition =
                    originalPos +
                    (Vector3)Random.insideUnitCircle * shakeAmount;
            }

            shakeTimer -= Time.deltaTime;

            if (shakeTimer <= 0)
            {
                if (barTransform != null)
                {
                    barTransform.localPosition = originalPos;
                }
            }
        }
    }

    private void UpdateBar(float current, float max)
    {
        maxHealth = max;
        currentHealth = current;

        if (HealthSlider != null)
        {
            HealthSlider.maxValue = maxHealth;
        }

        shakeTimer = shakeDuration;
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateBar;
        }
    }
}