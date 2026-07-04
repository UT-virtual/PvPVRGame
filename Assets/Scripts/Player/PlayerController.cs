using Fusion;
using UnityEngine;

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

        bool jumpPressed = pressedButtons.IsSet((int)PlayerInputButton.Jump);
        bool reloadPressed = pressedButtons.IsSet((int)PlayerInputButton.Reload);
        bool readyPressed = pressedButtons.IsSet((int)PlayerInputButton.Ready);
        bool skillPressed = pressedButtons.IsSet((int)PlayerInputButton.Skill);
        bool switchSkillPressed = pressedButtons.IsSet((int)PlayerInputButton.SwitchSkill);
        bool fireHeld = input.Buttons.IsSet((int)PlayerInputButton.Fire);

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

        Vector2 moveInput = input.MoveInput;

        if (moveInput.sqrMagnitude > 1.0f)
        {
            moveInput.Normalize();
        }

        NetworkedMoveX = moveInput.x;
        NetworkedMoveY = moveInput.y;
        NetworkedIsRunning = moveInput.sqrMagnitude > 0.01f;

        playerWeapon.Tick(deltaTime);

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

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

        //VRÇ≈ÇÕHMDÇÃå¸Ç´ÇéQè∆Ç∑ÇÈ
        if (input.IsVR)
        {
            playerMove.MoveOnSurfaceVR(moveInput, deltaTime, playerLook.ViewForward, playerMove.AimRight);
        }
        else
        {
            playerMove.MoveOnSurface(moveInput, deltaTime);
        }

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        playerMove.AlignToSurface(deltaTime);
        playerMove.ApplyGravityAndJump(jumpPressed, deltaTime);

        UpdateNetworkedSpectatorView();

        bool canUseWeapon = RoundManager.Instance == null || RoundManager.Instance.CanUseWeapons;

        if (!canUseWeapon)
        {
            return;
        }

        if (switchSkillPressed && playerSkillController != null)
        {
            playerSkillController.SwitchCurrentSkill();
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