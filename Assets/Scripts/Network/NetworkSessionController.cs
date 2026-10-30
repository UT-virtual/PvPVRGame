using Fusion;
using Fusion.Sockets;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NetworkSessionController
{
    private readonly MonoBehaviour owner;
    private readonly INetworkRunnerCallbacks callbacks;

    private readonly NetworkPrefabRef playerPrefab;
    private readonly string roomName;

    private readonly bool retryClientUntilFound;
    private readonly float clientRetryInterval;
    private readonly int maxClientRetryCount;

    private NetworkConnectionUIController connectionUIController;
    private readonly Func<WaitingRoomUI> getWaitingRoomUI;

    private GameObject runnerObject;
    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;

    private bool isStartingGame;
    private bool isReconnecting;
    private bool disposed;
    private int operationVersion;
    private bool sceneNeedsReload;
    private int gameSceneBuildIndex = -1;

    private static NetworkSessionController activeSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => activeSession = null;

    public NetworkRunner Runner => runner;
    public bool IsStartingGame => isStartingGame;
    public bool IsReconnecting => isReconnecting;

    public void SetConnectionUI(NetworkConnectionUIController controller) => connectionUIController = controller;

    public NetworkSessionController(
        MonoBehaviour owner,
        INetworkRunnerCallbacks callbacks,
        NetworkPrefabRef playerPrefab,
        string roomName,
        bool retryClientUntilFound,
        float clientRetryInterval,
        int maxClientRetryCount,
        NetworkConnectionUIController connectionUIController,
        Func<WaitingRoomUI> getWaitingRoomUI
    )
    {
        this.owner = owner;
        this.callbacks = callbacks;

        this.playerPrefab = playerPrefab;
        this.roomName = roomName;

        this.retryClientUntilFound = retryClientUntilFound;
        this.clientRetryInterval = clientRetryInterval;
        this.maxClientRetryCount = maxClientRetryCount;

        this.connectionUIController = connectionUIController;
        this.getWaitingRoomUI = getWaitingRoomUI;
    }

    public async void StartGame(GameMode gameMode)
    {
        if (disposed || owner == null)
        {
            return;
        }

        if (runner != null || isStartingGame || isReconnecting)
        {
            return;
        }

        if (activeSession != null && activeSession != this)
        {
            Debug.LogWarning("[NetworkSessionController] Another session already owns this process. Connection ignored.");
            return;
        }

        if (!playerPrefab.IsValid)
        {
            connectionUIController.SetStatusText("Player Prefab is not set.");
            Debug.LogError("[NetworkSessionController] Player Prefab is not set.");

            connectionUIController.ShowConnectionMenuAndUnlockButtons();
            return;
        }

        int buildIndex = gameSceneBuildIndex >= 0 ? gameSceneBuildIndex : SceneManager.GetActiveScene().buildIndex;

        if (buildIndex < 0)
        {
            connectionUIController.SetStatusText("Current scene is not in Build Settings.");
            Debug.LogError("[NetworkSessionController] Current scene is not in Build Settings.");

            connectionUIController.ShowConnectionMenuAndUnlockButtons();
            return;
        }

        ResetWaitingRoomUIForConnectionStart();

        isStartingGame = true;
        gameSceneBuildIndex = buildIndex;
        activeSession = this;
        int version = ++operationVersion;
        connectionUIController.SetConnectionMenuVisible(false);

        SceneRef sceneRef = SceneRef.FromIndex(buildIndex);
        int attemptCount = 0;

        while (true)
        {
            if (disposed || owner == null || version != operationVersion)
            {
                return;
            }

            attemptCount++;

            Debug.Log($"[NetworkSessionController] StartGame: {gameMode}, RoomName={roomName}, Attempt={attemptCount}, Scene={SceneManager.GetActiveScene().name}, BuildIndex={buildIndex}, Owner={owner.GetInstanceID()}");

            connectionUIController.SetStatusText($"{GetGameModeLabel(gameMode)}中...");

            StartGameResult result;
            try
            {
                await RestoreSceneIfNeededAsync();
                if (disposed || owner == null || version != operationVersion) return;
                // A new scene supplies new UI references through the persistent launcher.
                connectionUIController.LockButtons();
                connectionUIController.SetStatusTextVisible(true);
                connectionUIController.SetStatusText($"{GetGameModeLabel(gameMode)}中...");
                CreateRunnerObject(gameMode, attemptCount);
                NetworkRunner startingRunner = runner;
                result = await startingRunner.StartGame(new StartGameArgs()
                {
                    GameMode = gameMode,
                    SessionName = roomName,
                    Scene = sceneRef,
                    SceneManager = sceneManager,
                    PlayerCount = 8
                });
            }
            catch (Exception exception)
            {
                if (disposed || owner == null || version != operationVersion) return;
                Debug.LogError($"[NetworkSessionController] StartGame failed: {exception}");
                await ShutdownRunnerAsync();
                if (disposed || version != operationVersion) return;
                isStartingGame = false;
                if (activeSession == this) activeSession = null;
                ShowReconnectPrompt(exception.Message);
                return;
            }

            if (disposed || owner == null || version != operationVersion)
            {
                return;
            }

            if (result.Ok)
            {
                connectionUIController.SetStatusText($"ルーム名: {roomName}");
                Debug.Log($"[NetworkSessionController] ルーム名: {roomName}");

                isStartingGame = false;

                connectionUIController.SetConnectionMenuVisible(false);
                connectionUIController.SetStatusTextVisible(false);
                return;
            }

            ShutdownReason shutdownReason = result.ShutdownReason;

            connectionUIController.SetStatusText($"Failed: {shutdownReason}");
            Debug.LogError($"[NetworkSessionController] Failed to start Fusion: {shutdownReason}");

            await ShutdownRunnerAsync();
            if (disposed || version != operationVersion) return;

            bool shouldRetry =
                gameMode == GameMode.Client &&
                retryClientUntilFound &&
                shutdownReason == ShutdownReason.GameNotFound &&
                (maxClientRetryCount <= 0 || attemptCount < maxClientRetryCount);

            if (!shouldRetry)
            {
                isStartingGame = false;
                if (activeSession == this) activeSession = null;
                ShowReconnectPrompt($"Failed: {shutdownReason}");
                return;
            }

            connectionUIController.SetStatusText(
                $"再接続先を検索中... " +
                $"Attempt {attemptCount}, Room: {roomName}"
            );

            Debug.Log(
                $"[NetworkSessionController] GameNotFound. Retry Client after {clientRetryInterval} sec. " +
                $"RoomName={roomName}, Attempt={attemptCount}"
            );

            int delayMilliseconds = Mathf.RoundToInt(Mathf.Max(0.1f, clientRetryInterval) * 1000.0f);
            await Task.Delay(delayMilliseconds);
        }
    }

    public bool CanReconnect()
    {
        if (disposed || isStartingGame || isReconnecting)
        {
            return false;
        }

        return runner == null ||
            RoundManager.Instance != null &&
            RoundManager.Instance.IsWaitingForReady;
    }

    public void ReconnectAsClient() => _ = RestartSessionAsync(GameMode.Client);

    private async Task RestartSessionAsync(GameMode gameMode)
    {
        if (disposed || isStartingGame || isReconnecting)
        {
            return;
        }

        isReconnecting = true;
        int version = ++operationVersion;

        Debug.Log("[NetworkSessionController] Reconnect requested.");

        connectionUIController.SetConnectionMenuVisible(false);
        connectionUIController.SetConnectionButtonsInteractable(false);
        connectionUIController.SetStatusText("再接続中...");
        connectionUIController.SetStatusTextVisible(true);

        ResetWaitingRoomUIForConnectionStart();

        await ShutdownRunnerAsync();

        if (disposed || owner == null || version != operationVersion)
        {
            return;
        }

        CleanupRunner();

        isReconnecting = false;
        connectionUIController.LockButtons();

        StartGame(gameMode);
    }

    public void CleanupRunner()
    {
        if (runner != null)
        {
            runner.RemoveCallbacks(callbacks);
        }

        if (runnerObject != null)
        {
            UnityEngine.Object.Destroy(runnerObject);
        }
        else
        {
            if (runner != null)
            {
                UnityEngine.Object.Destroy(runner);
            }

            if (sceneManager != null)
            {
                UnityEngine.Object.Destroy(sceneManager);
            }
        }

        runner = null;
        sceneManager = null;
        runnerObject = null;
    }

    private async Task ShutdownRunnerAsync()
    {
        NetworkRunner oldRunner = runner;
        GameObject oldObject = runnerObject;
        runner = null;
        runnerObject = null;
        sceneManager = null;
        if (oldRunner != null)
        {
            sceneNeedsReload = true;
            oldRunner.RemoveCallbacks(callbacks);
            try { await oldRunner.Shutdown(); }
            catch (Exception exception) { Debug.LogWarning($"[NetworkSessionController] Shutdown failed: {exception.Message}"); }
        }
        if (oldObject != null) UnityEngine.Object.Destroy(oldObject);
    }

    public async void HandleShutdown(NetworkRunner stoppedRunner, ShutdownReason reason)
    {
        if (disposed || stoppedRunner != runner) return;
        Debug.Log($"[NetworkSessionController] Shutdown: {reason}, Runner={stoppedRunner.GetInstanceID()}");
        // StartGame owns cleanup while its asynchronous operation is in flight.
        if (isStartingGame || isReconnecting) return;
        isReconnecting = true;
        int version = ++operationVersion;
        sceneNeedsReload = true;
        // Let Fusion finish its callback before disposing the old runner.
        await Task.Yield();
        if (disposed || owner == null || version != operationVersion) return;
        await ShutdownRunnerAsync();
        if (disposed || owner == null || version != operationVersion) return;
        isReconnecting = false;
        if (activeSession == this) activeSession = null;
        ResetWaitingRoomUIForConnectionStart();
        ShowReconnectPrompt($"Disconnected: {reason}");
    }

    private async Task RestoreSceneIfNeededAsync()
    {
        if (!sceneNeedsReload) return;
        if (owner is NetworkLauncher launcher) launcher.PrepareForSceneReload();
        AsyncOperation load = SceneManager.LoadSceneAsync(gameSceneBuildIndex, LoadSceneMode.Single);
        if (load == null) throw new InvalidOperationException("Game scene could not be reloaded.");
        while (!load.isDone)
        {
            await Task.Yield();
            if (disposed || owner == null) return;
        }
        // Old scene objects (including action managers) finish destruction this frame.
        await Task.Yield();
        if (disposed || owner == null) return;
        if (owner is NetworkLauncher restoredLauncher && !restoredLauncher.RestoreScenePresentation())
            throw new InvalidOperationException("Reloaded scene is missing its cameras or XR rig.");
        sceneNeedsReload = false;
    }

    private void ShowReconnectPrompt(string message)
    {
        connectionUIController.SetStatusText(message + "\n再接続: R / ゲームパッド Start / VR 両手グリップ");
        connectionUIController.SetStatusTextVisible(true);
        connectionUIController.ShowConnectionMenuAndUnlockButtons();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ++operationVersion;
        if (activeSession == this) activeSession = null;
        _ = ShutdownRunnerAsync();
    }

    private void CreateRunnerObject(GameMode gameMode, int attemptCount)
    {
        CleanupRunner();

        runnerObject = new GameObject($"NetworkRunner_{gameMode}_Attempt{attemptCount}");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);

        runner = runnerObject.AddComponent<NetworkRunner>();
        Debug.Log($"[NetworkSessionController] Created runner={runner.GetInstanceID()}, Owner={owner.GetInstanceID()}");
        runner.ProvideInput = true;
        runner.AddCallbacks(callbacks);

        sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
    }

    private void ResetWaitingRoomUIForConnectionStart()
    {
        WaitingRoomUI waitingRoomUI = getWaitingRoomUI?.Invoke();

        if (waitingRoomUI == null)
        {
            return;
        }

        waitingRoomUI.SetLocalPlayerSpawned(false);
        waitingRoomUI.HideRoomUI();
    }

    private string GetGameModeLabel(GameMode gameMode)
    {
        if (gameMode == GameMode.Host)
        {
            return "ホスト";
        }

        if (gameMode == GameMode.Client)
        {
            return "クライアント";
        }

        return "自動接続";
    }
}
