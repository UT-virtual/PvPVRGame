using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
public class PlayerLook : MonoBehaviour
{
    [Header("Pitch")]
    [SerializeField] private float minPitch = -85.0f;
    [SerializeField] private float maxPitch = 85.0f;

    private PlayerMove playerMove;
    private float pitch;

    public float Pitch => pitch;
    public Vector3 ViewForward => GetViewForward();
    public Vector3 ViewUp => GetViewUp();

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
    }

    public void ApplyLook(Vector2 lookInput)
    {
        float yawAmount = lookInput.x;
        float pitchAmount = lookInput.y;

        playerMove.RotateYaw(yawAmount);

        if (Mathf.Abs(pitchAmount) > 0.001f)
        {
            pitch -= pitchAmount;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    private Vector3 GetViewForward()
    {
        Vector3 viewForward = Quaternion.AngleAxis(pitch, playerMove.AimRight) * playerMove.AimForward;

        if (viewForward.sqrMagnitude < 0.001f)
        {
            return playerMove.AimForward;
        }

        return viewForward.normalized;
    }

    private Vector3 GetViewUp()
    {
        Vector3 viewForward = GetViewForward();
        Vector3 viewRight = Vector3.Cross(playerMove.SurfaceUp, viewForward);

        if (viewRight.sqrMagnitude < 0.001f)
        {
            viewRight = playerMove.AimRight;
        }

        viewRight.Normalize();

        Vector3 viewUp = Vector3.Cross(viewForward, viewRight);

        if (viewUp.sqrMagnitude < 0.001f)
        {
            return playerMove.SurfaceUp;
        }

        return viewUp.normalized;
    }

    public void SetPitchFromViewForward(Vector3 viewForward)
{
    if (viewForward.sqrMagnitude < 0.001f)
    {
        return;
    }

    Vector3 normalizedViewForward = viewForward.normalized;
    float newPitch = Vector3.SignedAngle(
        playerMove.AimForward,
        normalizedViewForward,
        playerMove.AimRight
    );

    pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
}
}