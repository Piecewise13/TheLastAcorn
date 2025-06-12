using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{ 
    [SerializeField] private GameObject pauseMenuUI;
    private PlayerKeyboardControls playerMovementMap; 
    private InputAction pauseInput;
    private bool isPaused = false;

    void Awake()
    {
        playerMovementMap = new PlayerKeyboardControls();
        pauseInput = playerMovementMap.Keyboard.Pause;
        pauseInput.performed += TogglePause; 
    }

    void OnEnable() { pauseInput.Enable(); }

    void OnDisable() { pauseInput.Disable(); }

    private void TogglePause(InputAction.CallbackContext context)
    {
        isPaused = !isPaused;
        if (isPaused) Pause();
        else Resume();
    }

    
    public void TogglePauseFromUIButton() { if (!isPaused) Pause(); }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneLoader.RestartLevel();
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneLoader.LoadMainMenu();
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
        Debug.Log("Game Quit");
    }
}
 