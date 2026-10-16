using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerIdentity))]
public class PlayerHeadIcon : NetworkBehaviour
{
    [Header("Icon")]
    [SerializeField] private Transform iconRoot;
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private float heightOffset = 2.0f;

    [Header("Visibility")]
    [SerializeField] private float visibleDistance = 10.0f;
    [SerializeField] private float visibleAngle = 70.0f;
    [SerializeField] private LayerMask obstructionMask;

    [Header("Colors")]
    [SerializeField] private Color[] playerColors =
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta,
        Color.cyan
    };

    private PlayerIdentity playerIdentity;
    private Camera localCamera;

    private void Awake()
    {
        playerIdentity = GetComponent<PlayerIdentity>();

        if (iconRoot == null)
        {
            iconRoot = transform;
        }

        if (iconRenderer == null && iconRoot != null)
        {
            iconRenderer = iconRoot.GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    public override void Spawned()
    {
        UpdateIconColor();
        SetIconVisible(false);
    }

    private void LateUpdate()
    {
        if (Object == null)
        {
            return;
        }

        if (Object.HasInputAuthority)
        {
            SetIconVisible(false);
            return;
        }

        if (iconRoot == null || iconRenderer == null)
        {
            return;
        }

        if (localCamera == null)
        {
            localCamera = Camera.main;
        }

        if (localCamera == null)
        {
            SetIconVisible(false);
            return;
        }

        UpdateIconPosition();
        UpdateIconBillboard();
        UpdateIconColor();

        bool visible = ShouldShowIcon();
        SetIconVisible(visible);
    }

    private void UpdateIconPosition()
    {
        iconRoot.position = transform.position + transform.up * heightOffset;
    }

    private void UpdateIconBillboard()
    {
        Vector3 toCamera = localCamera.transform.position - iconRoot.position;

        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        iconRoot.rotation = Quaternion.LookRotation(-toCamera.normalized, localCamera.transform.up);
    }

    private void UpdateIconColor()
    {
        if (iconRenderer == null || playerIdentity == null)
        {
            return;
        }

        if (playerColors == null || playerColors.Length == 0)
        {
            iconRenderer.color = Color.white;
            return;
        }

        int colorIndex = playerIdentity.ColorIndex % playerColors.Length;
        iconRenderer.color = playerColors[colorIndex];
    }

    private bool ShouldShowIcon()
    {
        Vector3 cameraPosition = localCamera.transform.position;
        Vector3 iconPosition = iconRoot.position;

        Vector3 toIcon = iconPosition - cameraPosition;
        float distance = toIcon.magnitude;

        if (distance > visibleDistance)
        {
            return false;
        }

        if (distance < 0.001f)
        {
            return false;
        }

        Vector3 directionToIcon = toIcon / distance;

        float dot = Vector3.Dot(localCamera.transform.forward, directionToIcon);
        float angle = Mathf.Acos(Mathf.Clamp(dot, -1.0f, 1.0f)) * Mathf.Rad2Deg;

        if (angle > visibleAngle)
        {
            return false;
        }

        if (Physics.Linecast(cameraPosition, iconPosition, obstructionMask))
        {
            return false;
        }

        return true;
    }

    private void SetIconVisible(bool visible)
    {
        if (iconRenderer == null)
        {
            return;
        }

        if (iconRenderer.enabled == visible)
        {
            return;
        }

        iconRenderer.enabled = visible;
    }
}