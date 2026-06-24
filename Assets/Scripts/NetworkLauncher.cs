using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
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
    
    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    private string statusText = "Ready";
    private bool wasLeftTriggerPressed;
    private Vector2 queuedLookInput;
    private bool jumpQueued;
    private bool reloadQueued;

    // VRかどうかを判定する一時変数
    private bool isVRActive => UnityEngine.XR.XRSettings.isDeviceActive;
    private Quaternion currentHMD = Quaternion.identity;


    private void OnEnable()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        jumpAction.action.Enable();
        fireAction.action.Enable();
        reloadAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        lookAction.action.Disable();
        jumpAction.action.Disable();
        fireAction.action.Disable();
        reloadAction.action.Disable();
    }

    private async void StartGame(GameMode gameMode)
    {
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
        // VRならHMDの回転を取得
        if (isVRActive && hmdRotationAction != null)
        {
            currentHMD = hmdRotationAction.action.ReadValue<Quaternion>();
        }

        Vector2 lookInput = ReadLookInput();

        queuedLookInput += lookInput;

        if (localPlayerController != null)
        {
            localPlayerController.ApplyLocalLook(lookInput, isVRActive, currentHMD);
        }

        if (jumpAction.action.WasPressedThisFrame())
        {
            jumpQueued = true;
        }

        if (reloadAction.action.WasPressedThisFrame())
        {
            reloadQueued = true;
        }

    }

    private void OnGUI()
    {
        GUI.Label(new Rect(20, 20, 500, 30), statusText);

        if (runner != null)
        {
            return;
        }

        if (GUI.Button(new Rect(20, 60, 200, 50), "Host"))
        {
            StartGame(GameMode.Host);
        }

        if (GUI.Button(new Rect(20, 120, 200, 50), "Client"))
        {
            StartGame(GameMode.Client);
        }
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
    //data.LookInput = queuedLookInput;

    // VRの時はUpdate内で加工済みのものを使わず、ローカルと同じ計算で送る（同期ズレ防止）
    if (isVRActive) {
        Vector2 rawLook = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;
        data.LookInput = new Vector2(rawLook.x * vrStickTurnSpeed * Time.deltaTime, 0);
    } else {
        data.LookInput = queuedLookInput;
    }

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

    data.Buttons = buttons;

    input.Set(data);

    queuedLookInput = Vector2.zero;
    jumpQueued = false;
    reloadQueued = false;
}

    private Vector2 ReadMoveInput()
    {
        /*Vector2 input = Vector2.zero;

        if (Gamepad.current != null)
        {
            input = Gamepad.current.leftStick.ReadValue();
        }

        if (Keyboard.current != null)
        {
            Vector2 keyboardInput = Vector2.zero;

            if (Keyboard.current.wKey.isPressed) keyboardInput.y += 1.0f;
            if (Keyboard.current.sKey.isPressed) keyboardInput.y -= 1.0f;
            if (Keyboard.current.dKey.isPressed) keyboardInput.x += 1.0f;
            if (Keyboard.current.aKey.isPressed) keyboardInput.x -= 1.0f;

            if (keyboardInput.sqrMagnitude > 1.0f)
            {
                keyboardInput.Normalize();
            }

            if (keyboardInput.sqrMagnitude > 0.01f)
            {
                input = keyboardInput;
            }
        }

        return input;*/
        if (moveAction == null) return Vector2.zero;
        return moveAction.action.ReadValue<Vector2>();
    }

    private Vector2 ReadLookInput()
    {
        /*Vector2 lookInput = Vector2.zero;

        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            lookInput.x += mouseDelta.x * mouseLookSpeed;
            lookInput.y += mouseDelta.y * mouseLookSpeed;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed) lookInput.x -= keyboardLookSpeed * Time.deltaTime;
            if (Keyboard.current.rightArrowKey.isPressed) lookInput.x += keyboardLookSpeed * Time.deltaTime;
            if (Keyboard.current.upArrowKey.isPressed) lookInput.y += keyboardLookSpeed * Time.deltaTime;
            if (Keyboard.current.downArrowKey.isPressed) lookInput.y -= keyboardLookSpeed * Time.deltaTime;
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.rightStick.ReadValue();

            lookInput.x += stick.x * gamepadLookSpeed * Time.deltaTime;
            lookInput.y += stick.y * gamepadLookSpeed * Time.deltaTime;
        }

        return lookInput;*/

        if (lookAction == null) return Vector2.zero;
        Vector2 rawLook = lookAction.action.ReadValue<Vector2>();
        // マウスの時だけ、感度（mouseLookSpeed）を適用する
        if (isVRActive)
        {
            // VR: 右スティックのX軸（左右）のみを使用し、VR専用の感度を適用する
            return new Vector2(rawLook.x * vrStickTurnSpeed * Time.deltaTime, 0);
        }
        else
        {
            // PCデバッグ: 既存のマウス/ゲームパッドの感度処理
            if (Mouse.current != null && lookAction.action.activeControl?.device == Mouse.current)
                return rawLook * mouseLookSpeed;

            return rawLook * gamepadLookSpeed * Time.deltaTime;
        }
    }

    /*private bool ReadJumpInput()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }*/

    private bool ReadFireHeldInput()
    {
        /*if (Keyboard.current != null && Keyboard.current.jKey.isPressed)
        {
            return true;
        }

        if (Gamepad.current != null)
        {
            return Gamepad.current.rightTrigger.ReadValue() > 0.5f;
        }

        return false;*/
        if (fireAction == null) return false;

        // トリガーの押し込み量、またはボタンが押されているかを判定（0.5以上で発砲）
        // Value (Axis) と Button の両方に対応
        var control = fireAction.action.activeControl;
        if (control is UnityEngine.InputSystem.Controls.ButtonControl)
        {
            return fireAction.action.IsPressed();
        }
        return fireAction.action.ReadValue<float>() > 0.5f;
    }

    /*private bool ReadReloadInput()
    {
        bool reload = false;

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            reload = true;
        }

        if (Gamepad.current != null)
        {
            float leftTriggerValue = Gamepad.current.leftTrigger.ReadValue();
            bool leftTriggerPressed = leftTriggerValue > 0.5f;

            if (leftTriggerPressed && !wasLeftTriggerPressed)
            {
                reload = true;
            }

            wasLeftTriggerPressed = leftTriggerPressed;
        }
        else
        {
            wasLeftTriggerPressed = false;
        }

        return reload;
    }*/

    private PlayerController localPlayerController;

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