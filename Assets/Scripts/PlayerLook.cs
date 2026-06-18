using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
public class PlayerLook : MonoBehaviour
{
    [Header("Pitch")]
    [SerializeField] private float minPitch = -85.0f;
    [SerializeField] private float maxPitch = 85.0f;

    private PlayerMove motor;
    private float pitch;

    public float Pitch => pitch;
    public Vector3 ViewForward => GetViewForward();
    public Vector3 ViewUp => GetViewUp();

    private void Awake()
    {
        motor = GetComponent<PlayerMove>();
    }

    public void ApplyLook(Vector2 lookInput)
    {
        float yawAmount = lookInput.x;
        float pitchAmount = lookInput.y;

        motor.RotateYaw(yawAmount);

        if (Mathf.Abs(pitchAmount) > 0.001f)
        {
            pitch -= pitchAmount;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    private Vector3 GetViewForward()
    {
        Vector3 viewForward = Quaternion.AngleAxis(pitch, motor.AimRight) * motor.AimForward;

        if (viewForward.sqrMagnitude < 0.001f)
        {
            return motor.AimForward;
        }

        return viewForward.normalized;
    }

    private Vector3 GetViewUp()
    {
        Vector3 viewForward = GetViewForward();
        Vector3 viewRight = Vector3.Cross(motor.SurfaceUp, viewForward);

        if (viewRight.sqrMagnitude < 0.001f)
        {
            viewRight = motor.AimRight;
        }

        viewRight.Normalize();

        Vector3 viewUp = Vector3.Cross(viewForward, viewRight);

        if (viewUp.sqrMagnitude < 0.001f)
        {
            return motor.SurfaceUp;
        }

        return viewUp.normalized;
    }
}