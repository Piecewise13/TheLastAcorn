using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


public class InputSwitchManager : MonoBehaviour
{

    public GameObject controllerConnectedObject;
    public GameObject controllerDisconnectedObject;

    private PlayerGameControls playerMovementMap;
    private InputAction testInputDevice;

    void Awake()
    {
        playerMovementMap = new PlayerGameControls();

        testInputDevice = playerMovementMap.Gameplay.TestKeyboardControllerInput;
        testInputDevice.performed += SwitchControls;
        testInputDevice.Enable();

    }

    void Start()
    {


    }
    void Update()
    {

    }

    private void SwitchControls(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (context.control.device is Keyboard)
            {
                controllerConnectedObject.SetActive(false);
                controllerDisconnectedObject.SetActive(true);
            }
            else if (context.control.device is Gamepad)
            {
                controllerConnectedObject.SetActive(true);
                controllerDisconnectedObject.SetActive(false);
            }
        }
    }
}
