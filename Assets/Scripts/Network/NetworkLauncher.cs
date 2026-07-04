using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class NetworkLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
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
        InitializeControllers();

        connectionUIController.InitializeButtons(
            () => StartGame(GameMode.Host),
            () => StartGame(GameMode.Client)
        );
    }

    private void Start()
    {
        connectionUIController.SetStatusText("ホスト・クライアント選択");

        if (autoStartOnLaunch)
        {
            connectionUIController.SetConnectionMenuVisible(false);
            connectionUIController.SetStatusTextVisible(true);
            connectionUIController.LockButtons();

            StartGame(GameMode.AutoHostOrClient);
            return;
        }

        connectionUIController.SetConnectionMenuVisible(true);
        connectionUIController.SetStatusTextVisible(false);
        connectionUIController.SetConnectionButtonsInteractable(true);
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
        connectionUIController?.DisposeButtons();
    }

    private void Update()
    {
        inputCollector.UpdateHmdAndLook();

        if (skillSelectionInputController.UpdateIfSkillSelecting())
        {
            return;
        }

        if (sessionController.CanReconnectFromWaitingRoom() &&
            inputCollector.ReadReconnectPressed())
        {
            sessionController.ReconnectAsClient();
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
        sessionController.StartGame(gameMode);
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

        if (waitingRoomUI != null)
        {
            waitingRoomUI.SetLocalPlayerSpawned(false);
            waitingRoomUI.HideRoomUI();
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
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { request.Accept(); }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
