using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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

    private bool debugSpectatorDummiesSpawned;
    private readonly List<NetworkObject> debugSpectatorDummies = new();

    [Header("Skill Selection Input")]
    [SerializeField] private float skillSelectionStickThreshold = 0.6f;

    private int currentSkillSelectionSlot;
    private bool wasSkillSelecting;
    private bool skillSelectionMoveHeld;

    public int CurrentSkillSelectionSlot => currentSkillSelectionSlot;

    private bool isStartingGame;
    private GameObject runnerObject;

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    private string statusText = "ホスト・クライアント選択";

    private Vector2 queuedLookInput;
    private bool jumpQueued;
    private bool reloadQueued;
    private bool readyQueued;
    private bool skillQueued;
    private bool selectSkill1Queued;
    private bool selectSkill2Queued;
    private bool selectSkill3Queued;
    private bool selectSkill4Queued;
    private bool wasLeftTriggerPressed;

    private PlayerController localPlayerController;

    [Header("Editor VR Simulation")]
    [SerializeField] private bool forceVrSimulationInEditor = false;
    [SerializeField] private bool useKeyboardHmdSimulationInEditor = false;
    [SerializeField] private float editorHmdRotationSpeed = 90.0f;

    private Vector2 editorHmdEuler;

    private bool isVRActive
    {
        get
        {
    #if UNITY_EDITOR
            if (forceVrSimulationInEditor)
            {
                return true;
            }
    #endif
            return UnityEngine.XR.XRSettings.isDeviceActive;
        }
    }
    private Quaternion currentHMD = Quaternion.identity;

    private bool connectionButtonLocked;

    private void Awake()
    {
        /*
        if (hostButton != null)
        {
            hostButton.onClick.RemoveListener(OnHostButtonClicked);
            hostButton.onClick.AddListener(OnHostButtonClicked);
        }
        else
        {
            Debug.LogWarning("[NetworkLauncher] HostButton is not assigned.");
        }

        if (clientButton != null)
        {
            clientButton.onClick.RemoveListener(OnClientButtonClicked);
            clientButton.onClick.AddListener(OnClientButtonClicked);
        }
        else
        {
            Debug.LogWarning("[NetworkLauncher] ClientButton is not assigned.");
        }
        */
    }

    private void OnDestroy()
    {
        /*
        if (hostButton != null)
        {
            hostButton.onClick.RemoveListener(OnHostButtonClicked);
        }

        if (clientButton != null)
        {
            clientButton.onClick.RemoveListener(OnClientButtonClicked);
        }
        */
    }

    private void OnHostButtonClicked()
    {
        Debug.Log("[NetworkLauncher] Host button clicked.");

        if (connectionButtonLocked)
        {
            Debug.LogWarning("[NetworkLauncher] Host click ignored because button is locked.");
            return;
        }

        connectionButtonLocked = true;
        SetConnectionButtonsInteractable(false);

        StartGame(GameMode.Host);
    }

    private void OnClientButtonClicked()
    {
        Debug.Log("[NetworkLauncher] Client button clicked.");

        if (connectionButtonLocked)
        {
            Debug.LogWarning("[NetworkLauncher] Client click ignored because button is locked.");
            return;
        }

        connectionButtonLocked = true;
        SetConnectionButtonsInteractable(false);

        StartGame(GameMode.Client);
    }

    private void SetConnectionButtonsInteractable(bool interactable)
    {
        if (hostButton != null)
        {
            hostButton.interactable = interactable;
        }

        if (clientButton != null)
        {
            clientButton.interactable = interactable;
        }
    }

    private void Start()
    {
        UpdateStatusText();

        if (autoStartOnLaunch)
        {
            SetConnectionMenuVisible(false);
            SetStatusTextVisible(true);

            connectionButtonLocked = true;

            StartGame(GameMode.AutoHostOrClient);
            return;
        }

        SetConnectionMenuVisible(true);
        SetStatusTextVisible(false);
        SetConnectionButtonsInteractable(true);
    }

    private void OnEnable()
    {
        EnableAction(moveAction);
        EnableAction(lookAction);

        EnableAction(hmdRotationAction);
        EnableAction(hmdPositionAction);

        EnableAction(leftHandPositionAction);
        EnableAction(leftHandRotationAction);

        EnableAction(rightHandPositionAction);
        EnableAction(rightHandRotationAction);

        EnableAction(jumpAction);
        EnableAction(fireAction);
        EnableAction(reloadAction);
    }

    private void OnDisable()
    {
        DisableAction(moveAction);
        DisableAction(lookAction);

        DisableAction(hmdRotationAction);
        DisableAction(hmdPositionAction);

        DisableAction(leftHandPositionAction);
        DisableAction(leftHandRotationAction);

        DisableAction(rightHandPositionAction);
        DisableAction(rightHandRotationAction);

        DisableAction(jumpAction);
        DisableAction(fireAction);
        DisableAction(reloadAction);
    }

    private void EnableAction(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return;
        }

        actionReference.action.Enable();
    }

    private void DisableAction(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return;
        }

        actionReference.action.Disable();
    }

    private void SetStatusText(string text)
    {
        statusText = text;
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (statusTextLabel != null)
        {
            statusTextLabel.text = statusText;
        }
    }

    private void SetConnectionMenuVisible(bool visible)
    {
        if (connectionMenuRoot != null)
        {
            connectionMenuRoot.SetActive(visible);
        }
    }

    public async void StartGame(GameMode gameMode)
    {
        if (this == null)
        {
            return;
        }

        if (runner != null || isStartingGame)
        {
            return;
        }

        if (!playerPrefab.IsValid)
        {
            SetStatusText("Player Prefab is not set.");
            Debug.LogError(statusText);

            connectionButtonLocked = false;
            SetConnectionButtonsInteractable(true);
            SetConnectionMenuVisible(true);
            return;
        }

        int buildIndex = SceneManager.GetActiveScene().buildIndex;

        if (buildIndex < 0)
        {
            SetStatusText("Current scene is not in Build Settings.");
            Debug.LogError(statusText);

            connectionButtonLocked = false;
            SetConnectionButtonsInteractable(true);
            SetConnectionMenuVisible(true);
            return;
        }

        if (waitingRoomUI != null)
        {
            waitingRoomUI.SetLocalPlayerSpawned(false);
            waitingRoomUI.HideRoomUI();
        }

        isStartingGame = true;
        SetConnectionMenuVisible(false);

        SceneRef sceneRef = SceneRef.FromIndex(buildIndex);
        int attemptCount = 0;

        while (true)
        {
            if (this == null)
            {
                return;
            }

            attemptCount++;

            Debug.Log($"StartGame: {gameMode}, RoomName={roomName}, Attempt={attemptCount}");
            string gameModeJapanese =
                gameMode == GameMode.Host
                    ? "ホスト"
                    : gameMode == GameMode.Client
                        ? "クライアント"
                        : "自動接続";

            SetStatusText($"{gameModeJapanese}中...");

            CreateRunnerObject(gameMode, attemptCount);

            StartGameResult result = await runner.StartGame(new StartGameArgs()
            {
                GameMode = gameMode,
                SessionName = roomName,
                Scene = sceneRef,
                SceneManager = sceneManager,
                PlayerCount = 8
            });

            if (this == null)
            {
                return;
            }

            if (result.Ok)
            {
                SetStatusText($"ルーム名: {roomName}");
                Debug.Log(statusText);

                isStartingGame = false;

                SetConnectionMenuVisible(false);
                SetStatusTextVisible(false);

                return;
            }

            ShutdownReason shutdownReason = result.ShutdownReason;

            SetStatusText($"Failed: {shutdownReason}");
            Debug.LogError($"Failed to start Fusion: {shutdownReason}");

            CleanupFailedRunner();

            bool shouldRetry =
                gameMode == GameMode.Client &&
                retryClientUntilFound &&
                shutdownReason == ShutdownReason.GameNotFound &&
                (maxClientRetryCount <= 0 || attemptCount < maxClientRetryCount);

            if (!shouldRetry)
            {
                isStartingGame = false;
                connectionButtonLocked = false;
                SetConnectionMenuVisible(true);
                SetConnectionButtonsInteractable(true);
                return;
            }

            statusText =
                $"Room not found. Retrying Client... " +
                $"Attempt {attemptCount}, Room: {roomName}";

            Debug.Log(
                $"[NetworkLauncher] GameNotFound. Retry Client after {clientRetryInterval} sec. " +
                $"RoomName={roomName}, Attempt={attemptCount}"
            );

            int delayMilliseconds = Mathf.RoundToInt(Mathf.Max(0.1f, clientRetryInterval) * 1000.0f);
            await System.Threading.Tasks.Task.Delay(delayMilliseconds);
        }
    }

    private void Update()
    {
        UpdateHMD();

        Vector2 lookInput = ReadLookInput();

        queuedLookInput += lookInput;

        if (localPlayerController != null)
        {
            localPlayerController.ApplyLocalLook(lookInput, isVRActive, currentHMD);
        }

        bool isSkillSelecting =
            RoundManager.Instance != null &&
            RoundManager.Instance.IsSkillSelecting;

        if (isSkillSelecting)
        {
            UpdateSkillSelectionInput();

            // スキル選択中は、EnterやAをReady/Jump等として扱わない。
            return;
        }

        ResetSkillSelectionInputState();

        if (ReadJumpPressed())
        {
            jumpQueued = true;
        }

        if (ReadReloadPressed())
        {
            reloadQueued = true;
        }

        if (ReadReadyPressed())
        {
            readyQueued = true;
        }

        if (ReadSkillPressed())
        {
            skillQueued = true;
        }
    }

    private void UpdateSkillSelectionInput()
    {
        Vector2 navigateInput = ReadSkillSelectionNavigateInput();

        if (!wasSkillSelecting)
        {
            currentSkillSelectionSlot = 0;

            // 移動中にスキル選択へ入った瞬間、左スティック入力で勝手に動かないようにする。
            skillSelectionMoveHeld = IsSkillSelectionNavigateActive(navigateInput);
            wasSkillSelecting = true;
            return;
        }

        if (!IsSkillSelectionNavigateActive(navigateInput))
        {
            skillSelectionMoveHeld = false;
        }
        else if (!skillSelectionMoveHeld)
        {
            MoveSkillSelectionCursor(navigateInput);
            skillSelectionMoveHeld = true;
        }

        if (ReadSkillConfirmPressed())
        {
            QueueSelectedSkillSlot();
        }
    }

    private void ResetSkillSelectionInputState()
    {
        wasSkillSelecting = false;
        skillSelectionMoveHeld = false;
    }

    private Vector2 ReadSkillSelectionNavigateInput()
    {
        Vector2 input = Vector2.zero;

        if (moveAction != null && moveAction.action != null)
        {
            Vector2 actionInput = moveAction.action.ReadValue<Vector2>();

            if (actionInput.sqrMagnitude >= skillSelectionStickThreshold * skillSelectionStickThreshold)
            {
                return actionInput;
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.wasPressedThisFrame)
            {
                input.y += 1.0f;
            }

            if (Keyboard.current.sKey.wasPressedThisFrame)
            {
                input.y -= 1.0f;
            }

            if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                input.x += 1.0f;
            }

            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                input.x -= 1.0f;
            }
        }

        if (input.sqrMagnitude > 0.01f)
        {
            return input;
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();

            if (stick.sqrMagnitude >= skillSelectionStickThreshold * skillSelectionStickThreshold)
            {
                return stick;
            }
        }

        return Vector2.zero;
    }

    private bool IsSkillSelectionNavigateActive(Vector2 input)
    {
        return input.sqrMagnitude >= skillSelectionStickThreshold * skillSelectionStickThreshold;
    }

    private void MoveSkillSelectionCursor(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
        {
            return;
        }

        int row = currentSkillSelectionSlot / 2;
        int column = currentSkillSelectionSlot % 2;

        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            column += input.x > 0.0f ? 1 : -1;
        }
        else
        {
            // UI上では上が0行目、下が1行目
            row += input.y > 0.0f ? -1 : 1;
        }

        row = Mathf.Clamp(row, 0, 1);
        column = Mathf.Clamp(column, 0, 1);

        currentSkillSelectionSlot = row * 2 + column;

        Debug.Log($"[NetworkLauncher] Skill cursor: {currentSkillSelectionSlot + 1}");
    }

    private bool ReadSkillConfirmPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private void QueueSelectedSkillSlot()
    {
        switch (currentSkillSelectionSlot)
        {
            case 0:
                selectSkill1Queued = true;
                break;

            case 1:
                selectSkill2Queued = true;
                break;

            case 2:
                selectSkill3Queued = true;
                break;

            case 3:
                selectSkill4Queued = true;
                break;

            default:
                Debug.LogWarning($"[NetworkLauncher] Invalid skill selection slot: {currentSkillSelectionSlot}");
                break;
        }

        Debug.Log($"[NetworkLauncher] Confirm skill slot: {currentSkillSelectionSlot + 1}");
    }

    private void UpdateHMD()
    {
    #if UNITY_EDITOR
        if (forceVrSimulationInEditor && useKeyboardHmdSimulationInEditor)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.iKey.isPressed)
                {
                    editorHmdEuler.x -= editorHmdRotationSpeed * Time.deltaTime;
                }

                if (Keyboard.current.kKey.isPressed)
                {
                    editorHmdEuler.x += editorHmdRotationSpeed * Time.deltaTime;
                }

                if (Keyboard.current.jKey.isPressed)
                {
                    editorHmdEuler.y -= editorHmdRotationSpeed * Time.deltaTime;
                }

                if (Keyboard.current.lKey.isPressed)
                {
                    editorHmdEuler.y += editorHmdRotationSpeed * Time.deltaTime;
                }
            }

            editorHmdEuler.x = Mathf.Clamp(editorHmdEuler.x, -85.0f, 85.0f);
            currentHMD = Quaternion.Euler(editorHmdEuler.x, editorHmdEuler.y, 0.0f);

            Debug.Log($"[Editor HMD] Euler={currentHMD.eulerAngles}");

            return;
        }
    #endif

        if (!isVRActive)
        {
            currentHMD = Quaternion.identity;
            return;
        }

        if (hmdRotationAction == null || hmdRotationAction.action == null)
        {
            currentHMD = Quaternion.identity;
            return;
        }

        currentHMD = hmdRotationAction.action.ReadValue<Quaternion>();

        Debug.Log($"[HMD] Euler={currentHMD.eulerAngles}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"OnPlayerJoined: {player}, IsServer: {runner.IsServer}");

        if (!runner.IsServer)
        {
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(player);
        Quaternion spawnRotation = Quaternion.identity;

        NetworkObject playerObject = runner.Spawn(
            playerPrefab,
            spawnPosition,
            spawnRotation,
            player
        );

        int visualIndex = spawnedPlayers.Count;

        PlayerIdentity playerIdentity = playerObject.GetComponent<PlayerIdentity>();

        if (playerIdentity != null)
        {
            playerIdentity.SetIdentity(
                visualIndex + 1,
                visualIndex
            );
        }
        else
        {
            Debug.LogWarning($"{playerObject.name}: PlayerIdentity is not attached.");
        }

        spawnedPlayers.Add(player, playerObject);

        Debug.Log($"Spawned player: {player}, VisualIndex={visualIndex}");

        if (runner.IsServer)
    {
        SpawnDebugSpectatorDummiesIfNeeded(runner);
    }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
        {
            runner.Despawn(playerObject);
            spawnedPlayers.Remove(player);
        }
    }

    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        if (player.RawEncoded % 2 == 0)
        {
            return new Vector3(-5.0f, 5.0f, 0.0f);
        }

        return new Vector3(5.0f, 5.0f, 0.0f);
    }

    private void SpawnDebugSpectatorDummiesIfNeeded(NetworkRunner runner)
    {
        if (!spawnDebugSpectatorDummies)
        {
            return;
        }

        if (debugSpectatorDummiesSpawned)
        {
            return;
        }

        if (runner == null || !runner.IsServer)
        {
            return;
        }

        if (!playerPrefab.IsValid)
        {
            Debug.LogWarning("[NetworkLauncher] Cannot spawn debug spectator dummy because Player Prefab is not valid.");
            return;
        }

        debugSpectatorDummiesSpawned = true;

        int count = Mathf.Max(0, debugSpectatorDummyCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 position = GetDebugSpectatorDummyPosition(i);
            Quaternion rotation = Quaternion.identity;

            NetworkObject dummyObject = runner.Spawn(
                playerPrefab,
                position,
                rotation,
                PlayerRef.None
            );

            dummyObject.name = $"DebugSpectatorDummy_{i + 1}";

            debugSpectatorDummies.Add(dummyObject);

            PlayerIdentity playerIdentity = dummyObject.GetComponent<PlayerIdentity>();

            if (playerIdentity != null)
            {
                int visualIndex = spawnedPlayers.Count + i;
                playerIdentity.SetIdentity(visualIndex + 1, visualIndex);
            }

            Debug.Log($"[NetworkLauncher] Spawned debug spectator dummy: {dummyObject.name}");
        }
    }

    private Vector3 GetDebugSpectatorDummyPosition(int index)
    {
        float angle = index * 120.0f * Mathf.Deg2Rad;
        float radius = 8.0f;

        return new Vector3(
            Mathf.Cos(angle) * radius,
            5.0f,
            Mathf.Sin(angle) * radius
        );
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput();

        data.IsVR = isVRActive;
        data.HMDRotation = currentHMD;
        data.HMDPosition = ReadVector3Action(hmdPositionAction);

        data.LeftHandPosition = ReadVector3Action(leftHandPositionAction);
        data.LeftHandRotation = ReadQuaternionAction(leftHandRotationAction);
        data.HasLeftHand = HasActionValue(leftHandPositionAction) ? (byte)1 : (byte)0;

        data.RightHandPosition = ReadVector3Action(rightHandPositionAction);
        data.RightHandRotation = ReadQuaternionAction(rightHandRotationAction);
        data.HasRightHand = HasActionValue(rightHandPositionAction) ? (byte)1 : (byte)0;

        data.MoveInput = ReadMoveInput();
        data.LookInput = queuedLookInput;

        if (localPlayerController != null)
        {
            data.AimForward = localPlayerController.GetNetworkAimForward();
            data.ViewForward = localPlayerController.GetNetworkViewForward();
            data.HasLookDirection = 1;
        }
        else
        {
            data.AimForward = Vector3.zero;
            data.ViewForward = Vector3.zero;
            data.HasLookDirection = 0;
        }

        NetworkButtons buttons = default;

        buttons.Set((int)PlayerInputButton.Jump, jumpQueued);
        buttons.Set((int)PlayerInputButton.Fire, ReadFireHeldInput());
        buttons.Set((int)PlayerInputButton.Reload, reloadQueued);
        buttons.Set((int)PlayerInputButton.Ready, readyQueued);
        buttons.Set((int)PlayerInputButton.Skill, skillQueued);
        buttons.Set((int)PlayerInputButton.SelectSkill1, selectSkill1Queued);
        buttons.Set((int)PlayerInputButton.SelectSkill2, selectSkill2Queued);
        buttons.Set((int)PlayerInputButton.SelectSkill3, selectSkill3Queued);
        buttons.Set((int)PlayerInputButton.SelectSkill4, selectSkill4Queued);

        data.Buttons = buttons;

        input.Set(data);

        queuedLookInput = Vector2.zero;
        jumpQueued = false;
        reloadQueued = false;
        readyQueued = false;
        skillQueued = false;
        selectSkill1Queued = false;
        selectSkill2Queued = false;
        selectSkill3Queued = false;
        selectSkill4Queued = false;
    }

    private Vector2 ReadMoveInput()
    {
        if (moveAction != null && moveAction.action != null)
        {
            Vector2 actionMove = moveAction.action.ReadValue<Vector2>();

            if (actionMove.sqrMagnitude > 0.0001f)
            {
                return actionMove;
            }
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();

            if (stick.sqrMagnitude > 0.0001f)
            {
                return stick;
            }
        }

    #if UNITY_EDITOR
        if (forceVrSimulationInEditor)
        {
            return Vector2.zero;
        }
    #endif

        Vector2 moveInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
            {
                moveInput.y += 1.0f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                moveInput.y -= 1.0f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                moveInput.x -= 1.0f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                moveInput.x += 1.0f;
            }
        }

        return Vector2.ClampMagnitude(moveInput, 1.0f);
    }

    private Vector2 ReadLookInput()
    {
        if (lookAction != null && lookAction.action != null)
        {
            Vector2 rawLook = lookAction.action.ReadValue<Vector2>();

            if (isVRActive)
            {
                return new Vector2(rawLook.x * vrStickTurnSpeed * Time.deltaTime, 0.0f);
            }

            if (Mouse.current != null && lookAction.action.activeControl?.device == Mouse.current)
            {
                return rawLook * mouseLookSpeed;
            }

            return rawLook * gamepadLookSpeed * Time.deltaTime;
        }

        Vector2 lookInput = Vector2.zero;

        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            lookInput.x += mouseDelta.x * mouseLookSpeed;
            lookInput.y += mouseDelta.y * mouseLookSpeed;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed)
            {
                lookInput.x -= keyboardLookSpeed * Time.deltaTime;
            }

            if (Keyboard.current.rightArrowKey.isPressed)
            {
                lookInput.x += keyboardLookSpeed * Time.deltaTime;
            }

            if (Keyboard.current.upArrowKey.isPressed)
            {
                lookInput.y += keyboardLookSpeed * Time.deltaTime;
            }

            if (Keyboard.current.downArrowKey.isPressed)
            {
                lookInput.y -= keyboardLookSpeed * Time.deltaTime;
            }
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.rightStick.ReadValue();

            lookInput.x += stick.x * gamepadLookSpeed * Time.deltaTime;
            lookInput.y += stick.y * gamepadLookSpeed * Time.deltaTime;
        }

        return lookInput;
    }

    private bool ReadJumpPressed()
    {
        if (jumpAction != null && jumpAction.action != null && jumpAction.action.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadReloadPressed()
    {
        if (reloadAction != null && reloadAction.action != null && reloadAction.action.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadReadyPressed()
    {
        bool readyPressed = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                readyPressed = true;
            }

            if (Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                readyPressed = true;
            }
        }

        if (Gamepad.current != null)
        {
            float leftTriggerValue = Gamepad.current.leftTrigger.ReadValue();
            bool leftTriggerPressed = leftTriggerValue > 0.5f;

            if (leftTriggerPressed && !wasLeftTriggerPressed)
            {
                readyPressed = true;
                reloadQueued = true;
            }

            wasLeftTriggerPressed = leftTriggerPressed;
        }
        else
        {
            wasLeftTriggerPressed = false;
        }

        return readyPressed;
    }

    private bool ReadSkillPressed()
    {
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadSelectSkill1Pressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                return true;
            }

            if (Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadSelectSkill2Pressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                return true;
            }

            if (Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Gamepad.current != null && Gamepad.current.dpad.right.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadSelectSkill3Pressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                return true;
            }

            if (Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadSelectSkill4Pressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                return true;
            }

            if (Keyboard.current.numpad4Key.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Gamepad.current != null && Gamepad.current.dpad.left.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadFireHeldInput()
    {
        if (fireAction != null && fireAction.action != null)
        {
            InputControl control = fireAction.action.activeControl;

            if (control is UnityEngine.InputSystem.Controls.ButtonControl)
            {
                return fireAction.action.IsPressed();
            }

            return fireAction.action.ReadValue<float>() > 0.5f;
        }

        if (Keyboard.current != null && Keyboard.current.jKey.isPressed)
        {
            return true;
        }

        if (Gamepad.current != null)
        {
            return Gamepad.current.rightTrigger.ReadValue() > 0.5f;
        }

        return false;
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
        localPlayerController = playerController;
        Debug.Log($"Registered local player: {playerController.name}");
    }

    public void UnregisterLocalPlayer(PlayerController playerController)
    {
        if (localPlayerController == playerController)
        {
            localPlayerController = null;
        }

        if (waitingRoomUI != null)
        {
            waitingRoomUI.SetLocalPlayerSpawned(false);
            waitingRoomUI.HideRoomUI();
        }
    }
    
    private void CleanupFailedRunner()
    {
        if (runner != null)
        {
            runner.RemoveCallbacks(this);
        }

        if (runnerObject != null)
        {
            Destroy(runnerObject);
        }
        else
        {
            if (runner != null)
            {
                Destroy(runner);
            }

            if (sceneManager != null)
            {
                Destroy(sceneManager);
            }
        }

        runner = null;
        sceneManager = null;
        runnerObject = null;
    }

    private void CreateRunnerObject(GameMode gameMode, int attemptCount)
    {
        CleanupFailedRunner();

        runnerObject = new GameObject($"NetworkRunner_{gameMode}_Attempt{attemptCount}");
        DontDestroyOnLoad(runnerObject);

        runner = runnerObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
    }

    private void SetStatusTextVisible(bool visible)
    {
        if (statusTextLabel != null)
        {
            statusTextLabel.gameObject.SetActive(visible);
        }
    }

    private Vector3 ReadVector3Action(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return Vector3.zero;
        }

        return actionReference.action.ReadValue<Vector3>();
    }

    private Quaternion ReadQuaternionAction(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return Quaternion.identity;
        }

        Quaternion value = actionReference.action.ReadValue<Quaternion>();

        if (value.x == 0.0f &&
            value.y == 0.0f &&
            value.z == 0.0f &&
            value.w == 0.0f)
        {
            return Quaternion.identity;
        }

        return value;
    }

    private bool HasActionValue(InputActionReference actionReference)
    {
        return actionReference != null && actionReference.action != null;
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
    public void OnConnectedToServer(NetworkRunner runner) {}
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { request.Accept(); }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {}
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
    public void OnSceneLoadDone(NetworkRunner runner) {}
    public void OnSceneLoadStart(NetworkRunner runner) {}
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
}