using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class VRBodyTargetSync : NetworkBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform headTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;

    private Transform xrHmd;
    private Transform xrLeftController;
    private Transform xrRightController;
    private VRWeaponVisualSettings weaponVisualSettings;

    [Networked] private NetworkBool NetworkedTargetsInitialized { get; set; }

    [Networked] private Vector3 NetworkedHeadPosition { get; set; }
    [Networked] private Quaternion NetworkedHeadRotation { get; set; }

    [Networked] private Vector3 NetworkedLeftHandPosition { get; set; }
    [Networked] private Quaternion NetworkedLeftHandRotation { get; set; }

    [Networked] private Vector3 NetworkedRightHandPosition { get; set; }
    [Networked] private Quaternion NetworkedRightHandRotation { get; set; }

    private void Awake()
    {
        weaponVisualSettings = GetComponent<VRWeaponVisualSettings>();
    }

    public void SetXrTransforms(
        Transform hmd,
        Transform leftController,
        Transform rightController)
    {
        xrHmd = hmd;
        xrLeftController = leftController;
        xrRightController = rightController;
    }

    public override void Spawned()
    {
        AttachWeaponToRightHandTarget();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (!GetInput(out PlayerNetworkInput input))
        {
            return;
        }

        UpdateNetworkedTargets(input);
    }

    private void UpdateNetworkedTargets(PlayerNetworkInput input)
    {
        if (IsValidPosition(input.HMDPosition))
        {
            NetworkedHeadPosition = input.HMDPosition;
            NetworkedHeadRotation = NormalizeQuaternion(input.HMDRotation);
        }

        if (input.HasLeftHand != 0 && IsValidPosition(input.LeftHandPosition))
        {
            NetworkedLeftHandPosition = input.LeftHandPosition;
            NetworkedLeftHandRotation = NormalizeQuaternion(input.LeftHandRotation);
        }

        if (input.HasRightHand != 0 && IsValidPosition(input.RightHandPosition))
        {
            NetworkedRightHandPosition = input.RightHandPosition;
            NetworkedRightHandRotation = NormalizeQuaternion(input.RightHandRotation);
        }

        NetworkedTargetsInitialized = true;
    }

    private void LateUpdate()
    {
        if (Object != null && Object.HasInputAuthority)
        {
            ApplyFromXrTransforms();
        }
    }

    public override void Render()
    {
        if (Object != null && Object.HasInputAuthority)
        {
            return;
        }

        ApplyFromNetworked();
    }

    private void ApplyFromXrTransforms()
    {
        if (xrHmd != null && headTarget != null)
        {
            headTarget.SetPositionAndRotation(xrHmd.position, xrHmd.rotation);
        }

        if (xrLeftController != null && leftHandTarget != null)
        {
            leftHandTarget.SetPositionAndRotation(
                xrLeftController.position,
                xrLeftController.rotation);
        }

        if (xrRightController != null && rightHandTarget != null)
        {
            rightHandTarget.SetPositionAndRotation(
                xrRightController.position,
                xrRightController.rotation);
        }
    }

    private void ApplyFromNetworked()
    {
        if (!NetworkedTargetsInitialized)
        {
            return;
        }

        if (headTarget != null)
        {
            headTarget.SetPositionAndRotation(
                NetworkedHeadPosition,
                SafeRotation(NetworkedHeadRotation));
        }

        if (leftHandTarget != null)
        {
            leftHandTarget.SetPositionAndRotation(
                NetworkedLeftHandPosition,
                SafeRotation(NetworkedLeftHandRotation));
        }

        if (rightHandTarget != null)
        {
            rightHandTarget.SetPositionAndRotation(
                NetworkedRightHandPosition,
                SafeRotation(NetworkedRightHandRotation));
        }
    }

    private void AttachWeaponToRightHandTarget()
    {
        if (weaponVisualSettings == null || rightHandTarget == null)
        {
            return;
        }

        Transform weaponTransform = weaponVisualSettings.WeaponTransform;
        if (weaponTransform == null)
        {
            return;
        }

        Transform gripParent = weaponVisualSettings.WeaponGripParent != null
            ? weaponVisualSettings.WeaponGripParent
            : rightHandTarget;

        weaponTransform.SetParent(gripParent, false);
        weaponTransform.localPosition = weaponVisualSettings.WeaponLocalPosition;
        weaponTransform.localRotation = Quaternion.Euler(weaponVisualSettings.WeaponLocalEulerAngles);
        weaponTransform.localScale = Vector3.one * weaponVisualSettings.WeaponScale;
    }

    private bool IsValidPosition(Vector3 position)
    {
        return position.sqrMagnitude > 0.000001f;
    }

    private Quaternion SafeRotation(Quaternion rotation)
    {
        if (rotation.x == 0.0f &&
            rotation.y == 0.0f &&
            rotation.z == 0.0f &&
            rotation.w == 0.0f)
        {
            return Quaternion.identity;
        }

        return NormalizeQuaternion(rotation);
    }

    private Quaternion NormalizeQuaternion(Quaternion rotation)
    {
        float magnitude = Mathf.Sqrt(
            rotation.x * rotation.x +
            rotation.y * rotation.y +
            rotation.z * rotation.z +
            rotation.w * rotation.w);

        if (magnitude <= 0.0001f)
        {
            return Quaternion.identity;
        }

        return new Quaternion(
            rotation.x / magnitude,
            rotation.y / magnitude,
            rotation.z / magnitude,
            rotation.w / magnitude);
    }
}
