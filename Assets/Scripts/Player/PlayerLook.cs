using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
public class PlayerLook : MonoBehaviour
{
    [Header("Pitch")]
    [SerializeField] private float minPitch = -85.0f;
    [SerializeField] private float maxPitch = 85.0f;

    [Header("VR Look Settings")]
    //VRゴーグルの感度
    [SerializeField] private float hmdSensitivity = 1.0f;

    private PlayerMove playerMove;
    private float pitch;

    // VR用の現在の頭の回転を保持する変数
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
            // スティックの方向転換する
            playerMove.RotateYaw(lookInput.x);

            // ゴーグルの回転はHMDのデータをそのまま保持する
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
            //デバック用
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
            // PC用の既存処理
            Vector3 viewForward = Quaternion.AngleAxis(pitch, playerMove.AimRight) * playerMove.AimForward;
            if (viewForward.sqrMagnitude < 0.001f) return playerMove.AimForward;
            return viewForward.normalized;
        }
    }

    private Vector3 GetViewUp()
    {
        if (isVRMode && currentHMDRotation != Quaternion.identity)
        {
            // Rollのカメラの傾きも反映
            Quaternion bodyRotation = Quaternion.LookRotation(playerMove.AimForward, playerMove.SurfaceUp);
            Vector3 viewUp = (bodyRotation * currentHMDRotation) * Vector3.up;
            return viewUp.normalized;
        }

        // PC用の既存処理
        Vector3 viewForward = GetViewForward();
        Vector3 viewRight = Vector3.Cross(playerMove.SurfaceUp, viewForward);
        if (viewRight.sqrMagnitude < 0.001f) viewRight = playerMove.AimRight;
        viewRight.Normalize();
        Vector3 viewUpVec = Vector3.Cross(viewForward, viewRight);
        if (viewUpVec.sqrMagnitude < 0.001f) return playerMove.SurfaceUp;
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