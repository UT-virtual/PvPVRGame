using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private float keyboardLookSpeed = 90.0f;
    [SerializeField] private float mouseLookSpeed = 0.12f;
    [SerializeField] private float gamepadLookSpeed = 120.0f;

    private bool wasLeftTriggerPressed;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool FireHeld { get; private set; }
    public bool ReloadPressed { get; private set; }

    public void ReadInput()
    {
        MoveInput = ReadMoveInput();
        LookInput = ReadLookInput();
        JumpPressed = ReadJumpInput();
        FireHeld = ReadFireHeldInput();
        ReloadPressed = ReadReloadInput();
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

        if (Gamepad.current != null)
        {
            input = Gamepad.current.leftStick.ReadValue();
        }

        if (Keyboard.current != null)
        {
            Vector2 keyboardInput = Vector2.zero;

            if (Keyboard.current.wKey.isPressed)
            {
                keyboardInput.y += 1.0f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                keyboardInput.y -= 1.0f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                keyboardInput.x += 1.0f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                keyboardInput.x -= 1.0f;
            }

            if (keyboardInput.sqrMagnitude > 1.0f)
            {
                keyboardInput.Normalize();
            }

            if (keyboardInput.sqrMagnitude > 0.01f)
            {
                input = keyboardInput;
            }
        }

        return input;
    }

    private Vector2 ReadLookInput()
    {
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

    private bool ReadJumpInput()
    {
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

    private bool ReadFireHeldInput()
    {
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

    private bool ReadReloadInput()
    {
        bool reload = false;

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            reload = true;
        }

        if (Gamepad.current != null)
        {
            float leftTriggerValue = Gamepad.current.leftTrigger.ReadValue();
            bool leftTriggerPressed = leftTriggerValue > 0.5f;

            if (leftTriggerPressed && !wasLeftTriggerPressed)
            {
                reload = true;
            }

            wasLeftTriggerPressed = leftTriggerPressed;
        }
        else
        {
            wasLeftTriggerPressed = false;
        }

        return reload;
    }
}