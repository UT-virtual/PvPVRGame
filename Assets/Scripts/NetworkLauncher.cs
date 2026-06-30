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

    private bool isVRActive => UnityEngine.XR.XRSettings.isDeviceActive;
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
        EnableAction(jumpAction);
        EnableAction(fireAction);
        EnableAction(reloadAction);
    }

    private void OnDisable()
    {
        DisableAction(moveAction);
        DisableAction(lookAction);
        DisableAction(hmdRotationAction);
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
                SceneManager = sceneManager
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

        if (ReadJumpPressed())
        {
            jumpQueued = true;
        }

        if (ReadReloadPressed())
        {
            reloadQueued = true;

            if (isVRActive)
            {
                readyQueued = true;
            }
        }

        if (ReadReadyPressed())
        {
            readyQueued = true;
        }

        if (ReadSkillPressed())
        {
            skillQueued = true;
        }

        if (ReadSelectSkill1Pressed())
        {
            selectSkill1Queued = true;
        }

        if (ReadSelectSkill2Pressed())
        {
            selectSkill2Queued = true;
        }

        if (ReadSelectSkill3Pressed())
        {
            selectSkill3Queued = true;
        }

        if (ReadSelectSkill4Pressed())
        {
            selectSkill4Queued = true;
        }
    }

    private void UpdateHMD()
    {
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

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput();

        data.IsVR = isVRActive;
        data.HMDRotation = currentHMD;

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
            return moveAction.action.ReadValue<Vector2>();
        }

        Vector2 input = Vector2.zero;

        if (Gamepad.current != null)
        {
            input = Gamepad.current.leftStick.ReadValue();
        }

        if (Keyboard.current != null)
        {
            Vector2 keyboardInput = Vector2.zero;

            if (Keyboard.current.wKey.isPressed)
            {
                keyboardInput.y += 1.0f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                keyboardInput.y -= 1.0f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                keyboardInput.x += 1.0f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                keyboardInput.x -= 1.0f;
            }

            if (keyboardInput.sqrMagnitude > 1.0f)
            {
                keyboardInput.Normalize();
            }

            if (keyboardInput.sqrMagnitude > 0.01f)
            {
                input = keyboardInput;
            }
        }

        return input;
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