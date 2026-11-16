using Fusion;
using UnityEngine;

/// <summary>
/// 入力内のHMD・両手姿勢を地表基準のワールド座標へ変換し、頭と手のターゲット姿勢を同期する。
/// StateAuthorityがTickで算出し、各端末はRender／LateUpdateでターゲットへ適用する。
/// </summary>
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerCamera))]
public class VRBodyTargetSync : NetworkBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform headTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;

    [Header("Fallback Local Offsets")]
    [SerializeField] private Vector3 defaultLeftHandOffset = new Vector3(-0.35f, -0.35f, 0.45f);
    [SerializeField] private Vector3 defaultRightHandOffset = new Vector3(0.35f, -0.35f, 0.45f);

    [Header("Fallback")]
    [SerializeField] private bool useInitialTargetOffsetsAsFallback = true;

    private PlayerMove playerMove;
    private PlayerCamera playerCamera;

    [Networked] private NetworkBool NetworkedTargetsInitialized { get; set; }

    [Networked] private Vector3 NetworkedHeadPosition { get; set; }
    [Networked] private Quaternion NetworkedHeadRotation { get; set; }

    [Networked] private Vector3 NetworkedLeftHandPosition { get; set; }
    [Networked] private Quaternion NetworkedLeftHandRotation { get; set; }

    [Networked] private Vector3 NetworkedRightHandPosition { get; set; }
    [Networked] private Quaternion NetworkedRightHandRotation { get; set; }

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerCamera = GetComponent<PlayerCamera>();

        // 手動で配置した HeadTarget / HandTarget の差分をフォールバック位置として使う
        if (useInitialTargetOffsetsAsFallback &&
            headTarget != null &&
            leftHandTarget != null &&
            rightHandTarget != null)
        {
            defaultLeftHandOffset =
                transform.InverseTransformDirection(leftHandTarget.position - headTarget.position);

            defaultRightHandOffset =
                transform.InverseTransformDirection(rightHandTarget.position - headTarget.position);
        }
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

    /// <summary>
    /// HMDから手までの相対位置を地表基準へ回転し、固定のカメラ位置を頭の基点としてワールド姿勢を作る。
    /// 追跡位置がゼロまたは手のAction未設定なら、Prefab配置由来の手オフセットを代用する。
    /// </summary>
    private void UpdateNetworkedTargets(PlayerNetworkInput input)
    {
        if (playerMove == null || playerCamera == null)
        {
            return;
        }

        playerMove.ProbeGround();
        playerMove.UpdateAimBasis();

        Quaternion bodyRotation = Quaternion.LookRotation(
            playerMove.AimForward,
            playerMove.SurfaceUp
        );

        Vector3 headWorldPosition = playerCamera.CameraPosition;
        Quaternion headWorldRotation = bodyRotation * SafeRotation(input.HMDRotation);

        NetworkedHeadPosition = headWorldPosition;
        NetworkedHeadRotation = NormalizeQuaternion(headWorldRotation);

        bool hasHmdPosition = IsValidPosition(input.HMDPosition);

        bool hasLeftHandPosition =
            input.HasLeftHand != 0 &&
            IsValidPosition(input.LeftHandPosition);

        bool hasRightHandPosition =
            input.HasRightHand != 0 &&
            IsValidPosition(input.RightHandPosition);

        Vector3 hmdLocalPosition = hasHmdPosition
            ? input.HMDPosition
            : Vector3.zero;

        // 追跡空間の原点やHMDの高さを二重加算しないよう、手はHMDからの相対位置として扱う。
        Vector3 leftLocalOffset =
            hasHmdPosition && hasLeftHandPosition
                ? input.LeftHandPosition - hmdLocalPosition
                : defaultLeftHandOffset;

        Vector3 rightLocalOffset =
            hasHmdPosition && hasRightHandPosition
                ? input.RightHandPosition - hmdLocalPosition
                : defaultRightHandOffset;

        NetworkedLeftHandPosition =
            headWorldPosition + bodyRotation * leftLocalOffset;

        NetworkedRightHandPosition =
            headWorldPosition + bodyRotation * rightLocalOffset;

        Quaternion leftRotation =
            hasLeftHandPosition && IsValidRotation(input.LeftHandRotation)
                ? bodyRotation * input.LeftHandRotation
                : bodyRotation;

        Quaternion rightRotation =
            hasRightHandPosition && IsValidRotation(input.RightHandRotation)
                ? bodyRotation * input.RightHandRotation
                : bodyRotation;

        NetworkedLeftHandRotation = NormalizeQuaternion(leftRotation);
        NetworkedRightHandRotation = NormalizeQuaternion(rightRotation);

        NetworkedTargetsInitialized = true;
    }

    public override void Render()
    {
        ApplyTargets();
    }

    private void LateUpdate()
    {
        ApplyTargets();
    }

    private void ApplyTargets()
    {
        if (Object != null && !NetworkedTargetsInitialized)
        {
            return;
        }

        if (headTarget != null)
        {
            headTarget.SetPositionAndRotation(
                NetworkedHeadPosition,
                SafeRotation(NetworkedHeadRotation)
            );
        }

        if (leftHandTarget != null)
        {
            leftHandTarget.SetPositionAndRotation(
                NetworkedLeftHandPosition,
                SafeRotation(NetworkedLeftHandRotation)
            );
        }

        if (rightHandTarget != null)
        {
            rightHandTarget.SetPositionAndRotation(
                NetworkedRightHandPosition,
                SafeRotation(NetworkedRightHandRotation)
            );
        }
    }

    private bool IsValidPosition(Vector3 position)
    {
        return position.sqrMagnitude > 0.000001f;
    }

    private bool IsValidRotation(Quaternion rotation)
    {
        if (rotation.x == 0.0f &&
            rotation.y == 0.0f &&
            rotation.z == 0.0f &&
            rotation.w == 0.0f)
        {
            return false;
        }

        if (Mathf.Abs(rotation.x) < 0.0001f &&
            Mathf.Abs(rotation.y) < 0.0001f &&
            Mathf.Abs(rotation.z) < 0.0001f &&
            Mathf.Abs(rotation.w - 1.0f) < 0.0001f)
        {
            return false;
        }

        return true;
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
            rotation.w * rotation.w
        );

        if (magnitude <= 0.0001f)
        {
            return Quaternion.identity;
        }

        return new Quaternion(
            rotation.x / magnitude,
            rotation.y / magnitude,
            rotation.z / magnitude,
            rotation.w / magnitude
        );
    }
}