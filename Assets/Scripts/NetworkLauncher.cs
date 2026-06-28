using Fusion;
using Fusion.Sockets;
using SlimUI.ModernMenu;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    private string statusText = "Ready";

    private Vector2 queuedLookInput;
    private bool jumpQueued;
    private bool reloadQueued;
    private bool readyQueued;
    private bool wasLeftTriggerPressed;

    private PlayerController localPlayerController;

    private bool isVRActive => UnityEngine.XR.XRSettings.isDeviceActive;
    private Quaternion currentHMD = Quaternion.identity;




    private void Start()
    {
        
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

    public async void StartGame(GameMode gameMode)
    {
        waitingRoomUI.ShowRoomUI();
        if (runner != null)
        {
            return;
        }

        if (!playerPrefab.IsValid)
        {
            statusText = "Player Prefab is not set.";
            Debug.LogError(statusText);
            return;
        }

        Debug.Log($"StartGame: {gameMode}");
        statusText = $"Starting {gameMode}...";

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        int buildIndex = SceneManager.GetActiveScene().buildIndex;

        if (buildIndex < 0)
        {
            statusText = "Current scene is not in Build Settings.";
            Debug.LogError(statusText);
            return;
        }

        SceneRef sceneRef = SceneRef.FromIndex(buildIndex);

        StartGameResult result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = gameMode,
            SessionName = roomName,
            Scene = sceneRef,
            SceneManager = sceneManager
        });

        if (!result.Ok)
        {
            statusText = $"Failed: {result.ShutdownReason}";
            Debug.LogError($"Failed to start Fusion: {result.ShutdownReason}");

            if (runner != null)
            {
                runner.RemoveCallbacks(this);
                Destroy(runner);
                runner = null;
            }

            if (sceneManager != null)
            {
                Destroy(sceneManager);
                sceneManager = null;
            }

            return;
        }

        statusText = $"Running: {gameMode} / Room: {roomName}";
        Debug.Log(statusText);
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

    private void OnGUI()
    {
        GUI.Label(new Rect(20, 20, 500, 30), statusText);

         if (runner != null)
             return;

         if (GUI.Button(new Rect(20, 60, 200, 50), "Host"))
             StartGame(GameMode.Host);

         if (GUI.Button(new Rect(20, 120, 200, 50), "Client"))
             StartGame(GameMode.Client);
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

        spawnedPlayers.Add(player, playerObject);

        Debug.Log($"Spawned player: {player}");
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

        data.Buttons = buttons;

        input.Set(data);

        queuedLookInput = Vector2.zero;
        jumpQueued = false;
        reloadQueued = false;
        readyQueued = false;
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
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
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