using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 専用観戦者には投票候補、全端末には予告と効果の残り時間を表示する。
/// 同期された期間を表示するだけで、投票の受理や効果の適用は行わない。
/// </summary>
public sealed class RoundHappeningUI : MonoBehaviour
{
    private RoundManager manager;
    private SpectatorHappeningInputController input;
    private GameObject voteRoot;
    private GameObject noticeRoot;
    private GameObject confirmationRoot;
    private TMP_Text confirmationText;
    private TMP_Text voteTime;
    private TMP_Text voteStatus;
    private TMP_Text noticeTitle;
    private TMP_Text noticeDetail;
    private Image noticeBackground;
    private readonly TMP_Text[] candidates = new TMP_Text[RoundHappeningController.CandidateCount];
    private readonly Image[] candidateBackgrounds = new Image[RoundHappeningController.CandidateCount];
    private int version = -1;
    private RoundHappeningPhase lastPhase;
    private int lastSeconds = -1;
    private int lastSelection = -1;
    private int lastVoteCount = -1;
    private int lastInputStatus = -1;
    private float transitionAt;

    public void Initialize(RoundManager roundManager, TMP_FontAsset font)
    {
        manager = roundManager;
        input = manager.GetComponent<SpectatorHappeningInputController>();
        RectTransform voting = RoundDamageRankingUI.CreateRect("HappeningVote", transform,
            new Vector2(0f, 1f), new Vector2(16f, -64f), new Vector2(288f, 256f));
        voteRoot = voting.gameObject;
        AddBackground(voting.gameObject);
        TMP_Text heading = RoundDamageRankingUI.CreateText("Heading", voting, font, 20f,
            new Vector2(12f, -8f), new Vector2(264f, 30f));
        heading.text = "ハプニング投票";
        voteTime = RoundDamageRankingUI.CreateText("VoteTime", voting, font, 16f,
            new Vector2(12f, -38f), new Vector2(264f, 24f));
        for (int i = 0; i < candidates.Length; i++)
        {
            RectTransform row = RoundDamageRankingUI.CreateRect("Candidate" + i, voting,
                new Vector2(0f, 1f), new Vector2(12f, -64f - i * 36f), new Vector2(264f, 32f));
            candidateBackgrounds[i] = AddBackground(row.gameObject);
            candidates[i] = RoundDamageRankingUI.CreateText("Label", row, font, 17f,
                new Vector2(8f, 0f), new Vector2(248f, 32f));
        }
        TMP_Text controls = RoundDamageRankingUI.CreateText("Controls", voting, font, 14f,
            new Vector2(12f, -176f), new Vector2(264f, 42f));
        controls.text = "上下で選択・Aで早めに確定\nPC：↑↓ / W・S、Enter";
        voteStatus = RoundDamageRankingUI.CreateText("VoteStatus", voting, font, 15f,
            new Vector2(12f, -220f), new Vector2(264f, 24f));

        // 確定後は操作できない候補を畳み、観戦映像の表示領域を戻す。
        RectTransform confirmation = RoundDamageRankingUI.CreateRect("HappeningVoteConfirmed", transform,
            new Vector2(0f, 1f), new Vector2(16f, -64f), new Vector2(288f, 40f));
        confirmationRoot = confirmation.gameObject;
        AddBackground(confirmationRoot);
        confirmationText = RoundDamageRankingUI.CreateText("ConfirmedChoice", confirmation, font, 16f,
            new Vector2(12f, 0f), new Vector2(264f, 40f));

        RectTransform notice = RoundDamageRankingUI.CreateRect("HappeningNotice", transform,
            new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(360f, 62f));
        notice.pivot = new Vector2(0.5f, 0f);
        noticeRoot = notice.gameObject;
        noticeBackground = AddBackground(noticeRoot);
        noticeTitle = RoundDamageRankingUI.CreateText("EffectName", notice, font, 20f,
            new Vector2(8f, -4f), new Vector2(344f, 30f));
        noticeTitle.alignment = TextAlignmentOptions.Center;
        noticeDetail = RoundDamageRankingUI.CreateText("EffectTime", notice, font, 16f,
            new Vector2(8f, -34f), new Vector2(344f, 24f));
        noticeDetail.alignment = TextAlignmentOptions.Center;
        voteRoot.SetActive(false);
        confirmationRoot.SetActive(false);
        noticeRoot.SetActive(false);
    }

    private static Image AddBackground(GameObject target)
    {
        Image image = target.AddComponent<Image>();
        image.color = new Color(0.02f, 0.04f, 0.08f, 0.65f);
        image.raycastTarget = false;
        return image;
    }

    private void Update()
    {
        if (manager == null || voteRoot == null) return;
        if (!manager.IsNetworkReady || !manager.IsRoundPlaying || manager.IsSuddenDeath)
        {
            voteRoot.SetActive(false);
            confirmationRoot.SetActive(false);
            noticeRoot.SetActive(false);
            lastPhase = RoundHappeningPhase.None;
            return;
        }

        RoundHappeningState state = manager.HappeningState;
        if (version != manager.DamageRoundVersion)
        {
            version = manager.DamageRoundVersion;
            lastPhase = RoundHappeningPhase.None;
            lastSeconds = lastSelection = lastVoteCount = lastInputStatus = -1;
        }
        RoundHappeningPhase current = state.Phase;
        if (manager.IsHappeningActive) current = RoundHappeningPhase.Active;
        else if (current == RoundHappeningPhase.Active && state.EffectEndsAt.Expired(manager.Runner)) current = RoundHappeningPhase.Finished;
        if (current == RoundHappeningPhase.Waiting && manager.IsHappeningVotingOpen) current = RoundHappeningPhase.Voting;
        bool changed = lastPhase != current;
        if (changed)
        {
            transitionAt = Time.unscaledTime;
            if (current == RoundHappeningPhase.Active) RoundSeManager.Instance?.NotifyHappeningStarted();
            if (current == RoundHappeningPhase.Finished && state.Effect != RoundHappeningType.None)
                RoundSeManager.Instance?.NotifyHappeningEnded();
        }

        bool voting = current == RoundHappeningPhase.Voting && manager.IsLocalDedicatedSpectator;
        bool confirmed = input != null && input.HasConfirmedVote;
        voteRoot.SetActive(voting && !confirmed);
        confirmationRoot.SetActive(voting && confirmed);
        if (confirmationRoot.activeSelf &&
            (changed || lastInputStatus != 1 || lastSelection != input.SelectedCandidate))
        {
            confirmationText.text = $"投票済み：{GetEffectName(RoundHappeningController.GetCandidate(input.SelectedCandidate), state)}";
            lastInputStatus = 1;
            lastSelection = input.SelectedCandidate;
        }
        if (voteRoot.activeSelf && input != null)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(state.VoteClosesAt.RemainingTime(manager.Runner) ?? 0f));
            int status = input.HasConfirmedVote ? 1 : input.IsPending ? 2 : input.WasRejected ? 3 : 0;
            if (changed || seconds != lastSeconds || lastSelection != input.SelectedCandidate ||
                lastVoteCount != manager.HappeningVoteCount || lastInputStatus != status)
            {
                voteTime.text = seconds > 0 ? $"残り {seconds} 秒・確定 {manager.HappeningVoteCount}票" : "投票終了・集計中";
                voteStatus.text = status == 1 ? "投票確定済み・変更はできません" : status == 2 ? "投票を送信中…" :
                    status == 3 ? "受付できませんでした。再度確定" : "時間切れで選択中の候補に投票";
                for (int i = 0; i < candidates.Length; i++)
                {
                    candidates[i].text = GetEffectName(RoundHappeningController.GetCandidate(i), state);
                    candidates[i].fontStyle = i == input.SelectedCandidate ? FontStyles.Bold : FontStyles.Normal;
                    candidateBackgrounds[i].color = i == input.SelectedCandidate
                        ? new Color(0.12f, 0.42f, 0.55f, 0.75f) : new Color(0.04f, 0.08f, 0.12f, 0.65f);
                }
                lastSeconds = seconds;
                lastSelection = input.SelectedCandidate;
                lastInputStatus = status;
                lastVoteCount = manager.HappeningVoteCount;
            }
        }

        bool finishedNotice = current == RoundHappeningPhase.Finished && Time.unscaledTime - transitionAt < 3f &&
            (state.Effect != RoundHappeningType.None || manager.IsLocalDedicatedSpectator);
        noticeRoot.SetActive(current == RoundHappeningPhase.Preview || current == RoundHappeningPhase.Active || finishedNotice);
        if (noticeRoot.activeSelf)
        {
            int seconds = current == RoundHappeningPhase.Preview
                ? Mathf.Max(0, Mathf.CeilToInt(state.EffectStartsAt.RemainingTime(manager.Runner) ?? 0f))
                : Mathf.Max(0, Mathf.CeilToInt(state.EffectEndsAt.RemainingTime(manager.Runner) ?? 0f));
            if (changed || seconds != lastSeconds)
            {
                noticeTitle.text = state.Effect == RoundHappeningType.None ? "投票終了" : GetEffectName(state.Effect, state);
                noticeDetail.text = current == RoundHappeningPhase.Preview ? $"開始まで {seconds} 秒" :
                    current == RoundHappeningPhase.Active ? $"発動中・残り {seconds} 秒" :
                    state.Effect == RoundHappeningType.None ? "投票なし：今回はハプニングなし" : "ハプニング終了";
                lastSeconds = seconds;
            }
            float flash = current == RoundHappeningPhase.Active ? Mathf.Clamp01((Time.unscaledTime - transitionAt) / 0.8f) : 1f;
            noticeBackground.color = Color.Lerp(new Color(0.55f, 0.25f, 0.05f, 0.8f), new Color(0.02f, 0.04f, 0.08f, 0.65f), flash);
        }
        lastPhase = current;
    }

    private static string GetEffectName(RoundHappeningType effect, RoundHappeningState state)
    {
        switch (effect)
        {
            case RoundHappeningType.DoubleDamage: return $"全員の攻撃力 {state.DamageMultiplier:0.##}倍";
            case RoundHappeningType.FasterMovement: return $"全員の移動速度 {state.MoveSpeedMultiplier:0.##}倍";
            case RoundHappeningType.NoJump: return "全員ジャンプ禁止";
            default: return "ハプニングなし";
        }
    }
}
