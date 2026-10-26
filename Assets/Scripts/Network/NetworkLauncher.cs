using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 接続UI、セッション、プレイヤー生成、入力収集の窓口をまとめる。
/// Updateでローカル入力を蓄積し、Fusionのコールバックを各担当Controllerへ振り分ける。
/// </summary>
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
    private NetworkParticipantRegistry participantRegistry;
    private LocalParticipantRoleController participantRoleController;
    private SpectatorCameraController spectatorController;

    public int ParticipantRevision => participantRegistry.Revision;
    public IReadOnlyDictionary<PlayerRef, ParticipantRole> ParticipantRoles => participantRegistry.Roles;

    public ParticipantRole LocalRole => participantRoleController.LocalRole;
    public bool IsConnected => participantRoleController.IsConnected;
    public bool IsLocalCombatant => participantRoleController.IsLocalCombatant;
    public bool IsDedicatedSpectator => participantRoleController.IsDedicatedSpectator;
    public bool CanChooseRole => participantRoleController.CanChooseRole;
    public bool IsRoleRequestPending => participantRoleController.IsRoleRequestPending;
    public string RoleStatus => participantRoleController.RoleStatus;

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
        sessionController?.Dispose();
    }

    private void Update()
    {
        UpdateRoleSelection();
        if (sessionController.CanReconnectFromWaitingRoom() && inputCollector.ReadReconnectPressed())
        {
            ReconnectAsClient();
            return;
        }

        if (!IsLocalCombatant || IsRoleRequestPending)
        {
            inputCollector.ClearPendingInput();
            skillSelectionInputController.ResetSkillSelectionInputState();
            return;
        }

        inputCollector.UpdateHmdAndLook();
        if (!skillSelectionInputController.UpdateIfSkillSelecting())
        {
            inputCollector.QueueGameplayButtons();
        }
    }

    private void UpdateRoleSelection()
    {
        participantRoleController.Update();
        bool lockCursor = IsConnected;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;
    }

    public void RequestRole(ParticipantRole role) => participantRoleController.RequestRole(role);

    public bool TryChangeParticipantRole(NetworkRunner runner, PlayerRef player,
        ParticipantRole role, int requestId, bool isWaiting, bool initialAssignment, out string message) =>
        participantRegistry.TryChangeRole(runner, player, role, requestId, isWaiting, out message, initialAssignment);

    public ParticipantRole GetParticipantRole(PlayerRef player) => participantRegistry.GetRole(player);

    public void OnParticipantRoleResult(PlayerRef player, int requestId, ParticipantRole role, string message) =>
        participantRoleController.OnResult(player, requestId, role, message);

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

        sessionController.ConnectionReset += ResetLocalSessionState;

        skillSelectionInputController = new LocalSkillSelectionInputController(
            moveAction,
            skillSelectionStickThreshold,
            () => sessionController.Runner
        );

        participantRegistry = new NetworkParticipantRegistry(playerSpawner);
        participantRoleController = new LocalParticipantRoleController(
            () => sessionController.IsStartingGame || sessionController.IsReconnecting ? null : sessionController.Runner,
            inputCollector.ClearPendingInput,
            skillSelectionInputController.ResetSkillSelectionInputState
        );
        spectatorController = FindFirstObjectByType<SpectatorCameraController>();
        if (spectatorController == null)
        {
            spectatorController = gameObject.AddComponent<SpectatorCameraController>();
        }
    }

    public async void StartGame(GameMode gameMode)
    {
        await sessionController.StartGameAsync(gameMode);
    }

    private async void ReconnectAsClient()
    {
        await sessionController.ReconnectAsClientAsync();
    }

    private void ResetLocalSessionState()
    {
        participantRoleController?.ResetSession();
        participantRegistry?.ResetSession();
        inputCollector.ResetLocalState();
        skillSelectionInputController?.ResetSkillSelectionInputState();
        playerSpawner.ResetSession();
    }

    public void OnServerSkillSelectionProgress(PlayerRef playerRef, int selectedCount, int requestStep, PlayerSkillType firstSkill, int selectionVersion)
    {
        skillSelectionInputController.OnServerSkillSelectionProgress(playerRef, selectedCount, requestStep, firstSkill, selectionVersion);
    }

    /// <summary>
    /// ローカルプレイヤーの生成・復活を待機UIへ通知する。
    /// この通知だけで待機フェーズに変更することはない。
    /// </summary>
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
        participantRegistry.RegisterConnection(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        participantRegistry.UnregisterConnection(runner, player);
    }

    /// <summary>
    /// Fusionが入力を要求したタイミングで、Updateから蓄積したローカル入力を送信データへ移す。
    /// </summary>
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (IsLocalCombatant && !IsRoleRequestPending)
        {
            inputCollector.CollectInput(input);
        }
        else
        {
            input.Set(default(PlayerNetworkInput));
        }
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public async void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        await sessionController.HandleDisconnectedAsync(runner, shutdownReason.ToString());
    }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public async void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        await sessionController.HandleDisconnectedAsync(runner, reason.ToString());
    }
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
