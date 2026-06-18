using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 5.0f;

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

    public void MoveOnSurface(Vector2 input)
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

        controller.Move(moveDir * moveSpeed * Time.deltaTime);
    }

    public void AlignToSurface()
    {
        Quaternion targetRotation = Quaternion.LookRotation(aimForward, surfaceUp);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            alignSpeed * Time.deltaTime
        );
    }

    public void ApplyGravityAndJump(bool jumpPressed)
    {
        if (isGrounded && jumpPressed)
        {
            verticalSpeed = jumpSpeed;
            isGrounded = false;

            OnJumped?.Invoke();
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