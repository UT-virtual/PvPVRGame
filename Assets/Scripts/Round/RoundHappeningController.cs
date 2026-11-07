using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// StateAuthority側で一人一票を保持し、締切に最多得票を確定して予告・発動・終了へ進める。
/// 未確定の接続中観戦者は最後のフォーカスで自動確定する。確定票は退出後も保持する。
/// 同期と送信者認証はRoundManagerに委ね、全員の応答を待たず締切で進行する。
/// </summary>
public sealed class RoundHappeningController
{
    public const int CandidateCount = 3;
    private readonly Dictionary<PlayerRef, int> votes = new();
    private readonly Dictionary<PlayerRef, (int candidate, int revision)> focusedChoices = new();
    private readonly Func<int, int> chooseRandomIndex;
    public int VoteCount => votes.Count;
    public bool TryGetVote(PlayerRef voter, out int candidate) => votes.TryGetValue(voter, out candidate);

    public RoundHappeningController(Func<int, int> chooseRandomIndex)
    {
        this.chooseRandomIndex = chooseRandomIndex;
    }

    public RoundHappeningState Begin(NetworkRunner runner, RoundHappeningSettings settings)
    {
        Reset();
        float opens = Mathf.Max(0f, settings.VoteStartDelay);
        float closes = opens + Mathf.Max(0.1f, settings.VotingDuration);
        float starts = closes + Mathf.Max(0f, settings.PreviewDuration);
        float ends = starts + Mathf.Max(0.1f, settings.EffectDuration);
        return new RoundHappeningState
        {
            Phase = RoundHappeningPhase.Waiting,
            VoteOpensAt = TickTimer.CreateFromSeconds(runner, opens),
            VoteClosesAt = TickTimer.CreateFromSeconds(runner, closes),
            EffectStartsAt = TickTimer.CreateFromSeconds(runner, starts),
            EffectEndsAt = TickTimer.CreateFromSeconds(runner, ends),
            DamageMultiplier = Mathf.Max(0.1f, settings.DamageMultiplier),
            MoveSpeedMultiplier = Mathf.Max(0.1f, settings.MoveSpeedMultiplier)
        };
    }

    /// <summary>
    /// 本人の接続役割を検証した結果と同期期間を使って受理する。死亡後の観戦者や重複票は受理しない。
    /// </summary>
    public bool TryVote(PlayerRef voter, int candidate, bool isDedicatedSpectator,
        RoundHappeningState state, NetworkRunner runner)
    {
        if (!isDedicatedSpectator || voter == PlayerRef.None || candidate < 0 || candidate >= CandidateCount ||
            !state.IsVotingOpen(runner) || votes.ContainsKey(voter)) return false;
        votes.Add(voter, candidate);
        return true;
    }

    /// <summary>
    /// 未確定のフォーカスを保存する。変更番号で遅れて届いた古い選択を除き、確定済みの票は変更しない。
    /// </summary>
    public bool TrySetFocus(PlayerRef voter, int candidate, int revision, bool isDedicatedSpectator,
        RoundHappeningState state, NetworkRunner runner)
    {
        if (!isDedicatedSpectator || voter == PlayerRef.None || candidate < 0 || candidate >= CandidateCount ||
            revision <= 0 || !state.IsVotingOpen(runner) || votes.ContainsKey(voter)) return false;
        if (focusedChoices.TryGetValue(voter, out var previous) && revision <= previous.revision) return false;
        focusedChoices[voter] = (candidate, revision);
        return true;
    }

    public RoundHappeningState Advance(RoundHappeningState state, NetworkRunner runner,
        IReadOnlyDictionary<PlayerRef, ParticipantRole> participants = null)
    {
        if (state.Phase == RoundHappeningPhase.None || state.Phase == RoundHappeningPhase.Finished) return state;

        if (state.VoteClosesAt.Expired(runner) && state.Phase <= RoundHappeningPhase.Voting)
        {
            ConfirmFocusedChoices(participants);
            state.Effect = ResolveVote();
            state.Phase = state.Effect == RoundHappeningType.None
                ? RoundHappeningPhase.Finished : RoundHappeningPhase.Preview;
        }
        else if (state.Phase == RoundHappeningPhase.Waiting && state.VoteOpensAt.Expired(runner))
        {
            state.Phase = RoundHappeningPhase.Voting;
        }

        if (state.Phase == RoundHappeningPhase.Preview && state.EffectStartsAt.Expired(runner))
            state.Phase = RoundHappeningPhase.Active;
        if (state.Phase == RoundHappeningPhase.Active && state.EffectEndsAt.Expired(runner))
            state.Phase = RoundHappeningPhase.Finished;
        return state;
    }

    private void ConfirmFocusedChoices(IReadOnlyDictionary<PlayerRef, ParticipantRole> participants)
    {
        if (participants == null) return;
        foreach (var participant in participants)
        {
            if (participant.Key == PlayerRef.None || participant.Value != ParticipantRole.Spectator || votes.ContainsKey(participant.Key)) continue;
            // 未操作時の初期フォーカスはUIと同じ先頭候補。退出した未確定者は集計しない。
            int candidate = focusedChoices.TryGetValue(participant.Key, out var focus) ? focus.candidate : 0;
            votes.Add(participant.Key, candidate);
        }
    }

    private RoundHappeningType ResolveVote()
    {
        if (votes.Count == 0) return RoundHappeningType.None;
        int[] counts = new int[CandidateCount];
        foreach (int candidate in votes.Values) counts[candidate]++;
        int highest = Mathf.Max(counts[0], Mathf.Max(counts[1], counts[2]));
        List<int> tied = new(CandidateCount);
        for (int i = 0; i < CandidateCount; i++) if (counts[i] == highest) tied.Add(i);
        return GetCandidate(tied[chooseRandomIndex(tied.Count)]);
    }

    public void Reset()
    {
        votes.Clear();
        focusedChoices.Clear();
    }

    public static RoundHappeningType GetCandidate(int index) => index >= 0 && index < CandidateCount
        ? (RoundHappeningType)(index + 1) : RoundHappeningType.None;
}
