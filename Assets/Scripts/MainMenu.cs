using UnityEngine;
using UnityEngine.InputSystem;

public class MainMenu : MonoBehaviour
{
    void Update(){ if (Keyboard.current.spaceKey.wasPressedThisFrame) LoadGame();}

    public void LoadGame(){ SceneLoader.LoadNext();}
}
