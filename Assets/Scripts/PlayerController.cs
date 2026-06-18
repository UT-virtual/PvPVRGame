using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerLook))]
[RequireComponent(typeof(PlayerCamera))]
[RequireComponent(typeof(PlayerWeapon))]
public class PlayerController : MonoBehaviour
{
    private PlayerHealth playerHealth;
    private PlayerInput playerInput;
    private PlayerMove playerMove;
    private PlayerLook playerLook;
    private PlayerCamera playerCamera;
    private PlayerWeapon playerWeapon;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerInput = GetComponent<PlayerInput>();
        playerMove = GetComponent<PlayerMove>();
        playerLook = GetComponent<PlayerLook>();
        playerCamera = GetComponent<PlayerCamera>();
        playerWeapon = GetComponent<PlayerWeapon>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        playerInput.ReadInput();

        playerWeapon.Tick(Time.deltaTime);

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        playerLook.ApplyLook(playerInput.LookInput);

        if (playerInput.ReloadPressed)
        {
            playerWeapon.ReloadAmmo();
        }

        if (playerInput.FireHeld)
        {
            playerWeapon.TryFireProjectile();
        }

        playerMove.MoveOnSurface(playerInput.MoveInput);

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        playerMove.AlignToSurface();
        playerMove.ApplyGravityAndJump(playerInput.JumpPressed);

        playerCamera.UpdateCameraTarget();
    }
}