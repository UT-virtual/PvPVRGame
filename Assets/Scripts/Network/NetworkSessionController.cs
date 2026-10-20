using Fusion;
using Fusion.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// NetworkRunnerの生成・接続開始・失敗時の再試行と、待機中の手動再接続を管理する。
/// プレイヤー生成と入力処理はNetworkLauncherから別の担当へ委譲される。
/// </summary>
public sealed class NetworkSessionController : IDisposable
{
    public const int MaxPlayerCount = 4;
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

    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    private bool isHandlingDisconnect;
    public event Action ConnectionReset;

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
    /// 多重接続を抑止し、接続処理の終了まで待つ。失敗と例外はUIへ反映し、操作可能な状態へ戻す。
    /// ClientのGameNotFoundだけを設定に従って再試行する。
    /// </summary>
    public async Task StartGameAsync(GameMode gameMode)
    {
        if (disposed || owner == null || runner != null || isStartingGame || isReconnecting || isHandlingDisconnect)
        {
            return;
        }

        isStartingGame = true;
        connectionUIController.LockButtons();

        try
        {
            if (!playerPrefab.IsValid)
            {
                ShowConnectionFailure("Player Prefab is not set.");
                return;
            }

            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            if (buildIndex < 0)
            {
                ShowConnectionFailure("Current scene is not in Build Settings.");
                return;
            }

            ResetWaitingRoomUIForConnectionStart();
            ConnectionReset?.Invoke();
            connectionUIController.SetConnectionMenuVisible(false);
            connectionUIController.SetStatusTextVisible(true);
            await ConnectWithRetryAsync(gameMode, SceneRef.FromIndex(buildIndex));
        }
        catch (OperationCanceledException) when (disposed)
        {
            // 所有元の破棄で中断した場合はUIへ戻らない。
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (!disposed && owner != null)
            {
                CleanupRunner();
                ShowConnectionFailure("接続に失敗しました。もう一度接続してください。");
            }
        }
        finally
        {
            isStartingGame = false;
        }
    }

    private async Task ConnectWithRetryAsync(GameMode gameMode, SceneRef sceneRef)
    {
        int attemptCount = 0;
        while (!disposed && owner != null)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            attemptCount++;
            connectionUIController.SetStatusText($"{GetGameModeLabel(gameMode)}中... Attempt {attemptCount}");
            CreateRunnerObject(gameMode, attemptCount);

            StartGameResult result = await runner.StartGame(new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = roomName,
                Scene = sceneRef,
                SceneManager = sceneManager,
                PlayerCount = MaxPlayerCount,
                StartGameCancellationToken = lifetime.Token
            });

            if (disposed || owner == null)
            {
                return;
            }

            if (result.Ok)
            {
                connectionUIController.SetConnectionMenuVisible(false);
                connectionUIController.SetStatusTextVisible(false);
                return;
            }

            CleanupRunner();
            bool shouldRetry = gameMode == GameMode.Client && retryClientUntilFound &&
                result.ShutdownReason == ShutdownReason.GameNotFound &&
                (maxClientRetryCount <= 0 || attemptCount < maxClientRetryCount);

            if (!shouldRetry)
            {
                ShowConnectionFailure($"Failed: {result.ShutdownReason}");
                return;
            }

            connectionUIController.SetStatusText($"再接続先を検索中... Attempt {attemptCount}, Room: {roomName}");
            int delayMilliseconds = Mathf.RoundToInt(Mathf.Max(0.1f, clientRetryInterval) * 1000.0f);
            await Task.Delay(delayMilliseconds, lifetime.Token);
        }
    }

    private void ShowConnectionFailure(string message)
    {
        connectionUIController.SetStatusText(message);
        connectionUIController.SetStatusTextVisible(true);
        connectionUIController.ShowConnectionMenuAndUnlockButtons();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 接続・再接続処理が進行中でなく、RoundManagerが待機フェーズまたは未接続のときに手動再接続を許可する。
    /// </summary>
    public bool CanReconnectFromWaitingRoom()
    {
        if (disposed || isStartingGame || isReconnecting || isHandlingDisconnect)
        {
            return false;
        }

        return runner == null ||
            (RoundManager.Instance != null && RoundManager.Instance.IsWaitingForReady);
    }

    /// <summary>
    /// 待機中のRunnerを停止してからClientとして接続し直す。停止中の通知では復旧処理を重ねない。
    /// </summary>
    public async Task ReconnectAsClientAsync()
    {
        if (!CanReconnectFromWaitingRoom())
        {
            return;
        }

        isReconnecting = true;
        connectionUIController.LockButtons();
        connectionUIController.SetStatusText("再接続中...");
        connectionUIController.SetStatusTextVisible(true);
        ResetWaitingRoomUIForConnectionStart();
        ConnectionReset?.Invoke();

        try
        {
            if (runner != null)
            {
                await runner.Shutdown();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NetworkSessionController] Shutdown failed: {exception}");
        }
        finally
        {
            CleanupRunner();
            isReconnecting = false;
        }

        if (!disposed && owner != null)
        {
            await StartGameAsync(GameMode.Client);
        }
    }

    /// <summary>
    /// 現在のRunnerの予期しない切断を処理する。接続試行中・手動再接続中・旧Runnerの通知は無視する。
    /// </summary>
    public async Task HandleDisconnectedAsync(NetworkRunner source, string reason)
    {
        if (disposed || source != runner || isStartingGame || isReconnecting || isHandlingDisconnect)
        {
            return;
        }

        isHandlingDisconnect = true;
        connectionUIController.LockButtons();
        ResetWaitingRoomUIForConnectionStart();
        ConnectionReset?.Invoke();

        try
        {
            // 通知のコールスタックを抜けてからShutdownを待ち、通知内での再入を避ける。
            await Task.Yield();
            if (!disposed && source != null && !source.IsShutdown)
            {
                await source.Shutdown();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NetworkSessionController] Disconnect cleanup failed: {exception}");
        }
        finally
        {
            if (!disposed)
            {
                CleanupRunner();
                if (owner != null)
                {
                    ShowConnectionFailure($"接続が終了しました: {reason}（Rキーで再接続）");
                }
            }

            isHandlingDisconnect = false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lifetime.Cancel();
        CleanupRunner();
        lifetime.Dispose();
    }

    /// <summary>
    /// コールバックを解除してRunner関連オブジェクトを破棄し、参照をクリアする。
    /// 接続中のRunnerを停止する必要がある場合は、呼び出し前にShutdownを待つ。
    /// </summary>
    public void CleanupRunner()
    {
        bool hadRunner = runnerObject != null || runner != null;
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

        if (hadRunner)
        {
            ResetWaitingRoomUIForConnectionStart();
            ConnectionReset?.Invoke();
        }
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
