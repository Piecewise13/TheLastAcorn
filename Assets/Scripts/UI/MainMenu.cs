using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private float waitTime = 2f;
    [SerializeField] private AudioPlayer ambience;
    
    [SerializeField] private GameObject loadGameButton;

    private void Start()
    {
        if (!SaveLoadManager.IsLevelSaved())
        {
            loadGameButton.SetActive(false);
        }
        else
        {
            loadGameButton.SetActive(true);
        }
    }

    public void LoadSavedGame()
    {
        if (SaveLoadManager.IsLevelSaved())
        {
            string lastLevel = SaveLoadManager.GetLoadedLevel();
            StartCoroutine(WaitForTransition(waitTime, lastLevel));
        }
        else
        {
            Debug.LogWarning("No saved game found. Starting a new game instead.");
            StartNewGame();
        }
    }

    public void StartNewGame()
    {
        // Reset all game data for new game
        SaveLoadManager.ClearAllSaveData();
        
        // Reset score in ScoreManager if it exists
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetScore();
        }
        
        StartCoroutine(WaitForTransition(waitTime, "Cut Scenes"));
    }

    private IEnumerator WaitForTransition(float time, string sceneName)
    {
        FadeAudio();

        yield return new WaitForSeconds(time);
        SceneLoader.Load(sceneName);
    }

    public void QuitGame()
    {
        FadeAudio();
        Debug.Log("Quitting game...");
        Application.Quit();
    }

    private void FadeAudio() { ambience.FadeOut(1f); }
}
