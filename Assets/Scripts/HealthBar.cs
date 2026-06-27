using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private float maxHealth = 10.0f;
    [SerializeField] private PlayerHealth playerHealth;

    private float currentHealth;
    private float DisplayHealth;
    private float healthLerpSpeed = 0.15f;
    public Slider HealthSlider;

    public RectTransform barTransform;
    public float shakeAmount = 5f;     // 揺れの強さ
    public float shakeDuration = 0.1f; // 揺れる時間
    private Vector3 originalPos;
    private float shakeTimer = 0f;

    void Awake()
    {
        currentHealth = maxHealth;
        DisplayHealth = maxHealth;
        HealthSlider.maxValue = maxHealth;
        HealthSlider.value = DisplayHealth;
        originalPos = barTransform.localPosition;
    }

    private void Start()
    {
        if (playerHealth != null)
        {
            maxHealth = playerHealth.MaxHealth;
            currentHealth = playerHealth.CurrentHealth;
            DisplayHealth = currentHealth;

            HealthSlider.maxValue = maxHealth;
            HealthSlider.value = DisplayHealth;

            playerHealth.OnHealthChanged += UpdateBar;
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateBar;
        }
    }

    void Update()
    {
        DisplayHealth = Mathf.Lerp(DisplayHealth, currentHealth, healthLerpSpeed);
        HealthSlider.value = DisplayHealth;

        if (shakeTimer > 0)
        {
            barTransform.localPosition = originalPos + (Vector3)Random.insideUnitCircle * shakeAmount;
            shakeTimer -= Time.deltaTime;

            if (shakeTimer <= 0)
            {
                barTransform.localPosition = originalPos; // 元の位置に戻す
            }
        }
    }

    private void UpdateBar(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;
        HealthSlider.maxValue = maxHealth;
        shakeTimer = shakeDuration;
    }
}