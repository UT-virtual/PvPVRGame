using Fusion;
using UnityEngine;

/// <summary>
/// PlayerNetworkInput.Buttonsで送る操作のビット位置を定義する。
/// 押し続ける射撃と、PlayerControllerが前Tickとの差分で判定する操作を含む。
/// </summary>
public enum PlayerInputButton
{
    Jump,
    Fire,
    Reload,
    Ready,
    Skill,
    SwitchSkill,
    SelectSkill1,
    SelectSkill2,
    SelectSkill3,
    SelectSkill4,
    
    ActivateSkill1,
    ActivateSkill2
}

/// <summary>
/// NetworkInputCollectorからPlayerControllerとVRBodyTargetSyncへ渡す1回分の入力データ。
/// 視線の方向はワールド座標、HMD・手の姿勢は入力Actionの追跡座標で送り、地表基準への変換は受信側で行う。
/// </summary>
public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 MoveInput;
    public Vector2 LookInput;
    public NetworkButtons Buttons;

    public Vector3 AimForward;
    public Vector3 ViewForward;
    // 未登録時のゼロ方向と区別し、受信側が視点差分を使う経路へ切り替える。
    public byte HasLookDirection;

    public NetworkBool IsVR;
    public Quaternion HMDRotation;

    public Vector3 HMDPosition;

    public Vector3 LeftHandPosition;
    public Quaternion LeftHandRotation;
    // 位置Actionの割り当て有無を表す。追跡位置そのものの有効性はVRBodyTargetSync側で確認する。
    public byte HasLeftHand;

    public Vector3 RightHandPosition;
    public Quaternion RightHandRotation;
    public byte HasRightHand;
}