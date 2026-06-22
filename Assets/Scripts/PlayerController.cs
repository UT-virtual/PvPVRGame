using Fusion;
using UnityEngine;
using System;

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

    // イベント宣言
    public event Action OnTookDamage;   //被弾
    public event Action OnDied;         //死亡
    

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

        /*
        * 移動・ジャンプ・射撃・リロードの正式処理はHostだけ。
        */
        if (!Object.HasStateAuthority)
        {
            return;
        }

        NetworkButtons pressedButtons = input.Buttons.GetPressed(PreviousButtons);
        PreviousButtons = input.Buttons;

        bool jumpPressed = pressedButtons.IsSet((int)PlayerInputButton.Jump);
        bool reloadPressed = pressedButtons.IsSet((int)PlayerInputButton.Reload);
        bool fireHeld = input.Buttons.IsSet((int)PlayerInputButton.Fire);

        playerWeapon.Tick(deltaTime);

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        /*
        * Hostから見たClient Playerの向き。
        *
        * Host自身のPlayerは、すでに上の Object.HasInputAuthority ブロックで
        * ApplyLook済みなので、ここで二重にApplyLookしない。
        */
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

        playerCamera.UpdateCameraTarget();
    }

    public Vector3 GetNetworkAimForward()
    {
        return playerMove.AimForward;
    }

    public Vector3 GetNetworkViewForward()
    {
        return playerLook.ViewForward;
    }

    public void ApplyLocalLook(Vector2 lookInput)
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        if (lookInput.sqrMagnitude < 0.000001f)
        {
            return;
        }

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();
        playerLook.ApplyLook(lookInput);
    }
}