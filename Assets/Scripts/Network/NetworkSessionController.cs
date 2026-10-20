using Fusion;
using Fusion.Sockets;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// NetworkRunnerの生成・接続開始・失敗時の再試行と、待機中の手動再接続を管理する。
/// プレイヤー生成と入力処理はNetworkLauncherから別の担当へ委譲される。
/// </summary>
public sealed class NetworkSessionController
{
    private readonly MonoBehaviour owner;
    private readonly INetworkRunnerCallbacks callbacks;

    private readonly NetworkPrefabRef playerPrefab;
    private readonly string roomName;

    private readonly bool retryClientUntilFound;
    private readonly float clientRetryInterval;
    private readonly int maxClientRetryCount;

    private readonly NetworkConnectionUIController connectionUIController;
    private readonly Func<WaitingRoomUI> getWaitingRoomUI;

    private GameObject runnerObject;
    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;

    private bool isStartingGame;
    private bool isReconnecting;

    public NetworkRunner Runner => runner;
    public bool IsStartingGame => isStartingGame;
    public bool IsReconnecting => isReconnecting;

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

    /// <summary>
    /// Runnerが存在せず接続開始中でもない場合に接続を開始する。
    /// ClientのGameNotFoundだけを設定に従って再試行し、試行ごとにRunnerを作り直す。
    /// </summary>
    public async void StartGame(GameMode gameMode)
    {
        if (owner == null)
        {
            return;
        }

        if (runner != null || isStartingGame)
        {
            return;
        }

        if (!playerPrefab.IsValid)
        {
            connectionUIController.SetStatusText("Player Prefab is not set.");
            Debug.LogError("[NetworkSessionController] Player Prefab is not set.");

            connectionUIController.ShowConnectionMenuAndUnlockButtons();
            return;
        }

        int buildIndex = SceneManager.GetActiveScene().buildIndex;

        if (buildIndex < 0)
        {
            connectionUIController.SetStatusText("Current scene is not in Build Settings.");
            Debug.LogError("[NetworkSessionController] Current scene is not in Build Settings.");

            connectionUIController.ShowConnectionMenuAndUnlockButtons();
            return;
        }

        ResetWaitingRoomUIForConnectionStart();

        isStartingGame = true;
        connectionUIController.SetConnectionMenuVisible(false);

        SceneRef sceneRef = SceneRef.FromIndex(buildIndex);
        int attemptCount = 0;

        while (true)
        {
            if (owner == null)
            {
                return;
            }

            attemptCount++;

            Debug.Log($"[NetworkSessionController] StartGame: {gameMode}, RoomName={roomName}, Attempt={attemptCount}");

            connectionUIController.SetStatusText($"{GetGameModeLabel(gameMode)}中...");

            CreateRunnerObject(gameMode, attemptCount);

            StartGameResult result = await runner.StartGame(new StartGameArgs()
            {
                GameMode = gameMode,
                SessionName = roomName,
                Scene = sceneRef,
                SceneManager = sceneManager,
                PlayerCount = 8
            });

            if (owner == null)
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

            CleanupRunner();

            bool shouldRetry =
                gameMode == GameMode.Client &&
                retryClientUntilFound &&
                shutdownReason == ShutdownReason.GameNotFound &&
                (maxClientRetryCount <= 0 || attemptCount < maxClientRetryCount);

            if (!shouldRetry)
            {
                isStartingGame = false;
                connectionUIController.ShowConnectionMenuAndUnlockButtons();
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

    /// <summary>
    /// 接続・再接続処理が進行中でなく、RoundManagerが待機フェーズのときだけ手動再接続を許可する。
    /// </summary>
    public bool CanReconnectFromWaitingRoom()
    {
        if (isStartingGame || isReconnecting)
        {
            return false;
        }

        return
            RoundManager.Instance != null &&
            RoundManager.Instance.IsWaitingForReady;
    }

    /// <summary>
    /// 現在のRunnerのShutdownを待ち、参照と待機UIをリセットしてClientとして接続し直す。
    /// 多重実行は抑止するが、待機フェーズの確認は呼び出し元が行う。
    /// </summary>
    public async void ReconnectAsClient()
    {
        if (isStartingGame || isReconnecting)
        {
            return;
        }

        isReconnecting = true;

        Debug.Log("[NetworkSessionController] Reconnect requested from waiting room.");

        connectionUIController.SetConnectionMenuVisible(false);
        connectionUIController.SetConnectionButtonsInteractable(false);
        connectionUIController.SetStatusText("再接続中...");
        connectionUIController.SetStatusTextVisible(true);

        ResetWaitingRoomUIForConnectionStart();

        NetworkRunner currentRunner = runner;

        if (currentRunner != null)
        {
            try
            {
                await currentRunner.Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetworkSessionController] Runner shutdown during reconnect failed: {e}");
            }
        }

        if (owner == null)
        {
            return;
        }

        CleanupRunner();

        isReconnecting = false;
        connectionUIController.LockButtons();

        StartGame(GameMode.Client);
    }

    /// <summary>
    /// コールバックを解除してRunner関連オブジェクトを破棄し、参照をクリアする。
    /// 接続中のRunnerを停止する必要がある場合は、呼び出し前にShutdownを待つ。
    /// </summary>
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

    private void CreateRunnerObject(GameMode gameMode, int attemptCount)
    {
        CleanupRunner();

        runnerObject = new GameObject($"NetworkRunner_{gameMode}_Attempt{attemptCount}");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);

        runner = runnerObject.AddComponent<NetworkRunner>();
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
