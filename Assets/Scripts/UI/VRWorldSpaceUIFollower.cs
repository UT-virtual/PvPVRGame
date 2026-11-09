using UnityEngine;
using Unity.XR.CoreUtils;

[DefaultExecutionOrder(10000)]
public class VRWorldSpaceUIFollower : MonoBehaviour
{
    // RoundBattleStatusの生成Canvasと、Scene内の既存Canvasで同じ奥行きを使う。
    public const float SharedDistance = 2.0f;
    [SerializeField] private Transform targetCamera;

    [Header("Placement")]
    [SerializeField] private float distance = SharedDistance;
    [SerializeField] private float verticalOffset = -0.2f;

    [Header("Follow")]
    [SerializeField] private float directionFollowSpeed = 6.0f;
    [SerializeField] private bool followPitch = true;
    [SerializeField] private bool rotate180Y = false;

    private Vector3 smoothedForward;
    private bool initialized;
    private bool depthConfigured;

    private void Awake()
    {
        // 頭上HPなどのワールドUIではなく、この画面用Canvasだけを対象にする。
        if (GetComponent<ScreenUIForeground>() == null) gameObject.AddComponent<ScreenUIForeground>();
    }

    private void ConfigureVrDepthIfReady()
    {
        // Awake順やXR起動の待ち時間に依存せず、選択モード確定後に一度だけ適用する。
        if (depthConfigured || !LocalPlayModeController.IsReady) return;
        depthConfigured = true;
        if (!LocalPlayModeController.IsVirtualReality) return;

        // 旧1m Canvasを遠ざけても、画面上の大きさと上下位置は維持する。
        float ratio = SharedDistance / Mathf.Max(0.01f, distance);
        transform.localScale *= ratio;
        verticalOffset *= ratio;
        distance = SharedDistance;
    }

    public void SetTargetCamera(Transform cameraTransform)
    {
        targetCamera = cameraTransform;
        initialized = false;
    }

    private void LateUpdate()
    {
        ConfigureVrDepthIfReady();
        Transform cameraTransform = ResolveActiveCameraTransform();
        if (cameraTransform == null)
        {
            return;
        }

        Vector3 targetForward = cameraTransform.forward;

        if (!followPitch)
        {
            targetForward.y = 0.0f;
        }

        if (targetForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        targetForward.Normalize();

        if (!initialized)
        {
            smoothedForward = targetForward;
            initialized = true;
        }

        float t = 1.0f - Mathf.Exp(-directionFollowSpeed * Time.deltaTime);

        smoothedForward = Vector3.Slerp(
            smoothedForward,
            targetForward,
            t
        );

        smoothedForward.Normalize();

        Vector3 targetPosition =
            cameraTransform.position +
            smoothedForward * distance +
            cameraTransform.up * verticalOffset;

        transform.position = targetPosition;

        Quaternion targetRotation =
            Quaternion.LookRotation(smoothedForward, cameraTransform.up);

        if (rotate180Y)
        {
            targetRotation *= Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }

        transform.rotation = targetRotation;
    }

    private Transform ResolveActiveCameraTransform()
    {
        if (IsUsableCameraTransform(targetCamera))
        {
            return targetCamera;
        }

        XROrigin xrOrigin = LocalPlayModeController.IsVirtualReality ? FindFirstObjectByType<XROrigin>() : null;
        if (xrOrigin != null &&
            xrOrigin.Camera != null &&
            xrOrigin.Camera.enabled &&
            xrOrigin.Camera.gameObject.activeInHierarchy)
        {
            return xrOrigin.Camera.transform;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null &&
            mainCamera.enabled &&
            mainCamera.gameObject.activeInHierarchy)
        {
            return mainCamera.transform;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null &&
                camera.enabled &&
                camera.gameObject.activeInHierarchy)
            {
                return camera.transform;
            }
        }

        return null;
    }

    private static bool IsUsableCameraTransform(Transform cameraTransform)
    {
        if (cameraTransform == null)
        {
            return false;
        }

        Camera camera = cameraTransform.GetComponent<Camera>();
        if (camera == null)
        {
            return cameraTransform.gameObject.activeInHierarchy;
        }

        return camera.enabled && camera.gameObject.activeInHierarchy;
    }
}
