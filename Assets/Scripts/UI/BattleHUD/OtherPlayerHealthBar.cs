using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class OtherPlayerHealthBar : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Display")]
    [SerializeField] private Transform barRoot;
    [SerializeField] private Image healthFill;
    [SerializeField] private float heightOffset = 2.0f;

    private Camera localCamera;

    public override void Spawned()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (barRoot == null || healthFill == null || playerHealth == null)
        {
            Debug.LogWarning(
                $"[{nameof(OtherPlayerHealthBar)}] Inspector references are missing.",
                this
            );
            return;
        }

        // 自分のHPバーは表示しない
        barRoot.gameObject.SetActive(!Object.HasInputAuthority);

        UpdateHealthBar();
    }

    private void LateUpdate()
    {
        if (Object == null || barRoot == null)
            return;

        // 自分のHPバーは非表示
        if (Object.HasInputAuthority)
        {
            if (barRoot.gameObject.activeSelf)
                barRoot.gameObject.SetActive(false);

            return;
        }

        if (playerHealth == null)
            return;

        if (!barRoot.gameObject.activeSelf)
            barRoot.gameObject.SetActive(true);

        if (localCamera == null)
            localCamera = Camera.main;

        // PlayerHeadIcon と同じ位置計算
        barRoot.position =
            transform.position + transform.up * heightOffset;

        if (localCamera == null)
            return;

        // HPバーだけをカメラに向ける
        Vector3 toCamera =
            localCamera.transform.position - barRoot.position;

        if (toCamera.sqrMagnitude > 0.0001f)
        {
            barRoot.rotation = Quaternion.LookRotation(
                -toCamera.normalized,
                localCamera.transform.up
            );
        }

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (playerHealth == null || healthFill == null)
            return;

        float maxHealth = playerHealth.MaxHealth;

        healthFill.fillAmount = maxHealth > 0f
            ? Mathf.Clamp01(playerHealth.CurrentHealth / maxHealth)
            : 0f;
    }
}