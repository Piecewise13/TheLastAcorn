using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
        /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerKeyboardControls playerMovementMap;

    private InputAction pauseInput;

    public GameObject pauseMenuUI; // Assign in Inspector
    private bool isPaused = false;

    void Awake()
    {
        // Initialize input action map
        playerMovementMap = new PlayerKeyboardControls();
        pauseInput = playerMovementMap.Keyboard.Pause;
        pauseInput.performed += TogglePause; 
    }

    void OnEnable()
    {
        pauseInput.Enable();
    }

    void OnDisable()
    {
        pauseInput.Disable();
    }


    private void TogglePause(InputAction.CallbackContext context)
    {
        isPaused = !isPaused;
        if (isPaused)
        {
            Pause();
        }
        else
        {
            Resume();
        }
    }

    
    public void TogglePauseFromUIButton()
    {
        if (!isPaused)
        {
            Pause();
        }
    }

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
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main Menu");
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
        Debug.Log("Game Quit");
    }
}
 