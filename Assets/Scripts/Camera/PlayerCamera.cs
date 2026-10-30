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
    private PlayerController playerController;

    public Vector3 CameraPosition => GetCameraPosition();
    public Transform CameraTarget => cameraTarget;

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerLook = GetComponent<PlayerLook>();
        playerController = GetComponent<PlayerController>();
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

        if (LocalPlayModeController.IsVirtualReality)
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
        if (playerController != null && playerController.Object != null &&
            !playerController.Object.HasInputAuthority && !playerController.Object.HasStateAuthority &&
            playerController.HasSpectatorView)
        {
            cameraPosition = playerController.NetworkedSpectatorCameraPosition;
            cameraForward = playerController.NetworkedSpectatorViewForward;
            cameraUp = playerController.NetworkedSpectatorViewUp;
        }
        Quaternion cameraRotation = Quaternion.LookRotation(cameraForward, cameraUp);

        cameraTarget.SetPositionAndRotation(cameraPosition, cameraRotation);
    }

    private void ConfigureVrCamera()
    {
        // Keep the fallback camera if no usable XR camera exists.
        Unity.XR.CoreUtils.XROrigin origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin == null || origin.Camera == null || !origin.Camera.isActiveAndEnabled)
        {
            Debug.LogError($"{name}: VR camera is missing or disabled.");
            return;
        }
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
            AudioListener listener = sceneFollowCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
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
            AudioListener listener = sceneFollowCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = true;
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
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
        }

        if (sceneFollowCamera == null)
        {
            CinemachineBrain foundBrain = FindFirstObjectByType<CinemachineBrain>(FindObjectsInactive.Include);
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

    private bool IsVrActive()
    {
        if (playerController == null || playerController.Object == null || playerController.Object.HasInputAuthority)
            return LocalPlayModeController.IsVirtualReality;
        return playerController.IsCurrentInputVR;
    }

    private Vector3 GetCameraPosition()
    {
        return transform.position
            + playerMove.SurfaceUp * cameraEyeHeight
            + playerMove.AimForward * cameraForwardOffset
            + playerMove.AimRight * cameraSideOffset;
    }
}
