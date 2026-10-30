using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Collections;
using Unity.Cinemachine;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class NetworkLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkLauncher Instance { get; private set; }
    public NetworkRunner Runner => sessionController != null ? sessionController.Runner : null;
    [Header("Network")]
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private string roomName = "TestRoom";

    [Header("Look")]
    [SerializeField] private float keyboardLookSpeed = 90.0f;
    [SerializeField] private float mouseLookSpeed = 0.12f;
    [SerializeField] private float gamepadLookSpeed = 120.0f;
    [SerializeField] private float vrStickTurnSpeed = 150.0f;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference hmdRotationAction;
    [SerializeField] private InputActionReference hmdPositionAction;

    [SerializeField] private InputActionReference leftHandPositionAction;
    [SerializeField] private InputActionReference leftHandRotationAction;

    [SerializeField] private InputActionReference rightHandPositionAction;
    [SerializeField] private InputActionReference rightHandRotationAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private InputActionReference reloadAction;

    [SerializeField] private WaitingRoomUI waitingRoomUI;

    [Header("Connection UI")]
    [SerializeField] private GameObject connectionMenuRoot;
    [SerializeField] private TMP_Text statusTextLabel;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    [Header("Client Retry")]
    [SerializeField] private bool retryClientUntilFound = true;
    [SerializeField] private float clientRetryInterval = 1.0f;
    [SerializeField] private int maxClientRetryCount = 0;

    [Header("Auto Start")]
    [SerializeField] private bool autoStartOnLaunch = true;

    [Header("XR Rig (Scene)")]
    [SerializeField] private Transform xrOriginRoot;
    [SerializeField] private Transform xrHmd;
    [SerializeField] private Transform xrLeftController;
    [SerializeField] private Transform xrRightController;

    [Header("PC Camera (Scene)")]
    [SerializeField] private Camera sceneFollowCamera;
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Debug Spectator Dummy")]
    [SerializeField] private bool spawnDebugSpectatorDummies = false;
    [SerializeField] private int debugSpectatorDummyCount = 1;

    [Header("Skill Selection Input")]
    [SerializeField] private float skillSelectionStickThreshold = 0.6f;

    [Header("Editor VR Simulation")]
    [SerializeField] private bool forceVrSimulationInEditor = false;
    [SerializeField] private bool useKeyboardHmdSimulationInEditor = false;
    [SerializeField] private float editorHmdRotationSpeed = 90.0f;

    private NetworkConnectionUIController connectionUIController;
    private NetworkSessionController sessionController;
    private NetworkPlayerSpawner playerSpawner;
    private NetworkInputCollector inputCollector;
    private LocalSkillSelectionInputController skillSelectionInputController;
    private Transform attachedXrOriginRoot;
    private LocalPlayModeController playModeController;
    private bool presentationReady;
    private bool vrSpectating;
    private Vector3 spectatorHeadOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    public int CurrentSkillSelectionSlot =>
        skillSelectionInputController != null
            ? skillSelectionInputController.CurrentSkillSelectionSlot
            : 0;

    public int LocalSkillSelectionStep =>
        skillSelectionInputController != null
            ? skillSelectionInputController.LocalSkillSelectionStep
            : 0;

    public bool HasLocalSkillSelectionConfirmed =>
        skillSelectionInputController != null &&
        skillSelectionInputController.HasLocalSkillSelectionConfirmed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.AdoptSceneReferences(this);
            Debug.Log($"[NetworkLauncher] Reusing existing session; suppressing launcher {GetInstanceID()} in {gameObject.scene.name}.");
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        playModeController = GetComponent<LocalPlayModeController>();
        if (playModeController == null) playModeController = gameObject.AddComponent<LocalPlayModeController>();
        playModeController.SelectMode();
        InitializeControllers();
        SceneManager.sceneLoaded += HandleSceneLoaded;

        connectionUIController.InitializeButtons(
            () => StartGame(GameMode.Host),
            () => StartGame(GameMode.Client)
        );
    }

    private IEnumerator Start()
    {
        connectionUIController.SetStatusTextVisible(true);
        connectionUIController.LockButtons();
        connectionUIController.SetStatusText("起動モードを準備中...");
        yield return playModeController.Initialize();
        if (!LocalPlayModeController.IsReady || !ConfigureScenePresentation())
        {
            string error = playModeController.Error ?? "選択したモードのカメラ設定が不足しています。シーンの参照を確認してください。";
            Debug.LogError($"[NetworkLauncher] Startup stopped: {error}");
            connectionUIController.SetStatusText(error);
            yield break;
        }

        connectionUIController.SetStatusText("ホスト・クライアント選択");

        if (autoStartOnLaunch)
        {
            connectionUIController.SetConnectionMenuVisible(false);
            connectionUIController.SetStatusTextVisible(true);
            connectionUIController.LockButtons();

            StartGame(GameMode.AutoHostOrClient);
            yield break;
        }

        connectionUIController.SetConnectionMenuVisible(true);
        connectionUIController.SetStatusTextVisible(false);
        connectionUIController.UnlockButtons();
    }

    private void OnEnable()
    {
        inputCollector?.EnableActions();
    }

    private void OnDisable()
    {
        inputCollector?.DisableActions();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        connectionUIController?.DisposeButtons();
        sessionController?.Dispose();
        Instance = null;
    }

    private void Update()
    {
        if (Instance != this || !LocalPlayModeController.IsReady || !presentationReady) return;
        inputCollector.UpdateHmdAndLook();

        bool reconnectPressed = inputCollector.ReadReconnectPressed();
        if (sessionController.CanReconnect() && reconnectPressed)
        {
            sessionController.ReconnectAsClient();
            return;
        }

        if (skillSelectionInputController.UpdateIfSkillSelecting())
        {
            return;
        }

        inputCollector.QueueGameplayButtons();
    }

    private void InitializeControllers()
    {
        connectionUIController = new NetworkConnectionUIController(
            connectionMenuRoot,
            statusTextLabel,
            hostButton,
            clientButton
        );

        sessionController = new NetworkSessionController(
            this,
            this,
            playerPrefab,
            roomName,
            retryClientUntilFound,
            clientRetryInterval,
            maxClientRetryCount,
            connectionUIController,
            () => waitingRoomUI
        );

        playerSpawner = new NetworkPlayerSpawner(
            playerPrefab,
            spawnDebugSpectatorDummies,
            debugSpectatorDummyCount
        );

        inputCollector = new NetworkInputCollector(
            moveAction,
            lookAction,
            hmdRotationAction,
            hmdPositionAction,
            leftHandPositionAction,
            leftHandRotationAction,
            rightHandPositionAction,
            rightHandRotationAction,
            jumpAction,
            fireAction,
            reloadAction,
            keyboardLookSpeed,
            mouseLookSpeed,
            gamepadLookSpeed,
            vrStickTurnSpeed,
            forceVrSimulationInEditor,
            useKeyboardHmdSimulationInEditor,
            editorHmdRotationSpeed
        );

        skillSelectionInputController = new LocalSkillSelectionInputController(
            moveAction,
            skillSelectionStickThreshold,
            () => sessionController.Runner,
            () => inputCollector.LocalPlayerController
        );
    }

    public void StartGame(GameMode gameMode)
    {
        if (Instance != this || !LocalPlayModeController.IsReady || !presentationReady) return;
        sessionController.StartGame(gameMode);
    }

    private void AdoptSceneReferences(NetworkLauncher source)
    {
        waitingRoomUI = source.waitingRoomUI;
        connectionMenuRoot = source.connectionMenuRoot;
        statusTextLabel = source.statusTextLabel;
        hostButton = source.hostButton;
        clientButton = source.clientButton;
        xrOriginRoot = source.xrOriginRoot;
        xrHmd = source.xrHmd;
        xrLeftController = source.xrLeftController;
        xrRightController = source.xrRightController;
        sceneFollowCamera = source.sceneFollowCamera;
        cinemachineCamera = source.cinemachineCamera;
        attachedXrOriginRoot = null;

        connectionUIController.DisposeButtons();
        connectionUIController = new NetworkConnectionUIController(connectionMenuRoot, statusTextLabel, hostButton, clientButton);
        connectionUIController.InitializeButtons(() => StartGame(GameMode.Host), () => StartGame(GameMode.Client));
        sessionController.SetConnectionUI(connectionUIController);
        connectionUIController.SetConnectionMenuVisible(false);
        connectionUIController.SetStatusTextVisible(false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (LocalPlayModeController.IsReady) ConfigureScenePresentation();
    }

    public void PrepareForSceneReload()
    {
        presentationReady = false;
        EndVrSpectating();
        DetachXrOriginFromPlayer();
        inputCollector.ResetConnectionState();
        inputCollector.SetXrTransforms(null, null, null);
    }

    public bool RestoreScenePresentation()
    {
        inputCollector.EnableActions();
        return ConfigureScenePresentation();
    }

    public void UpdateVrSpectatorView(Vector3 eyePosition, Quaternion bodyRotation)
    {
        if (!IsVrSessionActive() || xrOriginRoot == null || xrHmd == null) return;
        if (!vrSpectating)
        {
            // Anchor the current physical head position; tracked head motion remains free.
            spectatorHeadOffset = xrOriginRoot.InverseTransformPoint(xrHmd.position);
            vrSpectating = true;
        }
        xrOriginRoot.rotation = bodyRotation;
        xrOriginRoot.position = eyePosition - xrOriginRoot.TransformVector(spectatorHeadOffset);
    }

    public void EndVrSpectating()
    {
        if (!vrSpectating) return;
        vrSpectating = false;
        if (attachedXrOriginRoot == null) return;
        attachedXrOriginRoot.localPosition = Vector3.zero;
        attachedXrOriginRoot.localRotation = Quaternion.identity;
    }

    private bool ConfigureScenePresentation()
    {
        ResolveXrReferencesIfNeeded();
        ResolvePcCameraReferencesIfNeeded();
        bool vr = IsVrSessionActive();
        Camera vrCamera = xrHmd != null ? xrHmd.GetComponent<Camera>() : null;
        presentationReady = false;
        if (vr && (xrOriginRoot == null || vrCamera == null || vrCamera == sceneFollowCamera)) return false;
        if (!vr && (sceneFollowCamera == null || cinemachineCamera == null)) return false;

        if (xrOriginRoot != null) xrOriginRoot.gameObject.SetActive(vr);
        if (vrCamera != null)
        {
            vrCamera.enabled = vr;
            AudioListener listener = vrCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = vr;
        }
        if (sceneFollowCamera != null && sceneFollowCamera != vrCamera)
        {
            sceneFollowCamera.enabled = !vr;
            AudioListener listener = sceneFollowCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = !vr;
            CinemachineBrain brain = sceneFollowCamera.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = !vr;
        }
        if (cinemachineCamera != null) cinemachineCamera.enabled = !vr;
        inputCollector.SetXrTransforms(vr ? xrHmd : null, vr ? xrLeftController : null, vr ? xrRightController : null);
        VRWorldSpaceUIFollower[] followers = FindObjectsByType<VRWorldSpaceUIFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform view = vr ? xrHmd : sceneFollowCamera != null ? sceneFollowCamera.transform : null;
        foreach (VRWorldSpaceUIFollower follower in followers) follower.SetTargetCamera(view);
        Debug.Log($"[NetworkLauncher] Presentation ready: {playModeController.Mode}, scene={SceneManager.GetActiveScene().name}.");
        presentationReady = true;
        return true;
    }

    public void OnServerSkillSelectionProgress(PlayerRef playerRef, int selectedCount)
    {
        skillSelectionInputController.OnServerSkillSelectionProgress(playerRef, selectedCount);
    }

    public void NotifyLocalPlayerSpawnedOnWaitingPlanet()
    {
        Debug.Log("[NetworkLauncher] Local player spawned on waiting planet.");

        if (waitingRoomUI == null)
        {
            waitingRoomUI = FindFirstObjectByType<WaitingRoomUI>();
        }

        if (waitingRoomUI != null)
        {
            waitingRoomUI.SetLocalPlayerSpawned(true);
        }
        else
        {
            Debug.LogWarning("[NetworkLauncher] WaitingRoomUI was not found.");
        }
    }

    public void RegisterLocalPlayer(PlayerController playerController)
    {
        inputCollector.RegisterLocalPlayer(playerController);
    }

    public void UnregisterLocalPlayer(PlayerController playerController)
    {
        bool unregistered = inputCollector.UnregisterLocalPlayer(playerController);

        if (!unregistered)
        {
            return;
        }

        DetachXrOriginFromPlayer();

        if (waitingRoomUI != null)
        {
            waitingRoomUI.SetLocalPlayerSpawned(false);
            waitingRoomUI.HideRoomUI();
        }
    }

    public void AttachXrOriginToPlayer(Transform playerTransform)
    {
        if (!IsVrSessionActive() || playerTransform == null)
        {
            return;
        }

        ResolveXrReferencesIfNeeded();

        if (xrOriginRoot == null)
        {
            Debug.LogWarning("[NetworkLauncher] XR Origin Root was not found.");
            return;
        }

        CharacterController xrCharacterController = xrOriginRoot.GetComponent<CharacterController>();
        if (xrCharacterController != null)
        {
            xrCharacterController.enabled = false;
        }

        SetXrControllerVisualsActive(false);

        xrOriginRoot.SetParent(playerTransform, false);
        xrOriginRoot.localPosition = Vector3.zero;
        xrOriginRoot.localRotation = Quaternion.identity;
        attachedXrOriginRoot = xrOriginRoot;
    }

    public void DetachXrOriginFromPlayer()
    {
        EndVrSpectating();
        if (attachedXrOriginRoot == null)
        {
            return;
        }

        SetXrControllerVisualsActive(true);

        attachedXrOriginRoot.SetParent(null, true);
        attachedXrOriginRoot = null;
    }

    private void SetXrControllerVisualsActive(bool active)
    {
        SetControllerVisualsActive(xrLeftController, active);
        SetControllerVisualsActive(xrRightController, active);
    }

    private static void SetControllerVisualsActive(Transform controllerRoot, bool active)
    {
        if (controllerRoot == null)
        {
            return;
        }

        Renderer[] renderers = controllerRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = active;
        }

        LineRenderer[] lineRenderers = controllerRoot.GetComponentsInChildren<LineRenderer>(true);
        foreach (LineRenderer lineRenderer in lineRenderers)
        {
            lineRenderer.enabled = active;
        }
    }

    public Vector2 GetLocalMoveInput()
    {
        return inputCollector != null
            ? inputCollector.LastMoveInput
            : Vector2.zero;
    }

    public void ConfigureLocalVrBodySync(VRBodyTargetSync bodySync)
    {
        ResolveXrReferencesIfNeeded();
        bool vr = IsVrSessionActive();

        if (bodySync != null)
        {
            bodySync.SetXrTransforms(vr ? xrHmd : null, vr ? xrLeftController : null, vr ? xrRightController : null);
        }

        inputCollector.SetXrTransforms(vr ? xrHmd : null, vr ? xrLeftController : null, vr ? xrRightController : null);
        if (vr) ConfigureVrUiFollowers();
    }

    private void ConfigureVrUiFollowers()
    {
        if (xrHmd == null)
        {
            return;
        }

        VRWorldSpaceUIFollower[] followers =
            FindObjectsByType<VRWorldSpaceUIFollower>(FindObjectsSortMode.None);

        foreach (VRWorldSpaceUIFollower follower in followers)
        {
            if (follower != null)
            {
                follower.SetTargetCamera(xrHmd);
            }
        }
    }

    public void ConfigureLocalPlayerCamera(PlayerCamera playerCamera)
    {
        ResolvePcCameraReferencesIfNeeded();

        if (playerCamera != null)
        {
            playerCamera.ConfigureSceneCameras(sceneFollowCamera, cinemachineCamera);
        }
    }

    private bool IsVrSessionActive()
    {
        return LocalPlayModeController.IsVirtualReality;
    }

    private void ResolveXrReferencesIfNeeded()
    {
        if (xrOriginRoot != null &&
            xrHmd != null &&
            xrLeftController != null &&
            xrRightController != null)
        {
            return;
        }

        XROrigin origin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (origin == null)
        {
            return;
        }

        if (xrOriginRoot == null)
        {
            xrOriginRoot = origin.transform;
        }

        if (xrHmd == null && origin.Camera != null)
        {
            xrHmd = origin.Camera.transform;
        }

        Transform[] children = origin.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (xrLeftController == null && child.name == "Left Controller")
            {
                xrLeftController = child;
            }

            if (xrRightController == null && child.name == "Right Controller")
            {
                xrRightController = child;
            }
        }
    }

    private void ResolvePcCameraReferencesIfNeeded()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
        }

        if (sceneFollowCamera == null)
        {
            CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>(FindObjectsInactive.Include);
            if (brain != null)
            {
                sceneFollowCamera = brain.GetComponent<Camera>();
            }
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        playerSpawner.OnPlayerJoined(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        playerSpawner.OnPlayerLeft(runner, player);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        inputCollector.CollectInput(input);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        sessionController.HandleShutdown(runner, shutdownReason);
    }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { request.Accept(); }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"[NetworkLauncher] Scene load complete, runner={runner.GetInstanceID()}, scene={SceneManager.GetActiveScene().name}");
        if (LocalPlayModeController.IsReady) ConfigureScenePresentation();
    }
    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log($"[NetworkLauncher] Scene load start, runner={runner.GetInstanceID()}");
    }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
