using System;
using Fusion;
using UnityEngine;

/// <summary>
/// 観戦者の投票で選べる一時効果。Noneは未確定または投票なしを表す。
/// ラウンド開始時のRoundEffectTypeとは別に管理する。
/// </summary>
public enum RoundHappeningType
{
    None,
    DoubleDamage,
    FasterMovement,
    NoJump
}

public enum RoundHappeningPhase
{
    None,
    Waiting,
    Voting,
    Preview,
    Active,
    Finished
}

/// <summary>
/// 投票と効果の時間・倍率を設定する。開始時にStateAuthorityが同期状態へ写し、端末間の設定差をなくす。
/// </summary>
[Serializable]
public sealed class RoundHappeningSettings
{
    [Min(0f)] public float VoteStartDelay = 15f;
    [Min(0.1f)] public float VotingDuration = 10f;
    [Min(0f)] public float PreviewDuration = 5f;
    [Min(0.1f)] public float EffectDuration = 15f;
    [Min(0.1f)] public float DamageMultiplier = 2f;
    [Min(0.1f)] public float MoveSpeedMultiplier = 1.5f;
}

/// <summary>
/// 投票期間、確定効果と適用期間を全端末へ届ける。時刻はラウンド開始Tickから設定する。
/// 受付・適用の境界をTickTimerで調べ、同一Tick内の処理順による遅延を避ける。
/// </summary>
public struct RoundHappeningState : INetworkStruct
{
    public RoundHappeningPhase Phase;
    public RoundHappeningType Effect;
    public TickTimer VoteOpensAt;
    public TickTimer VoteClosesAt;
    public TickTimer EffectStartsAt;
    public TickTimer EffectEndsAt;
    public float DamageMultiplier;
    public float MoveSpeedMultiplier;

    public bool IsVotingOpen(NetworkRunner runner) =>
        (Phase == RoundHappeningPhase.Waiting || Phase == RoundHappeningPhase.Voting) &&
        VoteOpensAt.Expired(runner) && !VoteClosesAt.ExpiredOrNotRunning(runner);

    public bool IsEffectActive(NetworkRunner runner) => Effect != RoundHappeningType.None &&
        (Phase == RoundHappeningPhase.Preview || Phase == RoundHappeningPhase.Active) &&
        EffectStartsAt.Expired(runner) && !EffectEndsAt.ExpiredOrNotRunning(runner);
}
