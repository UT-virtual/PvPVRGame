using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
public class PlayerLook : MonoBehaviour
{
    [Header("Pitch")]
    [SerializeField] private float minPitch = -85.0f;
    [SerializeField] private float maxPitch = 85.0f;

    [Header("VR Look Settings")]
    [SerializeField] private float hmdSensitivity = 1.0f;

    private PlayerMove playerMove;
    private float pitch;

    private Quaternion currentHMDRotation = Quaternion.identity;
    private bool isVRMode;

    public float Pitch => pitch;
    public Vector3 ViewForward => GetViewForward();
    public Vector3 ViewUp => GetViewUp();

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
    }

    public void ApplyLook(Vector2 lookInput, bool isVR, Quaternion hmdRotation)
    {
        isVRMode = isVR;

        if (isVR)
        {
            playerMove.RotateYaw(lookInput.x);

            if (Mathf.Approximately(hmdSensitivity, 1.0f))
            {
                currentHMDRotation = hmdRotation;
            }
            else
            {
                Quaternion bodyRotation = Quaternion.LookRotation(
                    playerMove.AimForward,
                    playerMove.SurfaceUp);

                currentHMDRotation = Quaternion.Slerp(bodyRotation, hmdRotation, hmdSensitivity);
            }
        }
        else
        {
            float yawAmount = lookInput.x;
            float pitchAmount = lookInput.y;

            playerMove.RotateYaw(yawAmount);

            if (Mathf.Abs(pitchAmount) > 0.001f)
            {
                pitch -= pitchAmount;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            currentHMDRotation = Quaternion.identity;
        }
    }

    private Vector3 GetViewForward()
    {
        if (isVRMode)
        {
            Vector3 viewForward = currentHMDRotation * Vector3.forward;
            if (viewForward.sqrMagnitude < 0.001f)
            {
                return playerMove.AimForward;
            }

            return viewForward.normalized;
        }

        Vector3 pcViewForward = Quaternion.AngleAxis(pitch, playerMove.AimRight) * playerMove.AimForward;
        if (pcViewForward.sqrMagnitude < 0.001f)
        {
            return playerMove.AimForward;
        }

        return pcViewForward.normalized;
    }

    private Vector3 GetViewUp()
    {
        if (isVRMode)
        {
            Vector3 viewUp = currentHMDRotation * Vector3.up;
            if (viewUp.sqrMagnitude < 0.001f)
            {
                return playerMove.SurfaceUp;
            }

            return viewUp.normalized;
        }

        Vector3 viewForward = GetViewForward();
        Vector3 viewRight = Vector3.Cross(playerMove.SurfaceUp, viewForward);
        if (viewRight.sqrMagnitude < 0.001f)
        {
            viewRight = playerMove.AimRight;
        }

        viewRight.Normalize();

        Vector3 viewUpVec = Vector3.Cross(viewForward, viewRight);
        if (viewUpVec.sqrMagnitude < 0.001f)
        {
            return playerMove.SurfaceUp;
        }

        return viewUpVec.normalized;
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
