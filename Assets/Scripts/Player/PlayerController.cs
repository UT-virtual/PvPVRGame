using Fusion;
using UnityEngine;

/// <summary>
/// 受信入力をStateAuthorityのネットワークTickで解釈し、移動・武器・スキル・準備操作を各コンポーネントへ振り分ける。
/// InputAuthority側の視点は別途即時更新し、アニメーションと観戦用視点には同期状態を用いる。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerLook))]
[RequireComponent(typeof(PlayerCamera))]
[RequireComponent(typeof(PlayerWeapon))]
[RequireComponent(typeof(PlayerSkillController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;

    private PlayerHealth playerHealth;
    private PlayerMove playerMove;
    private PlayerLook playerLook;
    private PlayerCamera playerCamera;
    private PlayerWeapon playerWeapon;
    private PlayerSkillController playerSkillController;

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    [Networked] private NetworkBool NetworkedIsRunning { get; set; }
    [Networked] private float NetworkedMoveX { get; set; }
    [Networked] private float NetworkedMoveY { get; set; }
    [Networked] public Vector3 NetworkedSpectatorCameraPosition { get; private set; }
    [Networked] public Vector3 NetworkedSpectatorViewForward { get; private set; }
    [Networked] public Vector3 NetworkedSpectatorViewUp { get; private set; }

    public bool HasSpectatorView =>
        NetworkedSpectatorViewForward.sqrMagnitude > 0.001f &&
        NetworkedSpectatorViewUp.sqrMagnitude > 0.001f;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMove = GetComponent<PlayerMove>();
        playerLook = GetComponent<PlayerLook>();
        playerCamera = GetComponent<PlayerCamera>();
        playerWeapon = GetComponent<PlayerWeapon>();
        playerSkillController = GetComponent<PlayerSkillController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();

            if (launcher != null)
            {
                launcher.RegisterLocalPlayer(this);
                launcher.NotifyLocalPlayerSpawnedOnWaitingPlanet();
            }
            else
            {
                Debug.LogWarning("[PlayerController] NetworkLauncher was not found.");
            }
        }

        Debug.Log(
            $"PlayerController Spawned. " +
            $"Name={gameObject.name}, " +
            $"InputAuthority={Object.InputAuthority}, " +
            $"HasInputAuthority={Object.HasInputAuthority}, " +
            $"HasStateAuthority={Object.HasStateAuthority}"
        );
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (!Object.HasInputAuthority)
        {
            return;
        }

        NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();

        if (launcher != null)
        {
            launcher.UnregisterLocalPlayer(this);
        }
    }

    /// <summary>
    /// StateAuthorityで入力の押下差分を判定し、フェーズの許可に従って操作を実行する。
    /// この実装ではInputAuthorityだけを持つ端末で移動・戦闘処理は実行しない。
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (Object.HasStateAuthority)
            {
                NetworkedIsRunning = false;
                NetworkedMoveX = 0.0f;
                NetworkedMoveY = 0.0f;
            }

            return;
        }

        if (!GetInput(out PlayerNetworkInput input))
        {
            return;
        }

        float deltaTime = Runner.DeltaTime;

        if (!Object.HasStateAuthority)
        {
            return;
        }

        NetworkButtons pressedButtons = input.Buttons.GetPressed(PreviousButtons);
        PreviousButtons = input.Buttons;

        bool readyPressed = pressedButtons.IsSet((int)PlayerInputButton.Ready);

        if (readyPressed && RoundManager.Instance != null)
        {
            RoundManager.Instance.SetPlayerReady(playerHealth);
        }

        if (RoundManager.Instance != null && RoundManager.Instance.IsSkillSelecting)
        {
            HandleSkillSelectionInput(pressedButtons);

            NetworkedMoveX = 0.0f;
            NetworkedMoveY = 0.0f;
            NetworkedIsRunning = false;

            return;
        }

        bool canControlPlayer = RoundManager.Instance == null || RoundManager.Instance.CanControlPlayers;

        if (!canControlPlayer)
        {
            NetworkedMoveX = 0.0f;
            NetworkedMoveY = 0.0f;
            NetworkedIsRunning = false;

            playerWeapon.Tick(deltaTime);
            return;
        }

        playerWeapon.Tick(deltaTime);
        ApplyMovementInput(input, pressedButtons.IsSet((int)PlayerInputButton.Jump), deltaTime);
        UpdateNetworkedSpectatorView();
        ApplyCombatInput(input, pressedButtons);
    }

    private void ApplyMovementInput(PlayerNetworkInput input, bool jumpPressed, float deltaTime)
    {
        Vector2 moveInput = input.MoveInput;

        if (moveInput.sqrMagnitude > 1.0f)
        {
            moveInput.Normalize();
        }

        NetworkedMoveX = moveInput.x;
        NetworkedMoveY = moveInput.y;
        NetworkedIsRunning = moveInput.sqrMagnitude > 0.01f;

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        // ローカル視点は入力収集時に反映済み。StateAuthorityでリモート所有者の視線だけを復元する。
        if (!Object.HasInputAuthority)
        {
            if (input.IsVR)
            {
                playerLook.ApplyLook(input.LookInput, true, input.HMDRotation);
            }
            else
            {
                if (input.HasLookDirection != 0)
                {
                    playerMove.SetAimForward(input.AimForward);
                    playerLook.SetPitchFromViewForward(input.ViewForward);
                }
                else
                {
                    playerLook.ApplyLook(input.LookInput, false, Quaternion.identity);
                }
            }
        }

        if (input.IsVR)
        {
            playerMove.MoveOnSurfaceVR(moveInput, deltaTime, playerLook.ViewForward, playerMove.AimRight);
        }
        else
        {
            playerMove.MoveOnSurface(moveInput, deltaTime);
        }

        // 移動先で法線が変わるため、姿勢整列と重力の前に接地基準を再取得する。
        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        playerMove.AlignToSurface(deltaTime);
        playerMove.ApplyGravityAndJump(jumpPressed, deltaTime);
    }

    private void ApplyCombatInput(PlayerNetworkInput input, NetworkButtons pressedButtons)
    {
        bool reloadPressed = pressedButtons.IsSet((int)PlayerInputButton.Reload);
        bool skillPressed = pressedButtons.IsSet((int)PlayerInputButton.Skill);
        bool switchSkillPressed = pressedButtons.IsSet((int)PlayerInputButton.SwitchSkill);
        bool activateSkill1Pressed = pressedButtons.IsSet((int)PlayerInputButton.ActivateSkill1);
        bool activateSkill2Pressed = pressedButtons.IsSet((int)PlayerInputButton.ActivateSkill2);
        bool fireHeld = input.Buttons.IsSet((int)PlayerInputButton.Fire);

        bool canUseWeapon = RoundManager.Instance == null || RoundManager.Instance.CanUseWeapons;

        if (!canUseWeapon)
        {
            return;
        }

        if (switchSkillPressed && playerSkillController != null)
        {
            playerSkillController.SwitchCurrentSkill();
        }

        if (activateSkill1Pressed && playerSkillController != null)
        {
            playerSkillController.TryActivateSkillBySlot(0);
        }
        if (activateSkill2Pressed && playerSkillController != null)
        {
            playerSkillController.TryActivateSkillBySlot(1);
        }

        if (skillPressed && playerSkillController != null)
        {
            playerSkillController.TryActivateSkill();
        }

        if (reloadPressed)
        {
            playerWeapon.ReloadAmmo();
        }

        if (fireHeld)
        {
            playerWeapon.TryFireProjectile();
        }
    }

    private void HandleSkillSelectionInput(NetworkButtons pressedButtons)
    {
        if (RoundManager.Instance == null)
        {
            return;
        }

        if (pressedButtons.IsSet((int)PlayerInputButton.SelectSkill1))
        {
            RoundManager.Instance.SelectSkillBySlot(playerHealth, 0);
        }

        if (pressedButtons.IsSet((int)PlayerInputButton.SelectSkill2))
        {
            RoundManager.Instance.SelectSkillBySlot(playerHealth, 1);
        }

        if (pressedButtons.IsSet((int)PlayerInputButton.SelectSkill3))
        {
            RoundManager.Instance.SelectSkillBySlot(playerHealth, 2);
        }

        if (pressedButtons.IsSet((int)PlayerInputButton.SelectSkill4))
        {
            RoundManager.Instance.SelectSkillBySlot(playerHealth, 3);
        }
    }

    private void UpdateNetworkedSpectatorView()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        Vector3 cameraPosition = playerCamera.CameraPosition;
        Vector3 viewForward = playerLook.ViewForward;
        Vector3 viewUp = playerLook.ViewUp;

        if (viewForward.sqrMagnitude < 0.001f)
        {
            viewForward = transform.forward;
        }

        if (viewUp.sqrMagnitude < 0.001f)
        {
            viewUp = transform.up;
        }

        NetworkedSpectatorCameraPosition = cameraPosition;
        NetworkedSpectatorViewForward = viewForward.normalized;
        NetworkedSpectatorViewUp = viewUp.normalized;
    }

    /// <summary>
    /// Tickで同期された移動状態をAnimatorへ反映する。
    /// ここでは移動や射撃などのゲーム状態を進めない。
    /// </summary>
    public override void Render()
    {
        if (animator == null)
        {
            return;
        }

        bool isRunning = NetworkedIsRunning && (playerHealth == null || !playerHealth.IsDead);

        animator.SetBool("isRunning", isRunning);
        animator.SetFloat("moveX", NetworkedMoveX);
        animator.SetFloat("moveY", NetworkedMoveY);
    }

    private void LateUpdate()
    {
        if (Object == null)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        playerCamera.UpdateCameraTarget();
    }

    public Vector3 GetNetworkAimForward()
    {
        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        return playerMove.AimForward;
    }

    public Vector3 GetNetworkViewForward()
    {
        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        return playerLook.ViewForward;
    }

    /// <summary>
    /// 入力収集のフレームで視点だけを即時反映する。
    /// StateAuthority側で同じ入力を扱う際は、InputAuthorityも持つ場合の二重適用を避ける。
    /// </summary>
    public void ApplyLocalLook(Vector2 lookInput, bool isVR, Quaternion hmdRotation)
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        if (!isVR && lookInput.sqrMagnitude < 0.000001f)
        {
            return;
        }

        playerLook.ApplyLook(lookInput, isVR, hmdRotation);
    }
}