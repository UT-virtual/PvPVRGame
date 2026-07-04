using UnityEngine;

[DefaultExecutionOrder(10000)]
public class VRWorldSpaceUIFollower : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;

    [Header("Placement")]
    [SerializeField] private float distance = 2.0f;
    [SerializeField] private float verticalOffset = -0.2f;

    [Header("Follow")]
    [SerializeField] private float directionFollowSpeed = 6.0f;
    [SerializeField] private bool followPitch = true;
    [SerializeField] private bool rotate180Y = false;

    private Vector3 smoothedForward;
    private bool initialized;

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera == null)
            {
                return;
            }

            targetCamera = mainCamera.transform;
        }

        Vector3 targetForward = targetCamera.forward;

        if (!followPitch)
        {
            targetForward.y = 0.0f;
        }

        if (targetForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        targetForward.Normalize();

        if (!initialized)
        {
            smoothedForward = targetForward;
            initialized = true;
        }

        float t = 1.0f - Mathf.Exp(-directionFollowSpeed * Time.deltaTime);

        smoothedForward = Vector3.Slerp(
            smoothedForward,
            targetForward,
            t
        );

        smoothedForward.Normalize();

        Vector3 targetPosition =
            targetCamera.position +
            smoothedForward * distance +
            targetCamera.up * verticalOffset;

        transform.position = targetPosition;

        Quaternion targetRotation =
            Quaternion.LookRotation(smoothedForward, targetCamera.up);

        if (rotate180Y)
        {
            targetRotation *= Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }

        transform.rotation = targetRotation;
    }
}