using UnityEngine;
using UnityEngine.Rendering;


public class TutorialTextManager : MonoBehaviour
{

    public GameObject controllerConnectedObject;
    public GameObject controllerDisconnectedObject;

    void Start()
    {
        bool controllerConnected = false;
        foreach (string name in Input.GetJoystickNames())
        {
            if (!string.IsNullOrEmpty(name))
            {
                controllerConnected = true;
                break;
            }
        }

        if (controllerConnectedObject != null)
            controllerConnectedObject.SetActive(controllerConnected);

        if (controllerDisconnectedObject != null)
            controllerDisconnectedObject.SetActive(!controllerConnected);
    }
}
