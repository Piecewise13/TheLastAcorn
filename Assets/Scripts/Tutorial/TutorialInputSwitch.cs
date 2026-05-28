using UnityEngine;

public class TutorialInputSwitch : MonoBehaviour
{

    [SerializeField] public GameObject keyboardTutorialObject;
    [SerializeField] public GameObject controllerTutorialObject;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InputSwitchManager.Instance.switchInputDevice += ChangeInputType;
        ChangeInputType(InputSwitchManager.currentInputDevice);
    }

    public void ChangeInputType(InputSwitchManager.InputDeviceType inputDeviceType)
    {
        switch (inputDeviceType)
        {
            case InputSwitchManager.InputDeviceType.Keyboard:
                keyboardTutorialObject.SetActive(true);
                controllerTutorialObject.SetActive(false);
                break;
            case InputSwitchManager.InputDeviceType.Gamepad:
                keyboardTutorialObject.SetActive(false);
                controllerTutorialObject.SetActive(true);
                break;
        }
    }
}
