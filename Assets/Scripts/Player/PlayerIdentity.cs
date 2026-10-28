using Fusion;
using UnityEngine;

public class PlayerIdentity : NetworkBehaviour
{
    [Networked] public int PlayerNumber { get; private set; }
    [Networked] public int ColorIndex { get; private set; }

    public void SetIdentity(int playerNumber, int colorIndex)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        PlayerNumber = Mathf.Max(1, playerNumber);
        ColorIndex = Mathf.Max(0, colorIndex);

        Debug.Log(
            $"[PlayerIdentity] {gameObject.name}: " +
            $"PlayerNumber={PlayerNumber}, ColorIndex={ColorIndex}"
        );
    }
}