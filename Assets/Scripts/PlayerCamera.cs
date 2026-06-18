using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerLook))]
public class PlayerCamera : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float cameraEyeHeight = 1.6f;
    [SerializeField] private float cameraForwardOffset = 0.0f;
    [SerializeField] private float cameraSideOffset = 0.0f;

    private PlayerMove motor;
    private PlayerLook playerLook;

    public Vector3 CameraPosition => GetCameraPosition();

    private void Awake()
    {
        motor = GetComponent<PlayerMove>();
        playerLook = GetComponent<PlayerLook>();
    }

    public void UpdateCameraTarget()
    {
        if (cameraTarget == null)
        {
            return;
        }

        Vector3 cameraPosition = GetCameraPosition();
        Vector3 cameraForward = playerLook.ViewForward;
        Vector3 cameraUp = playerLook.ViewUp;

        Quaternion cameraRotation = Quaternion.LookRotation(cameraForward, cameraUp);

        cameraTarget.SetPositionAndRotation(cameraPosition, cameraRotation);
    }

    private Vector3 GetCameraPosition()
    {
        return transform.position
            + motor.SurfaceUp * cameraEyeHeight
            + motor.AimForward * cameraForwardOffset
            + motor.AimRight * cameraSideOffset;
    }

    private void OnDrawGizmosSelected()
    {
        if (cameraTarget == null)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(cameraTarget.position, 0.2f);
    }
}