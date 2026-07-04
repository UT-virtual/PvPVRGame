using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 5.0f;
    private float moveSpeedMultiplier = 1.0f;

    [Header("Gravity / Jump")]
    [SerializeField] private float gravity = 25.0f;
    [SerializeField] private float jumpSpeed = 8.0f;
    private float fallGravityMultiplier = 1.0f;

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

    private CharacterController controller;

    private Vector3 surfaceUp = Vector3.up;
    private Vector3 aimForward = Vector3.forward;
    private Vector3 aimRight = Vector3.right;

    private float verticalSpeed;

    private bool hasGroundHit;
    private bool isGrounded;
    private float groundDistance;
    private RaycastHit groundHit;

    public Vector3 SurfaceUp => surfaceUp;
    public Vector3 AimForward => aimForward;
    public Vector3 AimRight => aimRight;
    public bool IsGrounded => isGrounded;
    private int extraAirJumpCount;
    private int remainingAirJumps;

    public event Action OnJumped;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        InitializeSurfaceVectors();
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

        if (surfaceUp.sqrMagnitude < 0.001f)
        {
            surfaceUp = Vector3.up;
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

        if (aimForward.sqrMagnitude < 0.001f)
        {
            aimForward = Vector3.Cross(Vector3.up, surfaceUp);
        }

        aimForward.Normalize();

        aimRight = Vector3.Cross(surfaceUp, aimForward).normalized;
        aimForward = Vector3.Cross(aimRight, surfaceUp).normalized;
    }

    public void ProbeGround()
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

    public void UpdateAimBasis()
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

    public void RotateYaw(float yawAmount)
    {
        if (Mathf.Abs(yawAmount) <= 0.001f)
        {
            return;
        }

        Quaternion yawRotation = Quaternion.AngleAxis(yawAmount, surfaceUp);

        aimForward = yawRotation * aimForward;
        aimForward = Vector3.ProjectOnPlane(aimForward, surfaceUp).normalized;

        aimRight = Vector3.Cross(surfaceUp, aimForward).normalized;
        aimForward = Vector3.Cross(aimRight, surfaceUp).normalized;
    }

    public void MoveOnSurface(Vector2 input, float deltaTime)
    {
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

        controller.Move(moveDir * moveSpeed * moveSpeedMultiplier * deltaTime);
    }

    //VRÇ≈HMDÇÃå¸Ç´Ç…êiÇﬁèàóù
    public void MoveOnSurfaceVR(Vector2 input, float deltaTime, Vector3 moveForward,Vector3 moveRight)
    {
        if (input.sqrMagnitude < 0.01f)
        {
            return;
        }

        if (input.sqrMagnitude > 1.0f)
        {
            input.Normalize();
        }

        moveForward = Vector3.ProjectOnPlane(moveForward, surfaceUp);

        if (moveForward.sqrMagnitude < 0.001f)
        {
            moveForward = aimForward;
        }

        moveForward.Normalize();

        moveRight = Vector3.Cross(surfaceUp, moveForward).normalized;
        moveForward = Vector3.Cross(moveRight, surfaceUp).normalized;

        Vector3 moveDir = moveForward * input.y + moveRight * input.x;

        if (moveDir.sqrMagnitude < 0.001f)
        {
            return;
        }

        if (moveDir.sqrMagnitude > 1.0f)
        {
            moveDir.Normalize();
        }

        controller.Move(moveDir * moveSpeed * moveSpeedMultiplier * deltaTime);
    }

    public void AlignToSurface(float deltaTime)
    {
        Quaternion targetRotation = Quaternion.LookRotation(aimForward, surfaceUp);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            alignSpeed * deltaTime
        );
    }

    public void ApplyGravityAndJump(bool jumpPressed, float deltaTime)
    {
        if (isGrounded)
        {
            remainingAirJumps = extraAirJumpCount;
        }

        if (isGrounded && jumpPressed)
        {
            verticalSpeed = jumpSpeed;
            isGrounded = false;
            remainingAirJumps = extraAirJumpCount;

            OnJumped?.Invoke();
        }
        else if (!isGrounded && jumpPressed && remainingAirJumps > 0)
        {
            verticalSpeed = jumpSpeed;
            remainingAirJumps--;

            OnJumped?.Invoke();
        }
        else if (isGrounded)
        {
            verticalSpeed = 0.0f;

            SnapToGround(deltaTime);
            return;
        }
        else
        {
            float currentGravityMultiplier = verticalSpeed < 0.0f
                ? fallGravityMultiplier
                : 1.0f;

            verticalSpeed -= gravity * currentGravityMultiplier * deltaTime;
        }

        Vector3 verticalMove = surfaceUp * verticalSpeed;
        controller.Move(verticalMove * deltaTime);
    }

    private void SnapToGround(float deltaTime)
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
            groundSnapSpeed * deltaTime
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

    public void SetAimForward(Vector3 worldForward)
    {
        Vector3 projectedForward = Vector3.ProjectOnPlane(worldForward, surfaceUp);

        if (projectedForward.sqrMagnitude < 0.001f)
        {
            return;
        }

        aimForward = projectedForward.normalized;
        aimRight = Vector3.Cross(surfaceUp, aimForward).normalized;
        aimForward = Vector3.Cross(aimRight, surfaceUp).normalized;
    }

    public void ResetMovementState()
    {
        verticalSpeed = 0.0f;
        hasGroundHit = false;
        isGrounded = false;
        groundDistance = 0.0f;
        remainingAirJumps = extraAirJumpCount;
    }

    public void ResetAfterRespawn()
    {
        verticalSpeed = 0.0f;
        hasGroundHit = false;
        isGrounded = false;
        groundDistance = 0.0f;
        remainingAirJumps = extraAirJumpCount;

        InitializeSurfaceVectors();
        ProbeGround();
        UpdateAimBasis();
        SnapRotationToSurface();

        Debug.Log(
            $"{gameObject.name} ResetAfterRespawn. " +
            $"Position={transform.position}, " +
            $"SurfaceUp={surfaceUp}, " +
            $"AimForward={aimForward}"
        );
    }

    private void SnapRotationToSurface()
    {
        if (aimForward.sqrMagnitude < 0.001f)
        {
            return;
        }

        if (surfaceUp.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(aimForward, surfaceUp);
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

    public void SetExtraAirJumpCount(int count)
    {
        count = Mathf.Max(0, count);

        if (count > extraAirJumpCount)
        {
            remainingAirJumps = Mathf.Max(remainingAirJumps, count);
        }
        else if (count < extraAirJumpCount)
        {
            remainingAirJumps = Mathf.Min(remainingAirJumps, count);
        }

        extraAirJumpCount = count;

        if (isGrounded)
        {
            remainingAirJumps = extraAirJumpCount;
        }
    }

    public void SetMoveSpeedMultiplier(float multiplier)
    {
        moveSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 10.0f);
    }

    public void SetFallGravityMultiplier(float multiplier)
    {
        fallGravityMultiplier = Mathf.Clamp(multiplier, 0.05f, 5.0f);
    }
}