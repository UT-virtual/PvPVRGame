using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 5.0f;

    [Header("Look")]
    [SerializeField] private float keyboardLookSpeed = 90.0f;
    [SerializeField] private float mouseLookSpeed = 0.12f;
    [SerializeField] private float gamepadLookSpeed = 120.0f;
    [SerializeField] private float minPitch = -60.0f;
    [SerializeField] private float maxPitch = 70.0f;

    [Header("Gravity / Jump")]
    [SerializeField] private float gravity = 25.0f;
    [SerializeField] private float jumpSpeed = 8.0f;

    [Header("Ground Check")]
    [SerializeField] private Transform planetCenter;
    [SerializeField] private LayerMask planetLayer;
    [SerializeField] private float groundProbeStartOffset = 2.0f;
    [SerializeField] private float groundProbeDistance = 6.0f;
    [SerializeField] private float groundProbeRadius = 0.2f;
    [SerializeField] private float groundSnapDistance = 0.5f;
    [SerializeField] private float groundSnapSpeed = 20.0f;

    [Header("Rotation")]
    [SerializeField] private float alignSpeed = 12.0f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float cameraTargetHeight = 1.3f;

    private CharacterController controller;

    private Vector3 surfaceUp = Vector3.up;
    private Vector3 aimForward = Vector3.forward;
    private Vector3 aimRight = Vector3.right;

    private float verticalSpeed;
    private float pitch;

    private bool hasGroundHit;
    private bool isGrounded;
    private float groundDistance;
    private RaycastHit groundHit;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        InitializeSurfaceVectors();
        InitializeCameraTarget();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        ProbeGround();
        UpdateAimBasis();

        Look();
        MoveOnSurface();

        // 横移動後にもう一度地面を確認する
        ProbeGround();
        UpdateAimBasis();

        AlignToSurface();
        ApplyGravityAndJump();
        UpdateCameraTarget();
    }

    private void InitializeSurfaceVectors()
    {
        if (planetCenter != null)
        {
            surfaceUp = (transform.position - planetCenter.position).normalized;
        }
        else
        {
            surfaceUp = transform.up;
        }

        aimForward = Vector3.ProjectOnPlane(transform.forward, surfaceUp);

        if (aimForward.sqrMagnitude < 0.001f)
        {
            aimForward = Vector3.ProjectOnPlane(Vector3.forward, surfaceUp);
        }

        if (aimForward.sqrMagnitude < 0.001f)
        {
            aimForward = Vector3.Cross(Vector3.right, surfaceUp);
        }

        aimForward.Normalize();

        aimRight = Vector3.Cross(surfaceUp, aimForward).normalized;
        aimForward = Vector3.Cross(aimRight, surfaceUp).normalized;
    }

    private void InitializeCameraTarget()
    {
        if (cameraTarget == null)
        {
            return;
        }

        Vector3 targetPosition = transform.position + surfaceUp * cameraTargetHeight;
        Quaternion targetRotation = Quaternion.LookRotation(aimForward, surfaceUp);

        cameraTarget.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void ProbeGround()
    {
        Vector3 probeOrigin = transform.position + surfaceUp * groundProbeStartOffset;
        Vector3 probeDirection = -surfaceUp;

        hasGroundHit = Physics.SphereCast(
            probeOrigin,
            groundProbeRadius,
            probeDirection,
            out groundHit,
            groundProbeDistance,
            planetLayer,
            QueryTriggerInteraction.Ignore
        );

        if (hasGroundHit)
        {
            surfaceUp = groundHit.normal;

            Vector3 desiredPosition = groundHit.point + surfaceUp * GetDesiredGroundOffset();
            groundDistance = Vector3.Dot(transform.position - desiredPosition, surfaceUp);

            // 上昇中は、地面が近くても接地扱いにしない
            isGrounded =
                verticalSpeed <= 0.0f &&
                groundDistance <= groundSnapDistance;

            return;
        }

        isGrounded = false;
        groundDistance = float.MaxValue;

        if (planetCenter != null)
        {
            surfaceUp = (transform.position - planetCenter.position).normalized;
        }
    }

    private float GetDesiredGroundOffset()
    {
        // CharacterControllerの足元が地面に接するように、
        // transform.positionから地面まで必要な距離を計算する。
        //
        // 例:
        // height = 2, center.y = 1 の場合、transform.positionは足元付近なので offset はほぼ0。
        // height = 2, center.y = 0 の場合、transform.positionは中心付近なので offset は約1。
        float offset = controller.height * 0.5f - controller.center.y + controller.skinWidth;

        return Mathf.Max(0.02f, offset);
    }

    private void UpdateAimBasis()
    {
        aimForward = Vector3.ProjectOnPlane(aimForward, surfaceUp);

        if (aimForward.sqrMagnitude < 0.001f)
        {
            aimForward = Vector3.ProjectOnPlane(transform.forward, surfaceUp);
        }

        if (aimForward.sqrMagnitude < 0.001f)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(surfaceUp, Vector3.up)) < 0.99f
                ? Vector3.up
                : Vector3.forward;

            aimForward = Vector3.Cross(reference, surfaceUp);
        }

        aimForward.Normalize();

        aimRight = Vector3.Cross(surfaceUp, aimForward).normalized;
        aimForward = Vector3.Cross(aimRight, surfaceUp).normalized;
    }

    private void Look()
    {
        Vector2 lookInput = ReadLookInput();

        float yawAmount = lookInput.x;
        float pitchAmount = lookInput.y;

        if (Mathf.Abs(yawAmount) > 0.001f)
        {
            Quaternion yawRotation = Quaternion.AngleAxis(yawAmount, surfaceUp);

            aimForward = yawRotation * aimForward;
            aimForward = Vector3.ProjectOnPlane(aimForward, surfaceUp).normalized;

            aimRight = Vector3.Cross(surfaceUp, aimForward).normalized;
            aimForward = Vector3.Cross(aimRight, surfaceUp).normalized;
        }

        if (Mathf.Abs(pitchAmount) > 0.001f)
        {
            pitch -= pitchAmount;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    private Vector2 ReadLookInput()
    {
        Vector2 lookInput = Vector2.zero;

        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            lookInput.x += mouseDelta.x * mouseLookSpeed;
            lookInput.y += mouseDelta.y * mouseLookSpeed;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed)
            {
                lookInput.x -= keyboardLookSpeed * Time.deltaTime;
            }

            if (Keyboard.current.rightArrowKey.isPressed)
            {
                lookInput.x += keyboardLookSpeed * Time.deltaTime;
            }

            if (Keyboard.current.upArrowKey.isPressed)
            {
                lookInput.y += keyboardLookSpeed * Time.deltaTime;
            }

            if (Keyboard.current.downArrowKey.isPressed)
            {
                lookInput.y -= keyboardLookSpeed * Time.deltaTime;
            }
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.rightStick.ReadValue();

            lookInput.x += stick.x * gamepadLookSpeed * Time.deltaTime;
            lookInput.y += stick.y * gamepadLookSpeed * Time.deltaTime;
        }

        return lookInput;
    }

    private void MoveOnSurface()
    {
        Vector2 input = ReadMoveInput();

        if (input.sqrMagnitude < 0.01f)
        {
            return;
        }

        if (input.sqrMagnitude > 1.0f)
        {
            input.Normalize();
        }

        Vector3 moveDir = aimForward * input.y + aimRight * input.x;

        if (moveDir.sqrMagnitude < 0.001f)
        {
            return;
        }

        if (moveDir.sqrMagnitude > 1.0f)
        {
            moveDir.Normalize();
        }

        controller.Move(moveDir * moveSpeed * Time.deltaTime);
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

        if (Gamepad.current != null)
        {
            input = Gamepad.current.leftStick.ReadValue();
        }

        if (Keyboard.current != null)
        {
            Vector2 keyboardInput = Vector2.zero;

            if (Keyboard.current.wKey.isPressed)
            {
                keyboardInput.y += 1.0f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                keyboardInput.y -= 1.0f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                keyboardInput.x += 1.0f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                keyboardInput.x -= 1.0f;
            }

            if (keyboardInput.sqrMagnitude > 1.0f)
            {
                keyboardInput.Normalize();
            }

            if (keyboardInput.sqrMagnitude > 0.01f)
            {
                input = keyboardInput;
            }
        }

        return input;
    }

    private bool ReadJumpInput()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        // ゲームパッドのAボタン
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private void AlignToSurface()
    {
        Quaternion targetRotation = Quaternion.LookRotation(aimForward, surfaceUp);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            alignSpeed * Time.deltaTime
        );
    }

    private void ApplyGravityAndJump()
    {
        if (isGrounded && ReadJumpInput())
        {
            verticalSpeed = jumpSpeed;
            isGrounded = false;
        }
        else if (isGrounded)
        {
            // 接地中は毎フレーム下向きに押し込まない。
            // これを0にしないと、CharacterControllerがPlanetに押し込まれて
            // 反発でY座標が増え続けることがある。
            verticalSpeed = 0.0f;

            SnapToGround();
            return;
        }
        else
        {
            verticalSpeed -= gravity * Time.deltaTime;
        }

        Vector3 verticalMove = surfaceUp * verticalSpeed;
        controller.Move(verticalMove * Time.deltaTime);
    }

    private void SnapToGround()
    {
        if (!hasGroundHit)
        {
            return;
        }

        if (Mathf.Abs(groundDistance) < 0.001f)
        {
            return;
        }

        float snapAmount = Mathf.Min(
            Mathf.Abs(groundDistance),
            groundSnapSpeed * Time.deltaTime
        );

        Vector3 snapDirection;

        if (groundDistance > 0.0f)
        {
            // 地面より少し浮いているので地面方向へ寄せる
            snapDirection = -surfaceUp;
        }
        else
        {
            // 地面に少しめり込んでいるので外側へ戻す
            snapDirection = surfaceUp;
        }

        controller.Move(snapDirection * snapAmount);
    }

    private void UpdateCameraTarget()
    {
        if (cameraTarget == null)
        {
            return;
        }

        Vector3 pitchedForward = Quaternion.AngleAxis(pitch, aimRight) * aimForward;
        pitchedForward.Normalize();

        Vector3 targetPosition = transform.position + surfaceUp * cameraTargetHeight;
        Quaternion targetRotation = Quaternion.LookRotation(pitchedForward, surfaceUp);

        cameraTarget.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 up = Application.isPlaying ? surfaceUp : transform.up;
        Vector3 rayOrigin = transform.position + up * groundProbeStartOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(rayOrigin, rayOrigin - up * groundProbeDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(rayOrigin, groundProbeRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + aimForward * 2.0f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + aimRight * 2.0f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + surfaceUp * 2.0f);
    }
}