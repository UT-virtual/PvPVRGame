using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 専用観戦者の候補選択と投票要求を管理する。カメラ操作とは軸の優先判定だけを共有する。
/// フォーカスを随時送信して締切時の自動確定に備え、Aによる早期確定は応答または同期票で判断する。
/// </summary>
public sealed class SpectatorHappeningInputController : MonoBehaviour
{
    private RoundManager manager;
    private int roundVersion = -1;
    private bool verticalHeld;
    private float retryAt;
    private int focusRevision;
    private int lastSentFocus = -1;
    private float focusRetryAt;
    public int SelectedCandidate { get; private set; }
    public bool HasConfirmedVote { get; private set; }
    public bool IsPending { get; private set; }
    public bool WasRejected { get; private set; }

    public void Initialize(RoundManager roundManager) => manager = roundManager;

    private void Update()
    {
        if (manager == null || !manager.IsNetworkReady) return;
        if (roundVersion != manager.DamageRoundVersion)
        {
            roundVersion = manager.DamageRoundVersion;
            SelectedCandidate = 0;
            focusRevision = 0;
            lastSentFocus = -1;
            HasConfirmedVote = IsPending = WasRejected = verticalHeld = false;
        }

        if (!manager.IsLocalDedicatedSpectator) return;
        if (manager.HappeningVotes.TryGet(manager.Runner.LocalPlayer, out int confirmed))
        {
            SelectedCandidate = confirmed;
            HasConfirmedVote = true;
            IsPending = false;
        }
        bool canChoose = manager.IsHappeningVotingOpen && !HasConfirmedVote && !IsPending;
        Vector2Int direction = Gamepad.current != null
            ? GetPrimaryStickDirection(Gamepad.current.leftStick.ReadValue(), 0.6f, true) : Vector2Int.zero;
        bool move = direction.y != 0 && !verticalHeld;
        verticalHeld = direction.y != 0;
        if (canChoose)
        {
            int step = move ? -direction.y : 0;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame) step = -1;
                else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame) step = 1;
            }
            if (step != 0)
            {
                SelectedCandidate = (SelectedCandidate + step + RoundHappeningController.CandidateCount) % RoundHappeningController.CandidateCount;
                WasRejected = false;
            }
            // 締切時の通信往復に頼らず、表示中の候補を先に進行側へ届ける。
            if (lastSentFocus != SelectedCandidate)
            {
                lastSentFocus = SelectedCandidate;
                focusRevision++;
                SendFocus();
            }
            else if (Time.unscaledTime >= focusRetryAt)
            {
                SendFocus();
            }
            bool confirm = IsConfirmPressed(Gamepad.current) ||
                (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame);
            if (confirm)
            {
                IsPending = true;
                WasRejected = false;
                SendVote();
            }
        }
        else if (IsPending && manager.IsHappeningVotingOpen && Time.unscaledTime >= retryAt)
        {
            SendVote();
        }
    }

    private void SendVote()
    {
        retryAt = Time.unscaledTime + 1f;
        manager.RPC_RequestHappeningVote(roundVersion, SelectedCandidate);
    }

    private void SendFocus()
    {
        focusRetryAt = Time.unscaledTime + 1f;
        manager.RPC_UpdateHappeningFocus(roundVersion, SelectedCandidate, focusRevision);
    }

    private static bool IsConfirmPressed(Gamepad pad)
    {
        if (pad == null) return false;
        // Switch系ではAが東側にあるため、固定位置ではなく表示名がAのボタンを優先する。
        return (pad.buttonEast.displayName == "A" ? pad.buttonEast : pad.buttonSouth).wasPressedThisFrame;
    }

    public void ApplyResult(int version, int candidate, bool accepted)
    {
        if (version != roundVersion || candidate != SelectedCandidate) return;
        IsPending = false;
        HasConfirmedVote = accepted;
        WasRejected = !accepted;
    }

    /// <summary>
    /// 斜め入力は絶対値が大きい軸だけを返す。同値なら左右を優先し、両方の操作が同時に発火しないようにする。
    /// </summary>
    public static Vector2Int GetPrimaryStickDirection(Vector2 stick, float threshold, bool allowVertical)
    {
        if (allowVertical && Mathf.Abs(stick.y) > Mathf.Abs(stick.x) && Mathf.Abs(stick.y) >= threshold)
            return new Vector2Int(0, stick.y > 0f ? 1 : -1);
        if (Mathf.Abs(stick.x) >= threshold) return new Vector2Int(stick.x > 0f ? 1 : -1, 0);
        return Vector2Int.zero;
    }
}
