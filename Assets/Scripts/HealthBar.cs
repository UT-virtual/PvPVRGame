using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Fusion;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private int maxHealth = 10;

    private PlayerHealth playerHealth;

    private float currentHealth;
    private float displayHealth;
    [SerializeField] private float healthLerpSpeed = 0.15f;

    public Slider HealthSlider;

    public RectTransform barTransform;
    public float shakeAmount = 5f;      // 揺れの強さ
    public float shakeDuration = 0.1f;  // 揺れる時間

    private Vector3 originalPos;
    private float shakeTimer = 0f;

    void Awake()
    {
        currentHealth = maxHealth;
        displayHealth = maxHealth;

        HealthSlider.maxValue = maxHealth;
        HealthSlider.value = maxHealth;

        originalPos = barTransform.localPosition;
    }

    private void Start()
    {
        StartCoroutine(FindLocalPlayer());
    }

    private IEnumerator FindLocalPlayer()
    {
        // 自分が操作しているプレイヤーを探す
        while (playerHealth == null)
        {
            PlayerHealth[] players = FindObjectsOfType<PlayerHealth>();

            foreach (PlayerHealth health in players)
            {
                NetworkObject netObj = health.GetComponent<NetworkObject>();

                if (netObj != null && netObj.HasInputAuthority)
                {
                    playerHealth = health;

                    // 現在HPを取得（初期表示用）
                    currentHealth = maxHealth;
                    displayHealth = currentHealth;

                    HealthSlider.maxValue = maxHealth;
                    HealthSlider.value = currentHealth;

                    playerHealth.OnHealthChanged += UpdateBar;

                    Debug.Log("Local Player Found");
                    yield break;
                }
            }

            yield return null;
        }
    }

    void Update()
    {
        displayHealth = Mathf.Lerp(
            displayHealth,
            currentHealth,
            healthLerpSpeed);

        HealthSlider.value = displayHealth;

        if (shakeTimer > 0)
        {
            barTransform.localPosition =
                originalPos +
                (Vector3)Random.insideUnitCircle * shakeAmount;

            shakeTimer -= Time.deltaTime;

            if (shakeTimer <= 0)
            {
                barTransform.localPosition = originalPos;
            }
        }
    }

    private void UpdateBar(int current, int max)
    {
        maxHealth = max;
        currentHealth = current;

        HealthSlider.maxValue = maxHealth;

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