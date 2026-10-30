using System;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

// Batch entry points and menu checks for the shared Desktop/VR configuration.
[InitializeOnLoad]
public static class GameSetupValidation
{
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string SmokeKey = "PvPVRGame.DesktopSmoke";

    static GameSetupValidation()
    {
        if (SessionState.GetBool(SmokeKey, false)) EditorApplication.update += SmokeTick;
    }

    [MenuItem("Tools/PvPVRGame/Validate Shared Game Setup")]
    public static void Validate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode before validation.");
        Require(Enumerable.Range(0, SceneManager.sceneCount).All(i => !SceneManager.GetSceneAt(i).isDirty), "Save modified scenes before opening the validation scene.");
        ValidateModeSelection();
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        Require(scenes.Length == 1 && scenes[0].path == ScenePath, "Only GameScene must be enabled in the shared scene list.");
        XRGeneralSettings[] xrSettings = AssetDatabase.LoadAllAssetsAtPath("Assets/XR/XRGeneralSettingsPerBuildTarget.asset").OfType<XRGeneralSettings>().ToArray();
        Require(xrSettings.Length >= 2, "Windows and Android XR settings must exist.");
        foreach (XRGeneralSettings settings in xrSettings)
            Require(!settings.InitManagerOnStart, "Automatic XR startup must be disabled for " + settings.name);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        NetworkLauncher[] launchers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NetworkLauncher>(true)).ToArray();
        Require(launchers.Length == 1, "GameScene must contain one launcher.");
        SerializedObject launcher = new SerializedObject(launchers[0]);
        foreach (string property in new[] { "xrOriginRoot", "xrHmd", "xrLeftController", "xrRightController", "sceneFollowCamera", "cinemachineCamera" })
            Require(launcher.FindProperty(property).objectReferenceValue != null, "Missing launcher reference: " + property);
        Require(launchers[0].GetComponent<LocalPlayModeController>() != null, "Launcher must have a play-mode selector.");
        Transform hmd = (Transform)launcher.FindProperty("xrHmd").objectReferenceValue;
        Camera pcCamera = (Camera)launcher.FindProperty("sceneFollowCamera").objectReferenceValue;
        Require(hmd != pcCamera.transform, "VR and PC cameras must be different.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GamePlayer.prefab");
        Require(prefab != null && prefab.GetComponent<NetworkObject>() != null, "Common player prefab is missing its NetworkObject.");
        Require(prefab.GetComponent<VRBodyTargetSync>() != null, "Common player needs head/hand synchronization.");
        Require(prefab.GetComponent<PlayerCamera>() != null && prefab.GetComponent<PlayerController>() != null, "Common player needs PC and VR control components.");
        Require(prefab.GetComponent<PlayerWeapon>().MuzzleTransform != null, "VR muzzle must be assigned.");
        Require(prefab.GetComponent<PlayerCamera>().CameraTarget != null, "Desktop camera target must be assigned.");
        Debug.Log("[GameSetupValidation] PASS: mode selection, shared scene, explicit XR startup and common player configuration.");
    }

    public static void Run()
    {
        try { Validate(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void RunDesktopSmoke()
    {
        try
        {
            Validate();
            NetworkLauncher launcher = UnityEngine.Object.FindFirstObjectByType<NetworkLauncher>();
            SetSerialized(launcher, "autoStartOnLaunch", false);
            SerializedObject mode = new SerializedObject(launcher.GetComponent<LocalPlayModeController>());
            mode.FindProperty("editorMode").enumValueIndex = (int)LocalPlayMode.Desktop;
            mode.ApplyModifiedPropertiesWithoutUndo();
            SessionState.SetBool(SmokeKey, true);
            SessionState.SetInt(SmokeKey + ".Stage", 0);
            SessionState.SetFloat(SmokeKey + ".Deadline", (float)EditorApplication.timeSinceStartup + 120f);
            EditorApplication.update -= SmokeTick;
            EditorApplication.update += SmokeTick;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Debug.LogException(exception); FinishSmoke(1); }
    }

    private static void SmokeTick()
    {
        try
        {
            int stage = SessionState.GetInt(SmokeKey + ".Stage", 0);
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(SmokeKey + ".Deadline", 0), "Desktop smoke test timed out.");
            if (stage == 2)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) FinishSmoke(0);
                return;
            }
            if (!EditorApplication.isPlaying || !LocalPlayModeController.IsReady) return;
            if (stage == 0)
            {
                Require(!LocalPlayModeController.IsVirtualReality, "Desktop unexpectedly selected VR.");
                XROrigin origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
                Require(origin != null && !origin.gameObject.activeInHierarchy, "Desktop must disable the XR rig.");
                Require(Camera.allCamerasCount == 1, "Desktop must have exactly one rendering camera.");
                Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled) == 1, "Exactly one audio listener must be active.");
                Require(UnityEngine.Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None).Length == 0, "Manual startup must not auto-connect.");
                SessionState.SetInt(SmokeKey + ".Owner", NetworkLauncher.Instance.GetInstanceID());
                UnityEngine.Object.Instantiate(NetworkLauncher.Instance.gameObject);
                SessionState.SetInt(SmokeKey + ".Stage", 1);
                SessionState.SetInt(SmokeKey + ".Frame", Time.frameCount);
                return;
            }
            if (Time.frameCount <= SessionState.GetInt(SmokeKey + ".Frame", 0) + 1) return;
            Require(UnityEngine.Object.FindObjectsByType<NetworkLauncher>(FindObjectsSortMode.None).Length == 1, "Duplicate launcher was not suppressed.");
            Require(NetworkLauncher.Instance.GetInstanceID() == SessionState.GetInt(SmokeKey + ".Owner", 0), "Duplicate launcher replaced the session owner.");
            Require(UnityEngine.Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None).Length == 0, "Duplicate launcher started an extra connection.");
            Debug.Log("[GameSetupValidation] PASS: Desktop boot, one camera/listener, XR rig disabled, duplicate launcher suppressed without connecting.");
            SessionState.SetInt(SmokeKey + ".Stage", 2);
            EditorApplication.ExitPlaymode();
        }
        catch (Exception exception) { Debug.LogException(exception); FinishSmoke(1); }
    }

    private static void FinishSmoke(int code)
    {
        SessionState.SetBool(SmokeKey, false);
        EditorApplication.update -= SmokeTick;
        EditorApplication.Exit(code);
    }

    private static void SetSerialized(UnityEngine.Object target, string property, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(property).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ValidateModeSelection()
    {
        string[] none = Array.Empty<string>();
        Require(LocalPlayModeController.ResolveMode(LocalPlayMode.VirtualReality, false, false, none) == LocalPlayMode.Desktop, "Windows default must be Desktop.");
        Require(LocalPlayModeController.ResolveMode(LocalPlayMode.Desktop, false, true, none) == LocalPlayMode.VirtualReality, "Quest default must be VR.");
        Require(LocalPlayModeController.ResolveMode(LocalPlayMode.VirtualReality, true, false, none) == LocalPlayMode.VirtualReality, "Editor mode must be respected.");
        Require(LocalPlayModeController.ResolveMode(LocalPlayMode.Desktop, true, false, none) == LocalPlayMode.Desktop, "Editor Desktop mode must be respected.");
        Require(LocalPlayModeController.ResolveMode(LocalPlayMode.Desktop, false, false, new[] { "--vr" }) == LocalPlayMode.VirtualReality, "--vr must enable PCVR.");
        Require(LocalPlayModeController.ResolveMode(LocalPlayMode.VirtualReality, false, true, new[] { "--desktop" }) == LocalPlayMode.Desktop, "--desktop must override the platform.");
        Require(LocalPlayModeController.ResolveMode(LocalPlayMode.Desktop, false, false, new[] { "--VR" }) == LocalPlayMode.VirtualReality, "Flags must ignore case.");
        bool rejected = false;
        try { LocalPlayModeController.ResolveMode(LocalPlayMode.Desktop, false, false, new[] { "--vr", "--desktop" }); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected, "Conflicting flags must fail clearly.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
