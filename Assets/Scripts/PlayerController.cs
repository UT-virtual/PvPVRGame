using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 5.0f;

    [Header("Look")]
    [SerializeField] private float keyboardLookSpeed = 90.0f;
    [SerializeField] private float mouseLookSpeed = 0.12f;
    [SerializeField] private float gamepadLookSpeed = 120.0f;

    [Header("Camera Pitch")]
    [SerializeField] private float minCameraPitch = -25.0f;
    [SerializeField] private float maxCameraPitch = 35.0f;

    [Header("Gravity / Jump")]
    [SerializeField] private float gravity = 25.0f;
    [SerializeField] private float jumpSpeed = 8.0f;

    [Header("Shoot")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float projectileSpeed = 18.0f;
    [SerializeField] private float projectileLifeTime = 3.0f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileSpawnHeight = 1.2f;
    [SerializeField] private float projectileSpawnForwardOffset = 0.8f;
    [SerializeField] private float projectileScale = 0.15f;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 20;
    [SerializeField] private float fireInterval = 0.15f;

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
    [SerializeField] private float cameraBackDistance = 6.0f;
    [SerializeField] private float cameraHeight = 4.0f;
    [SerializeField] private float cameraSideOffset = 0.0f;
    [SerializeField] private float cameraLookAtHeight = 1.2f;
    [SerializeField] private float cameraLookAheadDistance = 2.5f;

    private CharacterController controller;
    private PlayerHealth playerHealth;

    private Vector3 surfaceUp = Vector3.up;
    private Vector3 aimForward = Vector3.forward;
    private Vector3 aimRight = Vector3.right;

    private float verticalSpeed;
    private float cameraPitch;

    private bool hasGroundHit;
    private bool isGrounded;
    private float groundDistance;
    private RaycastHit groundHit;

    private int currentAmmo;
    private float fireTimer;
    private bool wasLeftTriggerPressed;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();

        if (aimCamera == null && Camera.main != null)
        {
            aimCamera = Camera.main;
        }

        currentAmmo = maxAmmo;
        fireTimer = 0.0f;

        InitializeSurfaceVectors();
        UpdateCameraTargetImmediate();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        if (fireTimer > 0.0f)
        {
            fireTimer -= Time.deltaTime;
        }

        ProbeGround();
        UpdateAimBasis();

        Look();

        if (ReadReloadInput())
        {
            ReloadAmmo();
        }

        if (ReadFireHeldInput())
        {
            TryFireProjectile();
        }

        MoveOnSurface();

        ProbeGround();
        UpdateAimBasis();

        AlignToSurface();
        ApplyGravityAndJump();
        UpdateCameraTargetImmediate();
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
            cameraPitch -= pitchAmount;
            cameraPitch = Mathf.Clamp(cameraPitch, minCameraPitch, maxCameraPitch);
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

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadFireHeldInput()
    {
        if (Keyboard.current != null && Keyboard.current.jKey.isPressed)
        {
            return true;
        }

        if (Gamepad.current != null)
        {
            return Gamepad.current.rightTrigger.ReadValue() > 0.5f;
        }

        return false;
    }

    private bool ReadReloadInput()
    {
        bool reload = false;

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            reload = true;
        }

        if (Gamepad.current != null)
        {
            float leftTriggerValue = Gamepad.current.leftTrigger.ReadValue();
            bool leftTriggerPressed = leftTriggerValue > 0.5f;

            if (leftTriggerPressed && !wasLeftTriggerPressed)
            {
                reload = true;
            }

            wasLeftTriggerPressed = leftTriggerPressed;
        }
        else
        {
            wasLeftTriggerPressed = false;
        }

        return reload;
    }

    private void ReloadAmmo()
    {
        currentAmmo = maxAmmo;
        Debug.Log($"Reloaded: {currentAmmo}/{maxAmmo}");
    }

    private void TryFireProjectile()
    {
        if (fireTimer > 0.0f)
        {
            return;
        }

        if (currentAmmo <= 0)
        {
            Debug.Log("No ammo. Press K or ZL to reload.");
            fireTimer = fireInterval;
            return;
        }

        FireProjectile();

        currentAmmo--;
        fireTimer = fireInterval;

        Debug.Log($"Ammo: {currentAmmo}/{maxAmmo}");
    }

    private void FireProjectile()
    {
        Vector3 fireDirection = GetScreenCenterFireDirection();

        Vector3 spawnPosition =
            transform.position
            + surfaceUp * projectileSpawnHeight
            + fireDirection * projectileSpawnForwardOffset;

        Quaternion spawnRotation = Quaternion.LookRotation(fireDirection, surfaceUp);

        GameObject projectileObject;

        if (projectilePrefab != null)
        {
            projectileObject = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
        }
        else
        {
            projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            projectileObject.transform.localScale = Vector3.one * projectileScale;

            Renderer renderer = projectileObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.yellow;
            }
        }

        Projectile projectile = projectileObject.GetComponent<Projectile>();

        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<Projectile>();
        }

        projectile.Initialize(
            fireDirection,
            projectileSpeed,
            projectileLifeTime,
            projectileDamage,
            playerHealth
        );
    }

    private Vector3 GetScreenCenterFireDirection()
    {
        if (aimCamera == null)
        {
            return aimForward;
        }

        Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));

        if (centerRay.direction.sqrMagnitude < 0.001f)
        {
            return aimForward;
        }

        return centerRay.direction.normalized;
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
            snapDirection = -surfaceUp;
        }
        else
        {
            snapDirection = surfaceUp;
        }

        controller.Move(snapDirection * snapAmount);
    }

    private void UpdateCameraTargetImmediate()
    {
        if (cameraTarget == null)
        {
            return;
        }

        Vector3 baseOffset =
            surfaceUp * cameraHeight
            - aimForward * cameraBackDistance
            + aimRight * cameraSideOffset;

        Vector3 pitchedOffset = Quaternion.AngleAxis(cameraPitch, aimRight) * baseOffset;

        Vector3 cameraPosition = transform.position + pitchedOffset;

        Vector3 lookPoint =
            transform.position
            + surfaceUp * cameraLookAtHeight
            + aimForward * cameraLookAheadDistance;

        Vector3 cameraForward = lookPoint - cameraPosition;

        if (cameraForward.sqrMagnitude < 0.001f)
        {
            cameraForward = aimForward;
        }

        cameraForward.Normalize();

        Quaternion cameraRotation = Quaternion.LookRotation(cameraForward, surfaceUp);

        cameraTarget.SetPositionAndRotation(cameraPosition, cameraRotation);
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

        if (cameraTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(cameraTarget.position, 0.2f);
        }
    }
}