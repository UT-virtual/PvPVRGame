using System;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerLook))]
[RequireComponent(typeof(PlayerCamera))]
[RequireComponent(typeof(PlayerWeapon))]
public class PlayerController : NetworkBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;

    private PlayerHealth playerHealth;
    private PlayerMove playerMove;
    private PlayerLook playerLook;
    private PlayerCamera playerCamera;
    private PlayerWeapon playerWeapon;

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    [Networked] private NetworkBool NetworkedIsRunning { get; set; }
    [Networked] private float NetworkedMoveX { get; set; }
    [Networked] private float NetworkedMoveY { get; set; }

    public event Action OnTookDamage;
    public event Action OnDied;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMove = GetComponent<PlayerMove>();
        playerLook = GetComponent<PlayerLook>();
        playerCamera = GetComponent<PlayerCamera>();
        playerWeapon = GetComponent<PlayerWeapon>();

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
        }

        Debug.Log(
            $"PlayerController Spawned. " +
            $"Name={gameObject.name}, " +
            $"InputAuthority={Object.InputAuthority}, " +
            $"HasInputAuthority={Object.HasInputAuthority}, " +
            $"HasStateAuthority={Object.HasStateAuthority}"
        );
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
        bool fireHeld = input.Buttons.IsSet((int)PlayerInputButton.Fire);

        Vector2 moveInput = input.MoveInput;

        if (moveInput.sqrMagnitude > 1.0f)
        {
            moveInput.Normalize();
        }

        NetworkedMoveX = moveInput.x;
        NetworkedMoveY = moveInput.y;
        NetworkedIsRunning = moveInput.sqrMagnitude > 0.01f;

        if (readyPressed && RoundManager.Instance != null)
        {
            RoundManager.Instance.SetPlayerReady(playerHealth);
        }

        playerWeapon.Tick(deltaTime);

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        if (!Object.HasInputAuthority)
        {
            if (input.HasLookDirection != 0)
            {
                playerMove.SetAimForward(input.AimForward);
                playerLook.SetPitchFromViewForward(input.ViewForward);
            }
            else
            {
                playerLook.ApplyLook(input.LookInput);
            }
        }

        playerMove.MoveOnSurface(moveInput, deltaTime);

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        playerMove.AlignToSurface(deltaTime);
        playerMove.ApplyGravityAndJump(jumpPressed, deltaTime);

        bool canUseWeapon = RoundManager.Instance == null || RoundManager.Instance.CanUseWeapons;

        if (!canUseWeapon)
        {
            return;
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
        if (Object == null || !Object.HasInputAuthority)
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

    public void ApplyLocalLook(Vector2 lookInput)
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        if (lookInput.sqrMagnitude < 0.000001f)
        {
            return;
        }

        playerLook.ApplyLook(lookInput);
    }
}