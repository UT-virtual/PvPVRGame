using System.Collections.Generic;
using Fusion;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class SpectatorCameraController : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Spectator")]
    [SerializeField] private float spectatorDelay = 3.0f;
    [SerializeField] private float stickSwitchThreshold = 0.6f;

    private GameObject hiddenOverheadIconRoot;
    private bool previousOverheadIconActive;

    private BattleHudBinder battleHudBinder;

    private PlayerHealth hiddenModelTarget;
    private readonly Dictionary<Renderer, bool> previousRendererStates = new();

    private readonly List<PlayerHealth> spectatorTargets = new();

    private PlayerHealth localPlayer;
    private PlayerHealth currentTarget;
    private Transform spectatorCameraTarget;

    private float deadTimer;
    private bool wasLocalDead;
    private bool isSpectating;
    private bool switchInputHeld;
    private int targetIndex;

    private void Awake()
    {
        FindCinemachineCameraIfNeeded();

        GameObject targetObject = new GameObject("SpectatorCameraTarget");
        spectatorCameraTarget = targetObject.transform;
        battleHudBinder = FindFirstObjectByType<BattleHudBinder>();
    }

    private void Update()
    {
        FindLocalPlayerIfNeeded();

        if (localPlayer == null || localPlayer.Object == null)
        {
            ResetSpectatorState();
            return;
        }

        if (!localPlayer.IsDead)
        {
            if (isSpectating)
            {
                RestoreLocalCamera();
            }

            ResetSpectatorState();
            return;
        }

        if (!wasLocalDead)
        {
            deadTimer = 0.0f;
            wasLocalDead = true;
            isSpectating = false;
            currentTarget = null;
            switchInputHeld = false;
        }

        deadTimer += Time.deltaTime;

        if (deadTimer < spectatorDelay)
        {
            return;
        }

        RefreshSpectatorTargets();

        if (spectatorTargets.Count == 0)
        {
            return;
        }

        if (!isSpectating)
        {
            isSpectating = true;
            targetIndex = Mathf.Clamp(targetIndex, 0, spectatorTargets.Count - 1);
            ApplySpectatorTarget(targetIndex);
        }
        else if (!IsValidSpectatorTarget(currentTarget))
        {
            targetIndex = Mathf.Clamp(targetIndex, 0, spectatorTargets.Count - 1);
            ApplySpectatorTarget(targetIndex);
        }

        HandleSwitchInput();
    }

    private void LateUpdate()
    {
        UpdateSpectatorCameraTarget();
    }

    private void UpdateSpectatorCameraTarget()
    {
        if (!isSpectating)
        {
            return;
        }

        if (currentTarget == null)
        {
            return;
        }

        if (spectatorCameraTarget == null)
        {
            return;
        }

        PlayerController targetController = currentTarget.GetComponent<PlayerController>();

        if (targetController != null && targetController.HasSpectatorView)
        {
            Quaternion rotation = Quaternion.LookRotation(
                targetController.NetworkedSpectatorViewForward,
                targetController.NetworkedSpectatorViewUp
            );

            spectatorCameraTarget.SetPositionAndRotation(
                targetController.NetworkedSpectatorCameraPosition,
                rotation
            );

            return;
        }

        PlayerCamera targetCamera = currentTarget.GetComponent<PlayerCamera>();

        if (targetCamera != null && targetCamera.CameraTarget != null)
        {
            targetCamera.UpdateCameraTarget();

            spectatorCameraTarget.SetPositionAndRotation(
                targetCamera.CameraTarget.position,
                targetCamera.CameraTarget.rotation
            );
        }
    }

    private void FindCinemachineCameraIfNeeded()
    {
        if (cinemachineCamera != null)
        {
            return;
        }

        cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
    }

    private void FindLocalPlayerIfNeeded()
    {
        if (localPlayer != null &&
            localPlayer.Object != null &&
            localPlayer.Object.HasInputAuthority)
        {
            return;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (PlayerHealth player in players)
        {
            if (player == null || player.Object == null)
            {
                continue;
            }

            if (player.Object.HasInputAuthority)
            {
                localPlayer = player;
                return;
            }
        }

        localPlayer = null;
    }

    private void RefreshSpectatorTargets()
    {
        spectatorTargets.Clear();

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (PlayerHealth player in players)
        {
            if (!IsValidSpectatorTarget(player))
            {
                continue;
            }

            spectatorTargets.Add(player);
        }

        if (targetIndex >= spectatorTargets.Count)
        {
            targetIndex = 0;
        }
    }

    private bool IsValidSpectatorTarget(PlayerHealth player)
    {
        if (player == null)
        {
            return false;
        }

        if (player == localPlayer)
        {
            return false;
        }

        if (player.Object == null)
        {
            return false;
        }

        if (player.IsDead)
        {
            return false;
        }

        return true;
    }

    private void HandleSwitchInput()
    {
        int direction = ReadSwitchDirection();

        if (direction == 0)
        {
            switchInputHeld = false;
            return;
        }

        if (switchInputHeld)
        {
            return;
        }

        switchInputHeld = true;

        if (spectatorTargets.Count <= 1)
        {
            return;
        }

        targetIndex += direction;

        if (targetIndex < 0)
        {
            targetIndex = spectatorTargets.Count - 1;
        }
        else if (targetIndex >= spectatorTargets.Count)
        {
            targetIndex = 0;
        }

        ApplySpectatorTarget(targetIndex);
    }

    private int ReadSwitchDirection()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                return -1;
            }

            if (Keyboard.current.sKey.wasPressedThisFrame)
            {
                return 1;
            }
        }

        if (Gamepad.current != null)
        {
            float stickX = Gamepad.current.leftStick.ReadValue().x;

            if (stickX <= -stickSwitchThreshold)
            {
                return -1;
            }

            if (stickX >= stickSwitchThreshold)
            {
                return 1;
            }
        }

        return 0;
    }

    private void ApplySpectatorTarget(int index)
    {
        FindCinemachineCameraIfNeeded();

        if (cinemachineCamera == null)
        {
            Debug.LogWarning("[SpectatorCameraController] CinemachineCamera was not found.");
            return;
        }

        if (index < 0 || index >= spectatorTargets.Count)
        {
            return;
        }

        PlayerHealth targetHealth = spectatorTargets[index];

        if (!IsValidSpectatorTarget(targetHealth))
        {
            return;
        }

        PlayerCamera targetCamera = targetHealth.GetComponent<PlayerCamera>();
        Transform targetTransform = null;

        if (targetCamera != null)
        {
            targetCamera.UpdateCameraTarget();
            targetTransform = targetCamera.CameraTarget;
        }

        if (targetTransform == null)
        {
            targetTransform = targetHealth.transform;
        }

        currentTarget = targetHealth;

        if (battleHudBinder == null)
        {
            battleHudBinder = FindFirstObjectByType<BattleHudBinder>();
        }

        if (battleHudBinder != null)
        {
            battleHudBinder.BindToSpectatorTarget(currentTarget);
        }

        HideSpectatedTargetModel(currentTarget);

        UpdateSpectatorCameraTarget();

        cinemachineCamera.Target.TrackingTarget = spectatorCameraTarget;
        cinemachineCamera.Target.LookAtTarget = spectatorCameraTarget;

        Debug.Log($"[SpectatorCameraController] Spectating: {targetHealth.name}");
    }

    private void RestoreLocalCamera()
    {
        RestoreHiddenSpectatedTargetModel();

        FindCinemachineCameraIfNeeded();

        if (cinemachineCamera == null || localPlayer == null)
        {
            return;
        }

        PlayerCamera localCamera = localPlayer.GetComponent<PlayerCamera>();

        if (localCamera == null || localCamera.CameraTarget == null)
        {
            return;
        }

        if (battleHudBinder == null)
        {
            battleHudBinder = FindFirstObjectByType<BattleHudBinder>();
        }

        if (battleHudBinder != null)
        {
            battleHudBinder.BindToSpectatorTarget(currentTarget);
        }

        localCamera.UpdateCameraTarget();

        cinemachineCamera.Target.TrackingTarget = localCamera.CameraTarget;
        cinemachineCamera.Target.LookAtTarget = localCamera.CameraTarget;

        Debug.Log("[SpectatorCameraController] Restored local camera.");
    }

    private void ResetSpectatorState()
    {
        RestoreHiddenSpectatedTargetModel();

        deadTimer = 0.0f;
        wasLocalDead = false;
        isSpectating = false;
        switchInputHeld = false;
        currentTarget = null;
        spectatorTargets.Clear();
        targetIndex = 0;
    }

    private void HideSpectatedTargetModel(PlayerHealth target)
    {
        if (target == null)
        {
            return;
        }

        if (hiddenModelTarget == target)
        {
            return;
        }

        RestoreHiddenSpectatedTargetModel();

        hiddenModelTarget = target;
        previousRendererStates.Clear();

        Renderer[] renderers = target.BodyRenderers;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            previousRendererStates[renderer] = renderer.enabled;
            renderer.enabled = false;
        }

        hiddenOverheadIconRoot = target.OverheadIconRoot;

        if (hiddenOverheadIconRoot != null)
        {
            previousOverheadIconActive = hiddenOverheadIconRoot.activeSelf;
            hiddenOverheadIconRoot.SetActive(false);
        }

        Debug.Log($"[SpectatorCameraController] Hide spectated model: {target.name}");
    }

    private void RestoreHiddenSpectatedTargetModel()
    {
        foreach (KeyValuePair<Renderer, bool> pair in previousRendererStates)
        {
            Renderer renderer = pair.Key;

            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = pair.Value;
        }

        previousRendererStates.Clear();

        if (hiddenOverheadIconRoot != null)
        {
            hiddenOverheadIconRoot.SetActive(previousOverheadIconActive);
            hiddenOverheadIconRoot = null;
        }

        hiddenModelTarget = null;

        Debug.Log("[SpectatorCameraController] Restore spectated model.");
    }
}