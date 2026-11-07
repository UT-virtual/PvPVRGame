using Fusion;

/// <summary>
/// ラウンド単位の撃破数、実効与ダメージ、生存状態を全端末へ送るスナップショット。
/// 死亡・退出後も参加時の色と記録を残し、終了時には生死とHPも結果時点の値に固定する。
/// </summary>
public struct RoundDamageEntry : INetworkStruct
{
    public PlayerRef Player;
    public int TeamIndex;
    public int Kills;
    public float Damage;
    public float Health;
    public float MaxHealth;
    public NetworkBool IsAlive;
}
