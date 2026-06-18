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
     * Client側の自分のPlayerでは、カメラ反応用に視点だけ先に更新する。
     * 移動はここではしない。
     */
    if (Object.HasInputAuthority && !Object.HasStateAuthority)
    {
        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        // Client側の見た目・カメラ用の向き
        playerLook.ApplyLook(input.LookInput);
    }

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
     * Host側の正式な向き更新。
     *
     * Host自身のPlayer:
     *   Hostは自分で入力を処理するので LookInput を使う。
     *
     * ClientのPlayer:
     *   Clientが実際に向いている AimForward / ViewForward を使う。
     *   これにより、Client画面の向きとHostから見える向きがズレにくくなる。
     */
    if (Object.HasInputAuthority)
    {
        // Host自身のPlayer用。二重回転はしない。
        playerLook.ApplyLook(input.LookInput);
    }
    else if (input.HasLookDirection != 0)
    {
        // ClientのPlayer用。Clientが送ってきた実際の向きに合わせる。
        playerMove.SetAimForward(input.AimForward);
        playerLook.SetPitchFromViewForward(input.ViewForward);
    }
    else
    {
        // 念のためのフォールバック
        playerLook.ApplyLook(input.LookInput);
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
}