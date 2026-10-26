using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 地表基準の目の位置とPlayerLookの視線からカメラ追従先を更新する。
/// ローカルカメラの割り当てはNetworkPlayer、観戦時の切替はSpectatorCameraControllerが呼び出す。
/// </summary>
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerLook))]
public class PlayerCamera : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float cameraEyeHeight = 1.6f;
    [SerializeField] private float cameraForwardOffset = 0.0f;
    [SerializeField] private float cameraSideOffset = 0.0f;

    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    private PlayerMove playerMove;
    private PlayerLook playerLook;

    public Vector3 CameraPosition => GetCameraPosition();
    public Transform CameraTarget => cameraTarget;

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerLook = GetComponent<PlayerLook>();
    }

    /// <summary>
    /// シーンのCinemachineCameraをこのプレイヤーの追従先へ接続する。
    /// カメラを奪い合わないよう、ローカルプレイヤーの設定経路から呼ぶ。
    /// </summary>
    public void SetupLocalCamera()
    {
        if (cameraTarget == null)
        {
            Debug.LogError($"{name}: CameraTarget is not assigned.");
            return;
        }

        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        if (cinemachineCamera == null)
        {
            Debug.LogError($"{name}: CinemachineCamera was not found in this scene.");
            return;
        }

        cinemachineCamera.Target.TrackingTarget = cameraTarget;
        cinemachineCamera.Target.LookAtTarget = cameraTarget;

        Debug.Log($"{name}: Cinemachine target set to {cameraTarget.name}");
    }

    public void UpdateCameraTarget()
    {
        if (cameraTarget == null)
        {
            return;
        }

        Vector3 cameraPosition = GetCameraPosition();
        Vector3 cameraForward = playerLook.ViewForward;
        Vector3 cameraUp = playerLook.ViewUp;

        Quaternion cameraRotation = Quaternion.LookRotation(cameraForward, cameraUp);

        cameraTarget.SetPositionAndRotation(cameraPosition, cameraRotation);
    }

    private Vector3 GetCameraPosition()
    {
        return transform.position
            + playerMove.SurfaceUp * cameraEyeHeight
            + playerMove.AimForward * cameraForwardOffset
            + playerMove.AimRight * cameraSideOffset;
    }
}