using Fusion;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 接続後の自動参加と待機中の役割切替入力を送り、サーバーの応答で役割を確定する。
/// キャラクター生成は行わず、役割変更時には未送信の対戦入力だけを破棄する。
/// </summary>
public sealed class LocalParticipantRoleController
{
    private readonly Func<NetworkRunner> getRunner;
    private readonly Action clearPendingInput;
    private readonly Action resetSkillSelection;
    private const float RoleRequestRetryInterval = 1f;
    private int roleRequestId;
    private int roleRetryCount;
    private float roleRetryAt;
    private ParticipantRole requestedRole;
    private bool rightRoleButtonHeld;
    private bool initialAssignment;
    private float automaticAssignmentAt;

    public ParticipantRole LocalRole { get; private set; }
    public bool IsRoleRequestPending { get; private set; }
    public string RoleStatus { get; private set; } = "";
    public bool IsConnected => getRunner() != null && getRunner().IsRunning && !getRunner().IsShutdown;
    public bool IsLocalCombatant => IsConnected && LocalRole == ParticipantRole.Combatant;
    public bool IsDedicatedSpectator => IsConnected && LocalRole == ParticipantRole.Spectator;
    public bool CanChooseRole => IsConnected && RoundManager.Instance != null &&
        RoundManager.Instance.IsNetworkReady && (RoundManager.Instance.IsWaitingForReady ||
        LocalRole == ParticipantRole.Unselected);


    public LocalParticipantRoleController(Func<NetworkRunner> getRunner, Action clearPendingInput,
        Action resetSkillSelection)
    {
        this.getRunner = getRunner;
        this.clearPendingInput = clearPendingInput;
        this.resetSkillSelection = resetSkillSelection;
    }

    public void Update()
    {
        bool rightHeld = IsRoleButtonHeld("<XRController>{RightHand}/primaryButton");
        bool toggleRole = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame) || (rightHeld && !rightRoleButtonHeld);
        rightRoleButtonHeld = rightHeld;

        if (CanChooseRole && !IsRoleRequestPending)
        {
            if (LocalRole == ParticipantRole.Unselected && Time.unscaledTime >= automaticAssignmentAt)
            {
                BeginRequest(ParticipantRole.Combatant, true);
            }
            else if (toggleRole && LocalRole != ParticipantRole.Unselected)
            {
                RequestRole(LocalRole == ParticipantRole.Combatant ? ParticipantRole.Spectator : ParticipantRole.Combatant);
            }
        }

        if (IsRoleRequestPending && Time.unscaledTime >= roleRetryAt)
        {
            if (roleRetryCount++ < 3 && CanChooseRole)
            {
                SendRoleRequest();
            }
            else
            {
                IsRoleRequestPending = false;
                RoleStatus = "役割の応答を確認できませんでした。";
                automaticAssignmentAt = Time.unscaledTime + RoleRequestRetryInterval;
            }
        }
    }

    private static bool IsRoleButtonHeld(string path) =>
        InputSystem.FindControl(path) is UnityEngine.InputSystem.Controls.ButtonControl button && button.isPressed;

    /// <summary>
    /// 役割を要求し、サーバーの受理通知までローカルの役割を変えない。再送には同じ要求番号を使う。
    /// </summary>
    public void RequestRole(ParticipantRole role)
    {
        if (!CanChooseRole || IsRoleRequestPending)
        {
            return;
        }

        BeginRequest(role, false);
    }

    private void BeginRequest(ParticipantRole role, bool isInitialAssignment)
    {
        requestedRole = role;
        initialAssignment = isInitialAssignment;
        roleRequestId++;
        roleRetryCount = 0;
        IsRoleRequestPending = true;
        RoleStatus = "役割を確認中…";
        clearPendingInput();
        SendRoleRequest();
    }

    private void SendRoleRequest()
    {
        roleRetryAt = Time.unscaledTime + RoleRequestRetryInterval;
        RoundManager manager = RoundManager.Instance;
        if (!IsConnected || manager == null || !manager.IsNetworkReady)
        {
            return;
        }

        try
        {
            manager.RPC_RequestParticipantRole(getRunner().LocalPlayer, requestedRole, roleRequestId, initialAssignment);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            IsRoleRequestPending = false;
            RoleStatus = "役割を送信できませんでした。";
            automaticAssignmentAt = Time.unscaledTime + RoleRequestRetryInterval;
        }
    }

    /// <summary>
    /// 自分宛ての最新要求の応答だけを適用する。拒否時もサーバーが保持する元の役割へ戻す。
    /// </summary>
    public void OnResult(PlayerRef player, int requestId,
        ParticipantRole role, string message)
    {
        if (!IsConnected || player != getRunner().LocalPlayer || requestId != roleRequestId)
        {
            return;
        }

        LocalRole = role;
        IsRoleRequestPending = false;
        RoleStatus = message;
        automaticAssignmentAt = Time.unscaledTime + RoleRequestRetryInterval;
        if (!string.IsNullOrEmpty(message))
        {
            Debug.LogWarning($"[LocalParticipantRoleController] {message}");
        }
        clearPendingInput();
        resetSkillSelection();
    }

    public void ResetSession()
    {
        LocalRole = ParticipantRole.Unselected;
        IsRoleRequestPending = false;
        RoleStatus = "";
        automaticAssignmentAt = 0f;
        // 旧セッションの応答が、新しい要求に一致しないよう番号を使い回さない。
        roleRequestId++;
    }
}
