using System;
using System.Linq;
using System.Reflection;
using Fusion;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs a real local Fusion session; no Photon Cloud or headset is required.
[InitializeOnLoad]
public static class GameRecoveryValidation
{
    private const string Key = "PvPVRGame.RecoverySmoke";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static GameRecoveryValidation()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        try
        {
            GameSetupValidation.Validate();
            NetworkLauncher launcher = UnityEngine.Object.FindFirstObjectByType<NetworkLauncher>();
            Set(launcher, "autoStartOnLaunch", false);
            SerializedObject mode = new SerializedObject(launcher.GetComponent<LocalPlayModeController>());
            mode.FindProperty("editorMode").enumValueIndex = (int)LocalPlayMode.Desktop;
            mode.ApplyModifiedPropertiesWithoutUndo();
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Stage", 0);
            SessionState.SetInt(Key + ".Restarts", 0);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180f);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + ".Deadline", 0), "Recovery smoke timed out.");
            if (!EditorApplication.isPlaying || !LocalPlayModeController.IsReady) return;
            NetworkLauncher launcher = NetworkLauncher.Instance;
            NetworkSessionController session = (NetworkSessionController)Field(launcher, "sessionController");
            int stage = SessionState.GetInt(Key + ".Stage", 0);
            if (stage == 0)
            {
                SessionState.SetInt(Key + ".Owner", launcher.GetInstanceID());
                launcher.StartGame(GameMode.Single);
                Stage(1);
                return;
            }
            if (stage == 3)
            {
                if (launcher.Runner != null || session.IsReconnecting) return;
                Require(session.CanReconnect(), "Disconnect without a RoundManager must allow reconnection.");
                // Exercise the same restart path with Single instead of contacting Photon Cloud.
                typeof(NetworkSessionController).GetMethod("RestartSessionAsync", Private).Invoke(session, new object[] { GameMode.Single });
                Stage(4);
                return;
            }
            if (launcher.Runner == null || !launcher.Runner.IsRunning || session.IsStartingGame ||
                RoundManager.Instance == null || !RoundManager.Instance.IsNetworkReady) return;
            PlayerHealth local = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.Object != null && p.Object.HasInputAuthority);
            if (local == null) return;
            if (stage == 1 || stage == 4)
            {
                // Let delayed camera setup and render callbacks finish before checking.
                SessionState.SetInt(Key + ".Frame", Time.frameCount);
                Stage(stage == 1 ? 2 : 5);
                return;
            }
            if (Time.frameCount < SessionState.GetInt(Key + ".Frame", 0) + 5) return;
            if (stage == 2 || stage == 5)
            {
                Require(launcher.GetInstanceID() == SessionState.GetInt(Key + ".Owner", 0), "Scene reload replaced launcher owner.");
                Require(UnityEngine.Object.FindObjectsByType<NetworkLauncher>(FindObjectsSortMode.None).Length == 1, "Duplicate launcher survived.");
                Require(UnityEngine.Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None).Length == 1, "Duplicate runner survived.");
                Require(BodyIsHidden(local), "Local Desktop body must be hidden: " + string.Join(", ", local.BodyRenderers.Where(r => r != null && r.enabled).Select(r => r.name)));
                Require(!LocalPlayModeController.IsVirtualReality, "Reconnect changed the local mode.");
                Require(Camera.allCamerasCount == 1, "Reconnect must leave one rendering camera.");
                Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled) == 1, "Reconnect must leave one audio listener.");
                NetworkInputCollector collector = (NetworkInputCollector)Field(launcher, "inputCollector");
                Require(collector.LocalPlayerController == local.GetComponent<PlayerController>(), "Input still points to the old player.");
                foreach (string actionName in new[] { "moveAction", "lookAction", "fireAction", "reloadAction" })
                {
                    UnityEngine.InputSystem.InputActionReference action = (UnityEngine.InputSystem.InputActionReference)Field(launcher, actionName);
                    Require(action != null && action.action.enabled, "Reload disabled action: " + actionName);
                }
                int beforeCount = UnityEngine.Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Length;
                launcher.OnPlayerJoined(launcher.Runner, launcher.Runner.LocalPlayer);
                Require(UnityEngine.Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Length == beforeCount, "Duplicate join spawned an extra player.");
                if (stage == 5)
                {
                    Require(SceneManager.GetActiveScene().handle != SessionState.GetInt(Key + ".Scene", 0), "Reconnect reused a damaged scene.");
                    Require(RoundManager.Instance.GetInstanceID() != SessionState.GetInt(Key + ".Round", 0), "RoundManager was not recreated.");
                    SessionState.SetInt(Key + ".Restarts", SessionState.GetInt(Key + ".Restarts", 0) + 1);
                }
                if (SessionState.GetInt(Key + ".Restarts", 0) < 2)
                {
                    SessionState.SetInt(Key + ".Scene", SceneManager.GetActiveScene().handle);
                    SessionState.SetInt(Key + ".Round", RoundManager.Instance.GetInstanceID());
                    if (SessionState.GetInt(Key + ".Restarts", 0) == 0)
                    {
                        Stage(4);
                        typeof(NetworkSessionController).GetMethod("RestartSessionAsync", Private).Invoke(session, new object[] { GameMode.Single });
                        // A second request in the same frame must not create another operation.
                        typeof(NetworkSessionController).GetMethod("RestartSessionAsync", Private).Invoke(session, new object[] { GameMode.Single });
                    }
                    else
                    {
                        Stage(3);
                        _ = launcher.Runner.Shutdown();
                    }
                    return;
                }
                local.RespawnForRound(null);
                Require(BodyIsHidden(local), "Respawn revealed local body.");
                ValidateVrSpectatorGeometry(launcher, local.transform);
                Debug.Log("[GameRecoveryValidation] PASS: two real local Fusion shutdown/reconnect cycles, fresh RoundManager, one runner/launcher/camera/listener, duplicate join guard, local body hidden after spawn/respawn, VR spectator tracking offset and restoration.");
                Finish(0);
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void ValidateVrSpectatorGeometry(NetworkLauncher launcher, Transform player)
    {
        LocalPlayModeController mode = launcher.GetComponent<LocalPlayModeController>();
        PropertyInfo modeProperty = typeof(LocalPlayModeController).GetProperty("Mode");
        modeProperty.SetValue(mode, LocalPlayMode.VirtualReality);
        Require(launcher.RestoreScenePresentation(), "VR scene references are incomplete.");
        launcher.AttachXrOriginToPlayer(player);
        Transform rig = (Transform)Field(launcher, "xrOriginRoot");
        Transform head = (Transform)Field(launcher, "xrHmd");
        Vector3 eye = new Vector3(30, 40, 50);
        Quaternion body = Quaternion.Euler(20, 70, 10);
        launcher.UpdateVrSpectatorView(eye, body);
        Require(Vector3.Distance(head.position, eye) < 0.001f, "Spectator eye anchor is incorrect.");
        Vector3 motion = new Vector3(0.1f, 0.05f, -0.2f);
        head.position += rig.TransformVector(motion);
        launcher.UpdateVrSpectatorView(eye, body);
        Require(Vector3.Distance(head.position, eye + rig.TransformVector(motion)) < 0.001f, "Spectator view overwrote physical head motion.");
        launcher.EndVrSpectating();
        Require(rig.localPosition.sqrMagnitude < 0.000001f && Quaternion.Angle(rig.localRotation, Quaternion.identity) < 0.01f, "VR rig did not return to local player.");
        launcher.UpdateVrSpectatorView(eye + Vector3.right, Quaternion.Euler(0, 120, 0));
        SpectatorCameraController spectator = UnityEngine.Object.FindFirstObjectByType<SpectatorCameraController>();
        spectator.enabled = false;
        Require(rig.localPosition.sqrMagnitude < 0.000001f && Quaternion.Angle(rig.localRotation, Quaternion.identity) < 0.01f, "Disabling spectator failed to restore the rig.");
        spectator.enabled = true;
        launcher.DetachXrOriginFromPlayer();
        modeProperty.SetValue(mode, LocalPlayMode.Desktop);
        Require(launcher.RestoreScenePresentation(), "Desktop camera was not restored after VR geometry test.");
    }

    private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static bool BodyIsHidden(PlayerHealth player)
    {
        // PlayerHealth captures renderers before VRBodyTargetSync reparents the gun.
        // Those cached body renderers can include the intentionally visible weapon.
        Renderer[] weapon = (Renderer[])Field(player, "weaponRenderers");
        Renderer[] body = player.BodyRenderers.Where(r => r != null && !weapon.Contains(r)).ToArray();
        return body.Length > 0 && body.All(r => !r.enabled);
    }
    private static void Stage(int value) => SessionState.SetInt(Key + ".Stage", value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Set(UnityEngine.Object target, string field, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Fail(Exception exception) { Debug.LogException(exception); Finish(1); }
    private static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
