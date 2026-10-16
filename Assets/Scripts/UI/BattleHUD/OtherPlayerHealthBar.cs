
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class OtherPlayerHealthBar : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject barRoot;
    [SerializeField] private Image healthFill;

    [Header("Display")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 10f, 0f);
    [SerializeField] private bool faceCamera = true;

    private Transform cameraTransform;

    public override void Spawned()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            Debug.LogError(
                $"{name}: PlayerHealth was not found."
            );
            enabled = false;
            return;
        }

        playerHealth.OnHealthChanged += OnHealthChanged;

        // 自分の体力バーは表示しない
        if (barRoot != null)
        {
            barRoot.SetActive(!Object.HasInputAuthority);
        }

        cameraTransform = Camera.main != null
            ? Camera.main.transform
            : null;

        RefreshHealthBar();
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnHealthChanged;
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        RefreshHealthBar();
    }

    private void RefreshHealthBar()
    {
        if (playerHealth == null ||
            barRoot == null ||
            healthFill == null)
        {
            return;
        }

        bool isOtherPlayer =
            Object != null && !Object.HasInputAuthority;

        bool shouldShow =
            isOtherPlayer && !playerHealth.IsDead;

        barRoot.SetActive(shouldShow);

        if (!shouldShow)
        {
            return;
        }

        float maxHealth = playerHealth.MaxHealth;

        healthFill.fillAmount = maxHealth > 0f
            ? Mathf.Clamp01(
                playerHealth.CurrentHealth / maxHealth
              )
            : 0f;
    }


    private void LateUpdate()
    {
        if (playerHealth == null || barRoot == null)
            return;

        // プレイヤーの位置には追従するが、回転には追従しない
        barRoot.transform.position = transform.position + worldOffset;

        if (cameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                cameraTransform = cam.transform;
        }

        if (cameraTransform == null)
            return;

        // カメラの向きを基準にする
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.001f)
        {
            barRoot.transform.rotation =
                Quaternion.LookRotation(forward, Vector3.up);
        }
    }

}

