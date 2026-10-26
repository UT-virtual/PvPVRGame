using Fusion;
using UnityEngine;

/// <summary>
/// サーバーの生成処理で割り当てられた表示番号と色インデックスを同期し、頭上アイコンなどに提供する。
/// </summary>
public class PlayerIdentity : NetworkBehaviour
{
    [Networked] public int PlayerNumber { get; private set; }
    [Networked] public int ColorIndex { get; private set; }

    /// <summary>
    /// StateAuthorityで表示番号と色インデックスを初期化する。番号は1以上、色は0以上へ補正する。
    /// </summary>
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