using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public enum LocalPlayMode
{
    Desktop,
    VirtualReality
}

// This is a per-process choice. Remote players use their networked input mode instead.
[DisallowMultipleComponent]
public sealed class LocalPlayModeController : MonoBehaviour
{
    [SerializeField] private LocalPlayMode editorMode = LocalPlayMode.Desktop;
    [SerializeField] private bool simulateVrInEditor;
    [SerializeField, Min(1f)] private float xrStartupTimeout = 15f;

    public static LocalPlayModeController Instance { get; private set; }
    public static bool IsVirtualReality => Instance != null && Instance.Mode == LocalPlayMode.VirtualReality;
    public static bool IsReady => Instance != null && Instance.initialized;
    public LocalPlayMode Mode { get; private set; }
    public string Error { get; private set; }

    private bool initialized;
    private XRManagerSettings xrManager;
    private bool ownsXr;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    public void SelectMode()
    {
        Instance = this;
        try
        {
            Mode = ResolveMode(editorMode, Application.isEditor,
                Application.platform == RuntimePlatform.Android, Environment.GetCommandLineArgs());
            Debug.Log($"[PlayMode] Selected {Mode}, scene={gameObject.scene.name}, owner={GetInstanceID()}");
        }
        catch (ArgumentException exception)
        {
            Error = exception.Message;
        }
    }

    public static LocalPlayMode ResolveMode(LocalPlayMode editor, bool isEditor, bool isAndroid, string[] arguments)
    {
        bool desktop = Array.Exists(arguments, value => string.Equals(value, "--desktop", StringComparison.OrdinalIgnoreCase));
        bool vr = Array.Exists(arguments, value => string.Equals(value, "--vr", StringComparison.OrdinalIgnoreCase));
        if (desktop && vr)
            throw new ArgumentException("Specify either --desktop or --vr, not both.");
        if (desktop) return LocalPlayMode.Desktop;
        if (vr) return LocalPlayMode.VirtualReality;
        return isEditor ? editor : isAndroid ? LocalPlayMode.VirtualReality : LocalPlayMode.Desktop;
    }

    public IEnumerator Initialize()
    {
        if (initialized || Error != null) yield break;
        // Manual XR initialization must run after Unity's first Start phase.
        yield return null;
        xrManager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;

        if (Mode == LocalPlayMode.Desktop)
        {
            ReleaseXr();
            initialized = true;
            yield break;
        }

#if UNITY_EDITOR
        if (simulateVrInEditor)
        {
            initialized = true;
            yield break;
        }
#endif

        if (xrManager == null)
        {
            Error = "VRの起動設定がありません。XR Plug-in Managementを確認してください。";
            yield break;
        }

        ownsXr = true;
        float deadline = Time.realtimeSinceStartup + xrStartupTimeout;
        IEnumerator initialization = xrManager.InitializeLoader();
        while (true)
        {
            bool more;
            try { more = initialization.MoveNext(); }
            catch (Exception exception) { Error = $"XR initialization failed: {exception.Message}"; break; }
            if (!more) break;
            if (Time.realtimeSinceStartup >= deadline)
            {
                Error = "VRの準備がタイムアウトしました。ヘッドセットとQuest Linkの接続を確認してください。";
                break;
            }
            yield return initialization.Current;
        }

        if (Error == null && xrManager.activeLoader == null)
            Error = "VRを起動できません。対象プラットフォームのOpenXR設定とヘッドセットの接続を確認してください。";

        if (Error == null)
        {
            try { xrManager.StartSubsystems(); }
            catch (Exception exception) { Error = $"XR startup failed: {exception.Message}"; }
        }

        while (Error == null && Time.realtimeSinceStartup < deadline)
        {
            XRDisplaySubsystem display = xrManager.activeLoader.GetLoadedSubsystem<XRDisplaySubsystem>();
            if (display != null && display.running)
            {
                initialized = true;
                Debug.Log("[PlayMode] XR display is running. Connection can start.");
                yield break;
            }
            yield return null;
        }

        if (Error == null) Error = "VR画面を開始できません。Quest Linkと使用中のOpenXRランタイムを確認してください。";
        ReleaseXr();
    }

    private void ReleaseXr()
    {
        if (xrManager == null) return;
        if (xrManager.isInitializationComplete)
        {
            xrManager.StopSubsystems();
            xrManager.DeinitializeLoader();
        }
        ownsXr = false;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (ownsXr) ReleaseXr();
        Instance = null;
    }
}
