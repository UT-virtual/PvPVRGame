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
    private PlayerHealth playerHealth;
    private PlayerMove playerMove;
    private PlayerLook playerLook;
    private PlayerCamera playerCamera;
    private PlayerWeapon playerWeapon;

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    public event Action OnTookDamage;
    public event Action OnDied;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMove = GetComponent<PlayerMove>();
        playerLook = GetComponent<PlayerLook>();
        playerCamera = GetComponent<PlayerCamera>();
        playerWeapon = GetComponent<PlayerWeapon>();
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

        playerMove.MoveOnSurface(input.MoveInput, deltaTime);

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