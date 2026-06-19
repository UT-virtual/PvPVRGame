using Fusion;
using UnityEngine;

public enum PlayerInputButton
{
    Jump,
    Fire,
    Reload
}

public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 MoveInput;
    public Vector2 LookInput;
    public NetworkButtons Buttons;

    public Vector3 AimForward;
    public Vector3 ViewForward;
    public byte HasLookDirection;
}