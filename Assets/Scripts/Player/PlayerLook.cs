using UnityEngine;

/// <summary>
/// 地表に沿う胴体の向きと視線を組み立てる。
/// PCはヨーとピッチ、VRはスティック旋回とHMD回転を使い分ける。
/// </summary>
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

    /// <summary>
    /// PCでは地表基準のヨーと制限付きピッチを更新し、VRではヨー旋回とHMD回転を組み合わせる。
    /// lookInputは時間積算済みの角度差分として受け取る。
    /// </summary>
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
                // VR感度の適用：元の回転と無回転の間を補間/補外して感度を表現
                currentHMDRotation = Quaternion.SlerpUnclamped(Quaternion.identity, hmdRotation, hmdSensitivity);
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
        if (isVRMode && currentHMDRotation != Quaternion.identity)
        {
            // スティックとゴーグルを合体させカメラの向きを決定
            Quaternion bodyRotation = Quaternion.LookRotation(playerMove.AimForward, playerMove.SurfaceUp);
            Vector3 viewForward = (bodyRotation * currentHMDRotation) * Vector3.forward;
            return viewForward.normalized;
        }
        else
        {
            Vector3 viewForward = Quaternion.AngleAxis(pitch, playerMove.AimRight) * playerMove.AimForward;
            if (viewForward.sqrMagnitude < 0.001f)
            {
                return playerMove.AimForward;
            }
            return viewForward.normalized;
        }
    }

    private Vector3 GetViewUp()
    {
        if (isVRMode && currentHMDRotation != Quaternion.identity)
        {
            Quaternion bodyRotation = Quaternion.LookRotation(playerMove.AimForward, playerMove.SurfaceUp);
            Vector3 viewUp = (bodyRotation * currentHMDRotation) * Vector3.up;
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

    /// <summary>
    /// 受信したワールド視線と現在のAimForwardからPC用ピッチを復元する。
    /// 送信済みの視線方向を使う経路では視点差分の再積算を避ける。
    /// </summary>
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
