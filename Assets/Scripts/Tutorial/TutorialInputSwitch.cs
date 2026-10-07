using UnityEngine;

public class TutorialInputSwitch : MonoBehaviour
{

    [SerializeField] public GameObject keyboardTutorialObject;
    [SerializeField] public GameObject controllerTutorialObject;

    // Start rather than OnEnable: InputSwitchManager sets Instance in Awake, and a sibling
    // component's OnEnable can run before that Awake.
    void Start()
    {
        if (InputSwitchManager.Instance != null)
        {
            InputSwitchManager.Instance.switchInputDevice += ChangeInputType;
        }

        ChangeInputType(InputSwitchManager.currentInputDevice);
    }

    // Prompts in pushed views are destroyed when the view pops while the manager can live on,
    // so drop the handler or the next device switch hits a destroyed object.
    private void OnDestroy()
    {
        if (InputSwitchManager.Instance != null)
        {
            InputSwitchManager.Instance.switchInputDevice -= ChangeInputType;
        }
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
