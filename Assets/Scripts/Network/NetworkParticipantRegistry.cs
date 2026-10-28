using Fusion;
using System.Collections.Generic;

/// <summary>
/// 接続者の役割を表す。UnselectedとSpectatorはキャラクターを持たず、ラウンド参加人数に含めない。
/// </summary>
public enum ParticipantRole : byte
{
    Unselected,
    Combatant,
    Spectator
}

/// <summary>
/// サーバー側で接続者の役割と役割要求の結果を保持し、待機フェーズと対戦人数上限を検証する。
/// キャラクターの生成・破棄はNetworkPlayerSpawnerへ委譲し、クライアントの表示状態は扱わない。
/// </summary>
public sealed class NetworkParticipantRegistry
{
    private readonly NetworkPlayerSpawner spawner;
    private readonly Dictionary<PlayerRef, ParticipantRole> roles = new();
    private readonly Dictionary<PlayerRef, (int id, bool accepted, string message)> roleRequests = new();

    public IReadOnlyDictionary<PlayerRef, ParticipantRole> Roles => roles;
    public int Revision { get; private set; }

    public ParticipantRole GetRole(PlayerRef player) =>
        roles.TryGetValue(player, out ParticipantRole role) ? role : ParticipantRole.Unselected;

    public NetworkParticipantRegistry(NetworkPlayerSpawner spawner)
    {
        this.spawner = spawner;
    }

    /// <summary>
    /// 接続直後は役割未選択として登録する。観戦端末にもPlayerRefはあるがキャラクターは生成しない。
    /// </summary>
    public void RegisterConnection(NetworkRunner runner, PlayerRef player)
    {
        if (runner != null && runner.IsServer && !roles.ContainsKey(player))
        {
            roles.Add(player, ParticipantRole.Unselected);
            Revision++;
        }
    }

    /// <summary>
    /// 検証済み送信者の役割要求を処理する。同一要求の再送では生成・破棄を繰り返さない。
    /// 初回は対戦枠があれば対戦者に割り当て、満員・試合中なら観戦者にする。手動の役割変更は待機中に限る。
    /// </summary>
    public bool TryChangeRole(NetworkRunner runner, PlayerRef player, ParticipantRole requestedRole,
        int requestId, bool isWaiting, out string message, bool initialAssignment = false)
    {
        message = "役割を変更できませんでした。";
        if (runner == null || !runner.IsServer || !roles.ContainsKey(player) || requestId <= 0)
        {
            return false;
        }

        if (roleRequests.TryGetValue(player, out var previous) && requestId <= previous.id)
        {
            message = requestId == previous.id ? previous.message : "古い役割要求です。";
            return requestId == previous.id && previous.accepted;
        }

        // 初回の自動割当だけは、試合中・対戦枠満員ならキャラクターを生成せず観戦者にする。
        // 応答が届かず初回要求を出し直した場合も、確定済みの役割を変更しない。
        if (initialAssignment)
        {
            if (GetRole(player) != ParticipantRole.Unselected)
            {
                roleRequests[player] = (requestId, true, "");
                message = "";
                return true;
            }
            requestedRole = isWaiting && spawner.CombatantCount < NetworkSessionController.MaxCombatantCount
                ? ParticipantRole.Combatant : ParticipantRole.Spectator;
        }

        bool accepted = ApplyRole(runner, player, requestedRole, isWaiting, out message);
        roleRequests[player] = (requestId, accepted, message);
        return accepted;
    }

    private bool ApplyRole(NetworkRunner runner, PlayerRef player, ParticipantRole requestedRole,
        bool isWaiting, out string message)
    {
        message = "対戦者または観戦者を選択してください。";
        if (requestedRole != ParticipantRole.Combatant && requestedRole != ParticipantRole.Spectator)
        {
            return false;
        }

        ParticipantRole currentRole = GetRole(player);
        if (currentRole == requestedRole)
        {
            message = "";
            return true;
        }

        if (!isWaiting && !(currentRole == ParticipantRole.Unselected && requestedRole == ParticipantRole.Spectator))
        {
            message = "役割を変更できるのは待機中だけです。";
            return false;
        }

        if (requestedRole == ParticipantRole.Combatant)
        {
            if (spawner.CombatantCount >= NetworkSessionController.MaxCombatantCount)
            {
                message = "対戦者は4人までです。観戦者として参加できます。";
                return false;
            }

            if (!spawner.TrySpawnCombatant(runner, player))
            {
                message = "プレイヤーを生成できませんでした。もう一度選択してください。";
                return false;
            }
        }
        else
        {
            spawner.DespawnCombatant(runner, player);
        }

        roles[player] = requestedRole;
        Revision++;
        message = "";
        return true;
    }

    public void UnregisterConnection(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        if (roles.Remove(player))
        {
            Revision++;
        }
        roleRequests.Remove(player);
        spawner.DespawnCombatant(runner, player);
    }

    public void ResetSession()
    {
        roles.Clear();
        Revision++;
        roleRequests.Clear();
    }
}
