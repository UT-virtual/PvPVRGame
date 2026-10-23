
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

    [Header("Visibility")]
    [SerializeField] private float visibleDistance = 10.0f;
    [SerializeField] private float visibleAngle = 70.0f;
    [SerializeField] private LayerMask obstructionMask;

    private Camera localCamera;

    public override void Spawned()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (barRoot == null || healthFill == null || playerHealth == null)
        {
            Debug.LogWarning(
                "[OtherPlayerHealthBar] Inspector references are missing.",
                this
            );
            return;
        }

        // 自分のHPバーは表示しない
        SetBarVisible(!Object.HasInputAuthority);

        UpdateHealthBar();
    }

    private void LateUpdate()
    {
        if (Object == null || barRoot == null || playerHealth == null)
            return;

        // 自分のHPバーは非表示
        if (Object.HasInputAuthority)
        {
            SetBarVisible(false);
            return;
        }

        // ローカルカメラを取得
        if (localCamera == null)
            localCamera = Camera.main;

        if (localCamera == null)
        {
            SetBarVisible(false);
            return;
        }

        // プレイヤーの上方向にHPバーを配置
        barRoot.position =
            transform.position + transform.up * heightOffset;

        // カメラに向ける
        UpdateBillboard();

        // HPの残量を更新
        UpdateHealthBar();

        // 距離・視野角・障害物による表示判定
        SetBarVisible(ShouldShowHealthBar());
    }

    private void UpdateBillboard()
    {
        Vector3 toCamera =
            localCamera.transform.position - barRoot.position;

        if (toCamera.sqrMagnitude < 0.0001f)
            return;

        barRoot.rotation = Quaternion.LookRotation(
            -toCamera.normalized,
            localCamera.transform.up
        );
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

    private bool ShouldShowHealthBar()
    {
        if (localCamera == null || barRoot == null)
            return false;

        Vector3 cameraPosition = localCamera.transform.position;
        Vector3 barPosition = barRoot.position;

        Vector3 toBar = barPosition - cameraPosition;
        float distance = toBar.magnitude;

        // 距離制限
        if (distance > visibleDistance || distance < 0.001f)
            return false;

        Vector3 directionToBar = toBar / distance;

        // 視野角制限
        float dot = Vector3.Dot(
            localCamera.transform.forward,
            directionToBar
        );

        float angle = Mathf.Acos(
            Mathf.Clamp(dot, -1.0f, 1.0f)
        ) * Mathf.Rad2Deg;

        if (angle > visibleAngle)
            return false;

        // 遮蔽物があれば非表示
        if (Physics.Linecast(
            cameraPosition,
            barPosition,
            obstructionMask
        ))
        {
            return false;
        }

        return true;
    }

    private void SetBarVisible(bool visible)
    {
        if (barRoot != null &&
            barRoot.gameObject.activeSelf != visible)
        {
            barRoot.gameObject.SetActive(visible);
        }
    }
}
