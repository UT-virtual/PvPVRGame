using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerLook))]
public class PlayerCamera : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float cameraEyeHeight = 1.6f;
    [SerializeField] private float cameraForwardOffset = 0.0f;
    [SerializeField] private float cameraSideOffset = 0.0f;

    private CinemachineCamera cinemachineCamera;
    private Camera sceneFollowCamera;
    private CinemachineBrain cinemachineBrain;

    private PlayerMove playerMove;
    private PlayerLook playerLook;

    public Vector3 CameraPosition => GetCameraPosition();
    public Transform CameraTarget => cameraTarget;

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerLook = GetComponent<PlayerLook>();
    }

    public void ConfigureSceneCameras(
        Camera followCamera,
        CinemachineCamera cineCamera)
    {
        sceneFollowCamera = followCamera;
        cinemachineCamera = cineCamera;
        cinemachineBrain = sceneFollowCamera != null
            ? sceneFollowCamera.GetComponent<CinemachineBrain>()
            : null;
    }

    public void SetupLocalCamera()
    {
        if (cameraTarget == null)
        {
            Debug.LogError($"{name}: CameraTarget is not assigned.");
            return;
        }

        if (IsVrActive())
        {
            ConfigureVrCamera();
            return;
        }

        ConfigurePcCamera();
    }

    public void UpdateCameraTarget()
    {
        if (IsVrActive())
        {
            return;
        }

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

    private void ConfigureVrCamera()
    {
        ResolvePcCameraReferences();

        if (cinemachineCamera != null)
        {
            cinemachineCamera.enabled = false;
            cinemachineCamera.Target.TrackingTarget = null;
            cinemachineCamera.Target.LookAtTarget = null;
        }

        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = false;
        }

        if (sceneFollowCamera != null)
        {
            sceneFollowCamera.enabled = false;
        }
    }

    private void ConfigurePcCamera()
    {
        ResolvePcCameraReferences();

        if (cinemachineCamera == null)
        {
            Debug.LogError($"{name}: CinemachineCamera was not found in this scene.");
            return;
        }

        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = true;
        }

        if (sceneFollowCamera != null)
        {
            sceneFollowCamera.enabled = true;
        }

        cinemachineCamera.enabled = true;
        cinemachineCamera.Target.TrackingTarget = cameraTarget;
        cinemachineCamera.Target.LookAtTarget = cameraTarget;

        UpdateCameraTarget();
    }

    private void ResolvePcCameraReferences()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        if (sceneFollowCamera == null)
        {
            CinemachineBrain foundBrain = FindFirstObjectByType<CinemachineBrain>();
            if (foundBrain != null)
            {
                cinemachineBrain = foundBrain;
                sceneFollowCamera = foundBrain.GetComponent<Camera>();
            }
        }

        if (cinemachineBrain == null && sceneFollowCamera != null)
        {
            cinemachineBrain = sceneFollowCamera.GetComponent<CinemachineBrain>();
        }
    }

    private static bool IsVrActive()
    {
        return UnityEngine.XR.XRSettings.isDeviceActive;
    }

    private Vector3 GetCameraPosition()
    {
        return transform.position
            + playerMove.SurfaceUp * cameraEyeHeight
            + playerMove.AimForward * cameraForwardOffset
            + playerMove.AimRight * cameraSideOffset;
    }
}
