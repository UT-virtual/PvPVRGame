using Fusion;
using UnityEngine;

public enum PlayerInputButton
{
    Jump,
    Fire,
    Reload,
    Ready,
    Skill,
    SelectSkill1,
    SelectSkill2,
    SelectSkill3,
    SelectSkill4
}

public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 MoveInput;
    public Vector2 LookInput;
    public NetworkButtons Buttons;

    public Vector3 AimForward;
    public Vector3 ViewForward;
    public byte HasLookDirection;

    public NetworkBool IsVR;
    public Quaternion HMDRotation;
}