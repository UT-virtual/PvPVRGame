using Fusion;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class LocalSkillSelectionInputController
{
    private readonly InputActionReference moveAction;
    private readonly float skillSelectionStickThreshold;

    private readonly Func<NetworkRunner> getRunner;
    private readonly Func<PlayerController> getLocalPlayerController;

    private int currentSkillSelectionSlot;
    private int localSkillSelectionStep;
    private PlayerSkillType localFirstSelectedSkill = PlayerSkillType.None;

    private bool wasSkillSelecting;
    private bool skillSelectionMoveHeld;
    private bool localSkillSelectionConfirmed;

    public int CurrentSkillSelectionSlot => currentSkillSelectionSlot;
    public int LocalSkillSelectionStep => localSkillSelectionStep;
    public bool HasLocalSkillSelectionConfirmed => localSkillSelectionConfirmed;

    public LocalSkillSelectionInputController(
        InputActionReference moveAction,
        float skillSelectionStickThreshold,
        Func<NetworkRunner> getRunner,
        Func<PlayerController> getLocalPlayerController
    )
    {
        this.moveAction = moveAction;
        this.skillSelectionStickThreshold = skillSelectionStickThreshold;

        this.getRunner = getRunner;
        this.getLocalPlayerController = getLocalPlayerController;
    }

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

        UpdateSkillSelectionInput();
        return true;
    }

    public void OnServerSkillSelectionProgress(PlayerRef playerRef, int selectedCount)
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

        if (selectedCount >= 2)
        {
            localSkillSelectionConfirmed = true;
        }
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

            skillSelectionMoveHeld = IsSkillSelectionNavigateActive(navigateInput);
            wasSkillSelecting = true;
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

    private void ResetSkillSelectionInputState()
    {
        wasSkillSelecting = false;
        skillSelectionMoveHeld = false;
        localSkillSelectionStep = 0;
        localFirstSelectedSkill = PlayerSkillType.None;
        localSkillSelectionConfirmed = false;
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

        return false;
    }

    private void QueueSelectedSkillSlot()
    {
        if (!CanConfirmCurrentSkillSelection())
        {
            return;
        }

        RoundManager roundManager = RoundManager.Instance;

        PlayerSkillType selectedSkill = roundManager != null
            ? roundManager.GetSkillOption(currentSkillSelectionSlot, localSkillSelectionStep)
            : PlayerSkillType.None;

        int selectedSlot = currentSkillSelectionSlot;

        SendSkillSelectionRpc(selectedSlot);

        Debug.Log(
            $"[LocalSkillSelectionInputController] Confirm skill slot by RPC: " +
            $"{selectedSlot + 1}, " +
            $"Step={localSkillSelectionStep + 1}, " +
            $"Skill={selectedSkill}"
        );

        if (localSkillSelectionStep <= 0)
        {
            localFirstSelectedSkill = selectedSkill;
            localSkillSelectionStep = 1;
            currentSkillSelectionSlot = 0;
            skillSelectionMoveHeld = true;
            localSkillSelectionConfirmed = false;
            return;
        }

        localSkillSelectionConfirmed = true;
    }

    private void SendSkillSelectionRpc(int slotIndex)
    {
        NetworkRunner runner = getRunner();

        if (runner == null)
        {
            Debug.LogWarning("[LocalSkillSelectionInputController] Cannot send skill RPC. runner is null.");
            return;
        }

        if (RoundManager.Instance == null)
        {
            Debug.LogWarning("[LocalSkillSelectionInputController] Cannot send skill RPC. RoundManager.Instance is null.");
            return;
        }

        RoundManager.Instance.RPC_RequestSelectSkillBySlot(
            runner.LocalPlayer,
            slotIndex
        );
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

        PlayerController localPlayerController = getLocalPlayerController();

        if (localPlayerController == null)
        {
            return true;
        }

        PlayerSkillController skillController = localPlayerController.GetComponent<PlayerSkillController>();

        if (skillController != null && !skillController.CanSelectSkill(selectedSkill))
        {
            Debug.LogWarning(
                $"[LocalSkillSelectionInputController] Cannot confirm skill because it cannot be selected: {selectedSkill}"
            );

            return false;
        }

        return true;
    }
}
