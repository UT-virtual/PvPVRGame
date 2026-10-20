using Fusion;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// スキル選択中のローカルカーソルと2段階の選択進行を管理し、RoundManagerへRPCで選択を要求する。
/// StateAuthorityの受理通知で選択段階を進め、応答待ちの間は次の送信を抑止する。
/// </summary>
public sealed class LocalSkillSelectionInputController
{
    private readonly InputActionReference moveAction;
    private readonly float skillSelectionStickThreshold;

    private readonly Func<NetworkRunner> getRunner;

    private int currentSkillSelectionSlot;
    private int localSkillSelectionStep;
    private PlayerSkillType localFirstSelectedSkill = PlayerSkillType.None;

    private bool wasSkillSelecting;
    private bool skillSelectionMoveHeld;
    private bool localSkillSelectionConfirmed;
    private bool selectionPending;
    private const float SelectionRetryInterval = 1f;
    private const int MaxSelectionRetries = 3;
    private int pendingSlot;
    private int selectionVersion;
    private int retryCount;
    private float retryAt;
    private bool wasLeftVrTriggerPressed;
    private bool wasRightVrTriggerPressed;

    public int CurrentSkillSelectionSlot => currentSkillSelectionSlot;
    public int LocalSkillSelectionStep => localSkillSelectionStep;
    public bool HasLocalSkillSelectionConfirmed => localSkillSelectionConfirmed;

    public LocalSkillSelectionInputController(
        InputActionReference moveAction,
        float skillSelectionStickThreshold,
        Func<NetworkRunner> getRunner
    )
    {
        this.moveAction = moveAction;
        this.skillSelectionStickThreshold = skillSelectionStickThreshold;

        this.getRunner = getRunner;
    }

    /// <summary>
    /// 選択フェーズならローカル選択UIの操作を処理し、通常のゲーム入力収集を止めるためtrueを返す。
    /// フェーズ外ではローカルの選択進行をリセットする。
    /// </summary>
    public bool UpdateIfSkillSelecting()
    {
        bool isSkillSelecting =
            RoundManager.Instance != null &&
            RoundManager.Instance.IsSkillSelecting;

        if (!isSkillSelecting)
        {
            ResetSkillSelectionInputState();
            return false;
        }

        if (wasSkillSelecting && selectionVersion != RoundManager.Instance.SkillSelectionVersion)
        {
            ResetSkillSelectionInputState();
        }

        UpdateSkillSelectionInput();
        return true;
    }

    /// <summary>
    /// 自分宛ての選択進捗通知だけを受け、2つ受理された場合にローカルの完了表示を確定する。
    /// </summary>
    public void OnServerSkillSelectionProgress(PlayerRef playerRef, int selectedCount, int requestStep, PlayerSkillType firstSkill, int responseVersion)
    {
        NetworkRunner runner = getRunner();

        if (runner == null)
        {
            return;
        }

        if (playerRef != runner.LocalPlayer)
        {
            return;
        }

        Debug.Log(
            $"[LocalSkillSelectionInputController] Server confirmed skill selection. " +
            $"PlayerRef={playerRef}, Count={selectedCount}/2"
        );

        if (!wasSkillSelecting || responseVersion != selectionVersion ||
            requestStep != localSkillSelectionStep || selectedCount < localSkillSelectionStep)
        {
            return;
        }

        if (selectedCount > localSkillSelectionStep)
        {
            if (localSkillSelectionStep == 0)
            {
                localFirstSelectedSkill = firstSkill;
            }

            currentSkillSelectionSlot = 0;
            skillSelectionMoveHeld = true;
        }

        selectionPending = false;
        localSkillSelectionStep = Mathf.Clamp(selectedCount, 0, 2);
        localSkillSelectionConfirmed = selectedCount >= 2;
    }

    private void UpdateSkillSelectionInput()
    {
        Vector2 navigateInput = ReadSkillSelectionNavigateInput();

        if (!wasSkillSelecting)
        {
            currentSkillSelectionSlot = 0;
            localSkillSelectionStep = 0;
            localFirstSelectedSkill = PlayerSkillType.None;
            localSkillSelectionConfirmed = false;

            // 画面を開く前から倒していたスティックを、そのまま最初のカーソル移動として扱わない。
            skillSelectionMoveHeld = IsSkillSelectionNavigateActive(navigateInput);
            wasSkillSelecting = true;
            selectionVersion = RoundManager.Instance.SkillSelectionVersion;

            wasLeftVrTriggerPressed = IsXrTriggerHeld("<XRController>{LeftHand}");
            wasRightVrTriggerPressed = IsXrTriggerHeld("<XRController>{RightHand}");
            return;
        }

        if (selectionPending)
        {
            if (Time.unscaledTime >= retryAt)
            {
                if (retryCount < MaxSelectionRetries)
                {
                    retryCount++;
                    SendPendingSelection();
                }
                else
                {
                    // 未応答でも操作を永久に閉じない。遅延した要求はサーバーの選択段階で重複排除する。
                    selectionPending = false;
                    skillSelectionMoveHeld = IsSkillSelectionNavigateActive(navigateInput);
                    wasLeftVrTriggerPressed = IsXrTriggerHeld("<XRController>{LeftHand}");
                    wasRightVrTriggerPressed = IsXrTriggerHeld("<XRController>{RightHand}");
                }
            }
            return;
        }

        if (localSkillSelectionConfirmed)
        {
            return;
        }

        if (!IsSkillSelectionNavigateActive(navigateInput))
        {
            skillSelectionMoveHeld = false;
        }
        else if (!skillSelectionMoveHeld)
        {
            MoveSkillSelectionCursor(navigateInput);
            skillSelectionMoveHeld = true;
        }

        if (ReadSkillConfirmPressed())
        {
            QueueSelectedSkillSlot();
        }
    }

    public void ResetSkillSelectionInputState()
    {
        selectionPending = false;
        wasSkillSelecting = false;
        skillSelectionMoveHeld = false;
        localSkillSelectionStep = 0;
        localFirstSelectedSkill = PlayerSkillType.None;
        localSkillSelectionConfirmed = false;
        wasLeftVrTriggerPressed = false;
        wasRightVrTriggerPressed = false;
    }

    private Vector2 ReadSkillSelectionNavigateInput()
    {
        Vector2 input = Vector2.zero;

        if (moveAction != null && moveAction.action != null)
        {
            Vector2 actionInput = moveAction.action.ReadValue<Vector2>();

            if (actionInput.sqrMagnitude >= skillSelectionStickThreshold * skillSelectionStickThreshold)
            {
                return actionInput;
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.wasPressedThisFrame)
            {
                input.y += 1.0f;
            }

            if (Keyboard.current.sKey.wasPressedThisFrame)
            {
                input.y -= 1.0f;
            }

            if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                input.x += 1.0f;
            }

            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                input.x -= 1.0f;
            }
        }

        if (input.sqrMagnitude > 0.01f)
        {
            return input;
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();

            if (stick.sqrMagnitude >= skillSelectionStickThreshold * skillSelectionStickThreshold)
            {
                return stick;
            }
        }

        return Vector2.zero;
    }

    private bool IsSkillSelectionNavigateActive(Vector2 input)
    {
        return input.sqrMagnitude >= skillSelectionStickThreshold * skillSelectionStickThreshold;
    }

    private void MoveSkillSelectionCursor(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
        {
            return;
        }

        int row = currentSkillSelectionSlot / 2;
        int column = currentSkillSelectionSlot % 2;

        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            column += input.x > 0.0f ? 1 : -1;
        }
        else
        {
            row += input.y > 0.0f ? -1 : 1;
        }

        row = Mathf.Clamp(row, 0, 1);
        column = Mathf.Clamp(column, 0, 1);

        currentSkillSelectionSlot = row * 2 + column;

        Debug.Log($"[LocalSkillSelectionInputController] Skill cursor: {currentSkillSelectionSlot + 1}");
    }

    private bool ReadSkillConfirmPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            return true;
        }

        if(ReadVrTriggerConfirmPressed())
        {
            return true;
        }

        return false;
    }

    private bool ReadVrTriggerConfirmPressed()
    {
        if (!UnityEngine.XR.XRSettings.isDeviceActive)
        {
            return false;
        }
        bool leftPressed = WasXrTriggerPressedThisFrame("<XRController>{LeftHand}", ref wasLeftVrTriggerPressed);
        bool rightPressed = WasXrTriggerPressedThisFrame("<XRController>{RightHand}", ref wasRightVrTriggerPressed);
        return leftPressed || rightPressed;
    }
    private static bool IsXrTriggerHeld(string handPath)
    {
        InputControl triggerButton = InputSystem.FindControl($"{handPath}/triggerButton");
        if (triggerButton is ButtonControl button && button.isPressed)
        {
            return true;
        }
        return false;
    }
    private static bool WasXrTriggerPressedThisFrame(string handPath, ref bool wasPressedLastFrame)
    {
        bool isPressed = IsXrTriggerHeld(handPath);
        bool pressedThisFrame = isPressed && !wasPressedLastFrame;
        wasPressedLastFrame = isPressed;
        return pressedThisFrame;
    }

    /// <summary>
    /// 選択をRPCへ送り、受理通知を待つ。同一フレームでHost側の応答が戻る場合も先に待機状態を設定する。
    /// </summary>
    private void QueueSelectedSkillSlot()
    {
        if (selectionPending || !CanConfirmCurrentSkillSelection())
        {
            return;
        }

        NetworkRunner runner = getRunner();
        RoundManager roundManager = RoundManager.Instance;
        if (runner == null || !runner.IsRunning || roundManager == null || !roundManager.IsSkillSelecting)
        {
            return;
        }

        pendingSlot = currentSkillSelectionSlot;
        retryCount = 0;
        selectionPending = true;
        SendPendingSelection();
    }

    private void SendPendingSelection()
    {
        NetworkRunner runner = getRunner();
        RoundManager roundManager = RoundManager.Instance;
        retryAt = Time.unscaledTime + SelectionRetryInterval;
        if (runner == null || !runner.IsRunning || roundManager == null || !roundManager.IsSkillSelecting)
        {
            selectionPending = false;
            return;
        }

        try
        {
            roundManager.RPC_RequestSelectSkillBySlot(runner.LocalPlayer, pendingSlot, selectionVersion, localSkillSelectionStep);
        }
        catch (Exception exception)
        {
            selectionPending = false;
            Debug.LogException(exception);
        }
    }

    private bool CanConfirmCurrentSkillSelection()
    {
        RoundManager roundManager = RoundManager.Instance;

        if (roundManager == null)
        {
            return false;
        }

        PlayerSkillType selectedSkill = roundManager.GetSkillOption(
            currentSkillSelectionSlot,
            localSkillSelectionStep
        );

        if (selectedSkill == PlayerSkillType.None)
        {
            Debug.LogWarning(
                $"[LocalSkillSelectionInputController] Cannot confirm empty skill slot: " +
                $"{currentSkillSelectionSlot + 1}, Step={localSkillSelectionStep + 1}"
            );

            return false;
        }

        if (localSkillSelectionStep > 0 && selectedSkill == localFirstSelectedSkill)
        {
            Debug.LogWarning(
                $"[LocalSkillSelectionInputController] Cannot select same skill twice: {selectedSkill}"
            );

            return false;
        }

        return true;
    }
}
