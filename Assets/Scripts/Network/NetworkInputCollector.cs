using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public sealed class NetworkInputCollector
{
    private readonly InputActionReference moveAction;
    private readonly InputActionReference lookAction;
    private readonly InputActionReference hmdRotationAction;
    private readonly InputActionReference hmdPositionAction;

    private readonly InputActionReference leftHandPositionAction;
    private readonly InputActionReference leftHandRotationAction;

    private readonly InputActionReference rightHandPositionAction;
    private readonly InputActionReference rightHandRotationAction;

    private readonly InputActionReference jumpAction;
    private readonly InputActionReference fireAction;
    private readonly InputActionReference reloadAction;

    private readonly float keyboardLookSpeed;
    private readonly float mouseLookSpeed;
    private readonly float gamepadLookSpeed;
    private readonly float vrStickTurnSpeed;

    private readonly bool forceVrSimulationInEditor;
    private readonly bool useKeyboardHmdSimulationInEditor;
    private readonly float editorHmdRotationSpeed;

    private Vector2 editorHmdEuler;
    private Quaternion currentHMD = Quaternion.identity;

    private Vector2 queuedLookInput;
    private bool jumpQueued;
    private bool reloadQueued;
    private bool readyQueued;
    private bool skillQueued;
    private bool selectSkill1Queued;
    private bool selectSkill2Queued;
    private bool selectSkill3Queued;
    private bool selectSkill4Queued;
    private bool switchSkillQueued;
    private bool wasLeftTriggerPressed;

    public PlayerController LocalPlayerController { get; private set; }

    private bool IsVRActive
    {
        get
        {
#if UNITY_EDITOR
            if (forceVrSimulationInEditor)
            {
                return true;
            }
#endif
            return UnityEngine.XR.XRSettings.isDeviceActive;
        }
    }

    public NetworkInputCollector(
        InputActionReference moveAction,
        InputActionReference lookAction,
        InputActionReference hmdRotationAction,
        InputActionReference hmdPositionAction,
        InputActionReference leftHandPositionAction,
        InputActionReference leftHandRotationAction,
        InputActionReference rightHandPositionAction,
        InputActionReference rightHandRotationAction,
        InputActionReference jumpAction,
        InputActionReference fireAction,
        InputActionReference reloadAction,
        float keyboardLookSpeed,
        float mouseLookSpeed,
        float gamepadLookSpeed,
        float vrStickTurnSpeed,
        bool forceVrSimulationInEditor,
        bool useKeyboardHmdSimulationInEditor,
        float editorHmdRotationSpeed
    )
    {
        this.moveAction = moveAction;
        this.lookAction = lookAction;
        this.hmdRotationAction = hmdRotationAction;
        this.hmdPositionAction = hmdPositionAction;

        this.leftHandPositionAction = leftHandPositionAction;
        this.leftHandRotationAction = leftHandRotationAction;

        this.rightHandPositionAction = rightHandPositionAction;
        this.rightHandRotationAction = rightHandRotationAction;

        this.jumpAction = jumpAction;
        this.fireAction = fireAction;
        this.reloadAction = reloadAction;

        this.keyboardLookSpeed = keyboardLookSpeed;
        this.mouseLookSpeed = mouseLookSpeed;
        this.gamepadLookSpeed = gamepadLookSpeed;
        this.vrStickTurnSpeed = vrStickTurnSpeed;

        this.forceVrSimulationInEditor = forceVrSimulationInEditor;
        this.useKeyboardHmdSimulationInEditor = useKeyboardHmdSimulationInEditor;
        this.editorHmdRotationSpeed = editorHmdRotationSpeed;
    }

    public void EnableActions()
    {
        EnableAction(moveAction);
        EnableAction(lookAction);

        EnableAction(hmdRotationAction);
        EnableAction(hmdPositionAction);

        EnableAction(leftHandPositionAction);
        EnableAction(leftHandRotationAction);

        EnableAction(rightHandPositionAction);
        EnableAction(rightHandRotationAction);

        EnableAction(jumpAction);
        EnableAction(fireAction);
        EnableAction(reloadAction);
    }

    public void DisableActions()
    {
        DisableAction(moveAction);
        DisableAction(lookAction);

        DisableAction(hmdRotationAction);
        DisableAction(hmdPositionAction);

        DisableAction(leftHandPositionAction);
        DisableAction(leftHandRotationAction);

        DisableAction(rightHandPositionAction);
        DisableAction(rightHandRotationAction);

        DisableAction(jumpAction);
        DisableAction(fireAction);
        DisableAction(reloadAction);
    }

    public void UpdateHmdAndLook()
    {
        UpdateHMD();

        Vector2 lookInput = ReadLookInput();

        queuedLookInput += lookInput;

        if (LocalPlayerController != null)
        {
            LocalPlayerController.ApplyLocalLook(lookInput, IsVRActive, currentHMD);
        }
    }

    public void QueueGameplayButtons()
    {
        if (ReadJumpPressed())
        {
            jumpQueued = true;
        }

        if (ReadReloadPressed())
        {
            reloadQueued = true;
        }

        if (ReadReadyPressed())
        {
            readyQueued = true;
        }

        if (ReadSkillPressed())
        {
            skillQueued = true;
        }

        if (ReadSwitchSkillPressed())
        {
            switchSkillQueued = true;
        }
    }

    public void CollectInput(NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput();

        data.IsVR = IsVRActive;
        data.HMDRotation = currentHMD;
        data.HMDPosition = ReadVector3Action(hmdPositionAction);

        data.LeftHandPosition = ReadVector3Action(leftHandPositionAction);
        data.LeftHandRotation = ReadQuaternionAction(leftHandRotationAction);
        data.HasLeftHand = HasActionValue(leftHandPositionAction) ? (byte)1 : (byte)0;

        data.RightHandPosition = ReadVector3Action(rightHandPositionAction);
        data.RightHandRotation = ReadQuaternionAction(rightHandRotationAction);
        data.HasRightHand = HasActionValue(rightHandPositionAction) ? (byte)1 : (byte)0;

        data.MoveInput = ReadMoveInput();
        data.LookInput = queuedLookInput;

        if (LocalPlayerController != null)
        {
            data.AimForward = LocalPlayerController.GetNetworkAimForward();
            data.ViewForward = LocalPlayerController.GetNetworkViewForward();
            data.HasLookDirection = 1;
        }
        else
        {
            data.AimForward = Vector3.zero;
            data.ViewForward = Vector3.zero;
            data.HasLookDirection = 0;
        }

        NetworkButtons buttons = default;

        buttons.Set((int)PlayerInputButton.Jump, jumpQueued);
        buttons.Set((int)PlayerInputButton.Fire, ReadFireHeldInput());
        buttons.Set((int)PlayerInputButton.Reload, reloadQueued);
        buttons.Set((int)PlayerInputButton.Ready, readyQueued);
        buttons.Set((int)PlayerInputButton.Skill, skillQueued);
        buttons.Set((int)PlayerInputButton.SwitchSkill, switchSkillQueued);
        buttons.Set((int)PlayerInputButton.SelectSkill1, selectSkill1Queued);
        buttons.Set((int)PlayerInputButton.SelectSkill2, selectSkill2Queued);
        buttons.Set((int)PlayerInputButton.SelectSkill3, selectSkill3Queued);
        buttons.Set((int)PlayerInputButton.SelectSkill4, selectSkill4Queued);

        data.Buttons = buttons;

        input.Set(data);

        ClearQueuedInputs();
    }

    public bool ReadReconnectPressed()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    public void RegisterLocalPlayer(PlayerController playerController)
    {
        LocalPlayerController = playerController;
        Debug.Log($"[NetworkInputCollector] Registered local player: {playerController.name}");
    }

    public bool UnregisterLocalPlayer(PlayerController playerController)
    {
        if (playerController == null)
        {
            return false;
        }

        if (LocalPlayerController != playerController)
        {
            Debug.Log(
                $"[NetworkInputCollector] Ignore unregister because this is not current local player: {playerController.name}"
            );
            return false;
        }

        LocalPlayerController = null;

        Debug.Log($"[NetworkInputCollector] Unregistered local player: {playerController.name}");
        return true;
    }

    private void ClearQueuedInputs()
    {
        queuedLookInput = Vector2.zero;
        jumpQueued = false;
        reloadQueued = false;
        readyQueued = false;
        skillQueued = false;
        switchSkillQueued = false;
        selectSkill1Queued = false;
        selectSkill2Queued = false;
        selectSkill3Queued = false;
        selectSkill4Queued = false;
    }

    private void EnableAction(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return;
        }

        actionReference.action.Enable();
    }

    private void DisableAction(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return;
        }

        actionReference.action.Disable();
    }

    private void UpdateHMD()
    {
#if UNITY_EDITOR
        if (forceVrSimulationInEditor && useKeyboardHmdSimulationInEditor)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.iKey.isPressed)
                {
                    editorHmdEuler.x -= editorHmdRotationSpeed * Time.deltaTime;
                }

                if (Keyboard.current.kKey.isPressed)
                {
                    editorHmdEuler.x += editorHmdRotationSpeed * Time.deltaTime;
                }

                if (Keyboard.current.jKey.isPressed)
                {
                    editorHmdEuler.y -= editorHmdRotationSpeed * Time.deltaTime;
                }

                if (Keyboard.current.lKey.isPressed)
                {
                    editorHmdEuler.y += editorHmdRotationSpeed * Time.deltaTime;
                }
            }

            editorHmdEuler.x = Mathf.Clamp(editorHmdEuler.x, -85.0f, 85.0f);
            currentHMD = Quaternion.Euler(editorHmdEuler.x, editorHmdEuler.y, 0.0f);

            Debug.Log($"[Editor HMD] Euler={currentHMD.eulerAngles}");

            return;
        }
#endif

        if (!IsVRActive)
        {
            currentHMD = Quaternion.identity;
            return;
        }

        if (hmdRotationAction == null || hmdRotationAction.action == null)
        {
            currentHMD = Quaternion.identity;
            return;
        }

        currentHMD = hmdRotationAction.action.ReadValue<Quaternion>();

        Debug.Log($"[HMD] Euler={currentHMD.eulerAngles}");
    }

    private Vector2 ReadMoveInput()
    {
        if (moveAction != null && moveAction.action != null)
        {
            Vector2 actionMove = moveAction.action.ReadValue<Vector2>();

            if (actionMove.sqrMagnitude > 0.0001f)
            {
                return actionMove;
            }
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();

            if (stick.sqrMagnitude > 0.0001f)
            {
                return stick;
            }
        }

#if UNITY_EDITOR
        if (forceVrSimulationInEditor)
        {
            return Vector2.zero;
        }
#endif

        Vector2 moveInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
            {
                moveInput.y += 1.0f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                moveInput.y -= 1.0f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                moveInput.x -= 1.0f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                moveInput.x += 1.0f;
            }
        }

        return Vector2.ClampMagnitude(moveInput, 1.0f);
    }

    private Vector2 ReadLookInput()
    {
        if (lookAction != null && lookAction.action != null)
        {
            Vector2 rawLook = lookAction.action.ReadValue<Vector2>();

            if (IsVRActive)
            {
                return new Vector2(rawLook.x * vrStickTurnSpeed * Time.deltaTime, 0.0f);
            }

            if (Mouse.current != null && lookAction.action.activeControl?.device == Mouse.current)
            {
                return rawLook * mouseLookSpeed;
            }

            return rawLook * gamepadLookSpeed * Time.deltaTime;
        }

        Vector2 lookInput = Vector2.zero;

        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            lookInput.x += mouseDelta.x * mouseLookSpeed;
            lookInput.y += mouseDelta.y * mouseLookSpeed;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed)
            {
                lookInput.x -= keyboardLookSpeed * Time.deltaTime;
            }

            if (Keyboard.current.rightArrowKey.isPressed)
            {
                lookInput.x += keyboardLookSpeed * Time.deltaTime;
            }

            if (Keyboard.current.upArrowKey.isPressed)
            {
                lookInput.y += keyboardLookSpeed * Time.deltaTime;
            }

            if (Keyboard.current.downArrowKey.isPressed)
            {
                lookInput.y -= keyboardLookSpeed * Time.deltaTime;
            }
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.rightStick.ReadValue();

            lookInput.x += stick.x * gamepadLookSpeed * Time.deltaTime;
            lookInput.y += stick.y * gamepadLookSpeed * Time.deltaTime;
        }

        return lookInput;
    }

    private bool ReadJumpPressed()
    {
        if (jumpAction != null && jumpAction.action != null && jumpAction.action.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadReloadPressed()
    {
        if (reloadAction != null && reloadAction.action != null && reloadAction.action.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadReadyPressed()
    {
        bool readyPressed = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                readyPressed = true;
            }

            if (Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                readyPressed = true;
            }
        }

        if (Gamepad.current != null)
        {
            float leftTriggerValue = Gamepad.current.leftTrigger.ReadValue();
            bool leftTriggerPressed = leftTriggerValue > 0.5f;

            if (leftTriggerPressed && !wasLeftTriggerPressed)
            {
                readyPressed = true;
                reloadQueued = true;
            }

            wasLeftTriggerPressed = leftTriggerPressed;
        }
        else
        {
            wasLeftTriggerPressed = false;
        }

        return readyPressed;
    }

    private bool ReadSkillPressed()
    {
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadSwitchSkillPressed()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool ReadFireHeldInput()
    {
        if (fireAction != null && fireAction.action != null)
        {
            InputControl control = fireAction.action.activeControl;

            if (control is ButtonControl)
            {
                return fireAction.action.IsPressed();
            }

            return fireAction.action.ReadValue<float>() > 0.5f;
        }

        if (Keyboard.current != null && Keyboard.current.jKey.isPressed)
        {
            return true;
        }

        if (Gamepad.current != null)
        {
            return Gamepad.current.rightTrigger.ReadValue() > 0.5f;
        }

        return false;
    }

    private Vector3 ReadVector3Action(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return Vector3.zero;
        }

        return actionReference.action.ReadValue<Vector3>();
    }

    private Quaternion ReadQuaternionAction(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return Quaternion.identity;
        }

        Quaternion value = actionReference.action.ReadValue<Quaternion>();

        if (value.x == 0.0f &&
            value.y == 0.0f &&
            value.z == 0.0f &&
            value.w == 0.0f)
        {
            return Quaternion.identity;
        }

        return value;
    }

    private bool HasActionValue(InputActionReference actionReference)
    {
        return actionReference != null && actionReference.action != null;
    }
}
