using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


public class InputSwitchManager : MonoBehaviour
{
    public static InputSwitchManager Instance { get; private set; }

    public GameObject controllerConnectedObject;
    public GameObject controllerDisconnectedObject;

    private PlayerGameControls playerMovementMap;
    private InputAction testInputDevice;

    public enum InputDeviceType
    {
        Keyboard,
        Gamepad
    }

    public Action<InputDeviceType> switchInputDevice;
    
    public static InputDeviceType currentInputDevice;

    void Awake()
    {
        playerMovementMap = new PlayerGameControls();

        testInputDevice = playerMovementMap.Gameplay.TestKeyboardControllerInput;
        testInputDevice.performed += SwitchControls;
        testInputDevice.Enable();

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

    }

    private void OnEnable()
    {
        switchInputDevice?.Invoke(currentInputDevice);
    }

    private void SwitchControls(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (context.control.device is Keyboard)
            {
                currentInputDevice = InputDeviceType.Keyboard;
                switchInputDevice?.Invoke(InputDeviceType.Keyboard);
            }
            else if (context.control.device is Gamepad)
            {
                currentInputDevice = InputDeviceType.Gamepad;
                switchInputDevice?.Invoke(InputDeviceType.Gamepad);       
            }
        }
    }
}
