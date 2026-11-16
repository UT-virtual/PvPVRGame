using System.Collections.Generic;
using Fusion;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 観戦専用端末、または死亡後の対戦者のカメラとHUDを生存者の同期視点へ切り替える。
/// 観戦対象のモデルを端末上で一時的に隠し、対象切替や観戦終了時に保存した表示状態へ戻す。
/// 固定視点は待機中かつ対戦者不在の場合に限り、それ以外の対象不在時は直前の視点を保持する。
/// </summary>
public class SpectatorCameraController : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Spectator")]
    [SerializeField] private float spectatorDelay = 3.0f;
    [SerializeField] private float stickSwitchThreshold = 0.6f;

    [Header("Overview")]
    [SerializeField] private Transform overviewCameraPoint;
    [SerializeField] private Vector3 defaultOverviewPosition = new Vector3(0f, 20f, -30f);

    private NetworkLauncher networkLauncher;
    private bool hasSpawnedCombatants;
    public bool IsWaitingOverview { get; private set; }
    private bool savedCameraTargets;
    private Transform previousTrackingTarget;
    private Transform previousLookAtTarget;

    private BattleHudBinder battleHudBinder;

    private PlayerHealth hiddenModelTarget;
    private readonly Dictionary<Renderer, bool> previousRendererStates = new();

    private readonly List<PlayerHealth> spectatorTargets = new();

    private PlayerHealth localPlayer;
    private PlayerHealth currentTarget;
    private Transform spectatorCameraTarget;

    private float deadTimer;
    private bool wasLocalDead;
    private bool isSpectating;
    private bool switchInputHeld;
    private int targetIndex;
    public bool IsSpectatingActive =>
        isSpectating && (currentTarget != null ||
        (networkLauncher != null && networkLauncher.IsDedicatedSpectator));

    public PlayerHealth CurrentSpectatorTarget => currentTarget;

    private void Awake()
    {
        FindCinemachineCameraIfNeeded();

        GameObject targetObject = new GameObject("SpectatorCameraTarget");
        spectatorCameraTarget = targetObject.transform;
        battleHudBinder = FindFirstObjectByType<BattleHudBinder>(FindObjectsInactive.Include);
    }

    private void OnDisable()
    {
        if (isSpectating)
        {
            RestoreLocalCamera();
        }

        ResetSpectatorState();
    }

    private void OnDestroy()
    {
        RestoreHiddenSpectatedTargetModel();

        if (spectatorCameraTarget != null)
        {
            Destroy(spectatorCameraTarget.gameObject);
        }
    }

    private void Update()
    {
        if (networkLauncher == null)
        {
            networkLauncher = FindFirstObjectByType<NetworkLauncher>();
        }

        // 観戦専用端末と役割未選択の端末にはローカルPlayerが存在しない。
        if (networkLauncher != null && networkLauncher.IsConnected && !networkLauncher.IsLocalCombatant)
        {
            UpdateSpectatorView();
            return;
        }

        FindLocalPlayerIfNeeded();
        if (localPlayer == null || localPlayer.Object == null || !localPlayer.Object.IsValid ||
            !CanSpectateInCurrentPhase() || !localPlayer.IsDead)
        {
            if (isSpectating)
            {
                RestoreLocalCamera();
            }
            ResetSpectatorState();
            return;
        }

        if (!wasLocalDead)
        {
            deadTimer = 0f;
            wasLocalDead = true;
        }
        deadTimer += Time.deltaTime;
        if (deadTimer >= spectatorDelay)
        {
            UpdateSpectatorView();
        }
    }

    private void UpdateSpectatorView()
    {
        RefreshSpectatorTargets();
        if (spectatorTargets.Count == 0)
        {
            ClearSpectatorTarget(CanShowWaitingOverview());
        }
        else if (!isSpectating || !IsValidSpectatorTarget(currentTarget))
        {
            targetIndex = Mathf.Clamp(targetIndex, 0, spectatorTargets.Count - 1);
            ApplySpectatorTarget(targetIndex);
        }
        else
        {
            // 退出で配列の位置が詰まっても、現在の対象から隣の対象へ切り替える。
            targetIndex = spectatorTargets.IndexOf(currentTarget);
        }
        HandleSwitchInput();
    }

    private void RememberCameraTargets()
    {
        FindCinemachineCameraIfNeeded();
        if (savedCameraTargets || cinemachineCamera == null)
        {
            return;
        }
        previousTrackingTarget = cinemachineCamera.Target.TrackingTarget;
        previousLookAtTarget = cinemachineCamera.Target.LookAtTarget;
        savedCameraTargets = true;
    }

    private bool CanShowWaitingOverview()
    {
        RoundManager manager = RoundManager.Instance;
        if (manager == null || !manager.IsNetworkReady || !manager.IsWaitingForReady || hasSpawnedCombatants)
        {
            return false;
        }

        // キャラクターの生成待ちも対戦者として扱い、同期途中で固定視点へ飛ばないようにする。
        foreach (var participant in manager.ConnectedParticipants)
        {
            if (participant.Value == ParticipantRole.Combatant)
            {
                return false;
            }
        }
        return true;
    }

    private void ClearSpectatorTarget(bool showWaitingOverview)
    {
        RememberCameraTargets();
        RestoreHiddenSpectatedTargetModel();
        if (!isSpectating || currentTarget != null)
        {
            if (battleHudBinder != null)
            {
                battleHudBinder.ClearSpectatorTarget();
            }
        }
        if (!isSpectating && cinemachineCamera != null && spectatorCameraTarget != null)
        {
            spectatorCameraTarget.SetPositionAndRotation(cinemachineCamera.transform.position, cinemachineCamera.transform.rotation);
        }

        currentTarget = null;
        IsWaitingOverview = showWaitingOverview;
        isSpectating = true;
        UpdateSpectatorCameraTarget();
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Target.TrackingTarget = spectatorCameraTarget;
            cinemachineCamera.Target.LookAtTarget = spectatorCameraTarget;
        }
    }

    private bool CanSpectateInCurrentPhase()
    {
        RoundManager roundManager = RoundManager.Instance;

        return
            roundManager != null &&
            roundManager.IsNetworkReady &&
            roundManager.IsRoundPlaying;
    }

    private void LateUpdate()
    {
        UpdateSpectatorCameraTarget();
    }

    private void UpdateSpectatorCameraTarget()
    {
        if (!isSpectating)
        {
            return;
        }

        if (currentTarget == null)
        {
            // 戦闘終了直後や対象の再生成待ちは、最後のプレイヤー視点の位置を保つ。
            if (IsWaitingOverview && spectatorCameraTarget != null)
            {
                Vector3 position = overviewCameraPoint != null ? overviewCameraPoint.position : defaultOverviewPosition;
                Quaternion rotation = overviewCameraPoint != null ? overviewCameraPoint.rotation
                    : Quaternion.LookRotation(-position.normalized, Vector3.up);
                spectatorCameraTarget.SetPositionAndRotation(position, rotation);
            }
            return;
        }

        if (spectatorCameraTarget == null)
        {
            return;
        }

        PlayerController targetController = currentTarget.GetComponent<PlayerController>();

        // 他プレイヤーのローカル視点計算に依存せず、StateAuthorityが同期した観戦用視点を優先する。
        if (targetController != null && targetController.HasSpectatorView)
        {
            Quaternion rotation = Quaternion.LookRotation(
                targetController.NetworkedSpectatorViewForward,
                targetController.NetworkedSpectatorViewUp
            );

            spectatorCameraTarget.SetPositionAndRotation(
                targetController.NetworkedSpectatorCameraPosition,
                rotation
            );

            return;
        }

        PlayerCamera targetCamera = currentTarget.GetComponent<PlayerCamera>();

        if (targetCamera != null && targetCamera.CameraTarget != null)
        {
            targetCamera.UpdateCameraTarget();

            spectatorCameraTarget.SetPositionAndRotation(
                targetCamera.CameraTarget.position,
                targetCamera.CameraTarget.rotation
            );
        }
    }

    private void FindCinemachineCameraIfNeeded()
    {
        if (cinemachineCamera != null)
        {
            return;
        }

        cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
    }

    private void FindLocalPlayerIfNeeded()
    {
        if (localPlayer != null &&
            localPlayer.Object != null &&
            localPlayer.Object.IsValid && localPlayer.Object.HasInputAuthority)
        {
            return;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (PlayerHealth player in players)
        {
            if (player == null || player.Object == null)
            {
                continue;
            }

            if (player.Object.IsValid && player.Object.HasInputAuthority)
            {
                localPlayer = player;
                return;
            }
        }

        localPlayer = null;
    }

    private void RefreshSpectatorTargets()
    {
        spectatorTargets.Clear();
        hasSpawnedCombatants = false;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (PlayerHealth player in players)
        {
            if (player != null && player.Object != null && player.Object.IsValid &&
                player.Object.InputAuthority != PlayerRef.None)
            {
                hasSpawnedCombatants = true;
            }
            if (!IsValidSpectatorTarget(player))
            {
                continue;
            }

            spectatorTargets.Add(player);
        }

        // 表示番号順で並べ、端末や探索順序によって切替順が変わらないようにする。
        spectatorTargets.Sort((a, b) =>
        {
            PlayerIdentity aIdentity = a.GetComponent<PlayerIdentity>();
            PlayerIdentity bIdentity = b.GetComponent<PlayerIdentity>();
            int aNumber = aIdentity != null ? aIdentity.PlayerNumber : a.GetInstanceID();
            int bNumber = bIdentity != null ? bIdentity.PlayerNumber : b.GetInstanceID();
            return aNumber.CompareTo(bNumber);
        });

        if (targetIndex >= spectatorTargets.Count)
        {
            targetIndex = 0;
        }
    }

    private bool IsValidSpectatorTarget(PlayerHealth player)
    {
        if (player == null)
        {
            return false;
        }

        if (player == localPlayer)
        {
            return false;
        }

        if (player.Object == null || !player.Object.IsValid)
        {
            return false;
        }

        // 1人用の死亡後観戦テストではダミーを許可する。展示用観戦では実際の対戦者だけを列挙する。
        if (player.Object.InputAuthority == PlayerRef.None &&
            ((networkLauncher != null && networkLauncher.IsDedicatedSpectator) ||
            !player.name.StartsWith("DebugSpectatorDummy")))
        {
            return false;
        }

        if (player.IsDead)
        {
            return false;
        }

        return true;
    }

    private void HandleSwitchInput()
    {
        int direction = ReadSwitchDirection();

        if (direction == 0)
        {
            switchInputHeld = false;
            return;
        }

        if (switchInputHeld)
        {
            return;
        }

        switchInputHeld = true;

        if (spectatorTargets.Count == 0)
        {
            return;
        }

        if (currentTarget == null)
        {
            ApplySpectatorTarget(Mathf.Clamp(targetIndex, 0, spectatorTargets.Count - 1));
            return;
        }

        targetIndex += direction;

        if (targetIndex < 0)
        {
            targetIndex = spectatorTargets.Count - 1;
        }
        else if (targetIndex >= spectatorTargets.Count)
        {
            targetIndex = 0;
        }

        ApplySpectatorTarget(targetIndex);
    }

    private int ReadSwitchDirection()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                return -1;
            }

            if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                return 1;
            }
        }

        if (Gamepad.current != null)
        {
            float stickX = Gamepad.current.leftStick.ReadValue().x;

            if (stickX <= -stickSwitchThreshold)
            {
                return -1;
            }

            if (stickX >= stickSwitchThreshold)
            {
                return 1;
            }
        }

        return 0;
    }

    private void ApplySpectatorTarget(int index)
    {
        FindCinemachineCameraIfNeeded();

        if (cinemachineCamera == null)
        {
            Debug.LogWarning("[SpectatorCameraController] CinemachineCamera was not found.");
            return;
        }

        if (index < 0 || index >= spectatorTargets.Count)
        {
            return;
        }

        PlayerHealth targetHealth = spectatorTargets[index];

        if (!IsValidSpectatorTarget(targetHealth))
        {
            return;
        }

        RememberCameraTargets();
        IsWaitingOverview = false;
        isSpectating = true;
        currentTarget = targetHealth;

        if (battleHudBinder == null)
        {
            battleHudBinder = FindFirstObjectByType<BattleHudBinder>(FindObjectsInactive.Include);
        }

        if (battleHudBinder != null)
        {
            battleHudBinder.BindToSpectatorTarget(currentTarget);
        }

        HideSpectatedTargetModel(currentTarget);

        UpdateSpectatorCameraTarget();

        cinemachineCamera.Target.TrackingTarget = spectatorCameraTarget;
        cinemachineCamera.Target.LookAtTarget = spectatorCameraTarget;

        Debug.Log($"[SpectatorCameraController] Spectating: {targetHealth.name}");
    }

    private void RestoreLocalCamera()
    {
        RestoreHiddenSpectatedTargetModel();

        FindCinemachineCameraIfNeeded();

        if (cinemachineCamera == null)
        {
            return;
        }

        PlayerCamera localCamera = localPlayer != null ? localPlayer.GetComponent<PlayerCamera>() : null;

        if (localCamera == null || localCamera.CameraTarget == null)
        {
            // 切断・役割変更で自分のPlayerが消えた場合も、破棄予定の追従先を保持しない。
            cinemachineCamera.Target.TrackingTarget = previousTrackingTarget;
            cinemachineCamera.Target.LookAtTarget = previousLookAtTarget;
            return;
        }

        localCamera.UpdateCameraTarget();

        cinemachineCamera.Target.TrackingTarget = localCamera.CameraTarget;
        cinemachineCamera.Target.LookAtTarget = localCamera.CameraTarget;

        Debug.Log("[SpectatorCameraController] Restored local camera.");
    }

    private void ResetSpectatorState()
    {
        RestoreHiddenSpectatedTargetModel();

        if (isSpectating && battleHudBinder != null)
        {
            battleHudBinder.BindToLocalPlayer();
        }

        savedCameraTargets = false;
        previousTrackingTarget = null;
        previousLookAtTarget = null;
        IsWaitingOverview = false;
        deadTimer = 0.0f;
        wasLocalDead = false;
        isSpectating = false;
        switchInputHeld = false;
        currentTarget = null;
        spectatorTargets.Clear();
        targetIndex = 0;
    }

    private void HideSpectatedTargetModel(PlayerHealth target)
    {
        if (target == null)
        {
            return;
        }

        if (hiddenModelTarget == target)
        {
            return;
        }

        RestoreHiddenSpectatedTargetModel();

        hiddenModelTarget = target;
        previousRendererStates.Clear();

        SuppressRenderers(target.BodyRenderers ?? System.Array.Empty<Renderer>());
        if (target.OverheadIconRoot != null)
        {
            // Rootの有効状態はPlayerHealthが死亡・復活時に変更するため、描画だけを抑止する。
            SuppressRenderers(target.OverheadIconRoot.GetComponentsInChildren<Renderer>(true));
        }

        Debug.Log($"[SpectatorCameraController] Hide spectated model: {target.name}");
    }

    private void SuppressRenderers(Renderer[] renderers)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || previousRendererStates.ContainsKey(renderer))
            {
                continue;
            }

            // enabledやSetActiveで再表示されても、観戦対象の体とアイコンは描画しない。
            previousRendererStates.Add(renderer, renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }
    }

    private void RestoreHiddenSpectatedTargetModel()
    {
        if (previousRendererStates.Count == 0 && hiddenModelTarget == null)
        {
            return;
        }

        foreach (KeyValuePair<Renderer, bool> pair in previousRendererStates)
        {
            Renderer renderer = pair.Key;

            if (renderer == null)
            {
                continue;
            }

            renderer.forceRenderingOff = pair.Value;
        }

        previousRendererStates.Clear();

        hiddenModelTarget = null;

        Debug.Log("[SpectatorCameraController] Restore spectated model.");
    }
}