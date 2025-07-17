using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneInitializer : MonoBehaviour
{
    private void Start()
    {
        // Initialize scene state tracking for acorn collection
        string currentSceneName = SceneManager.GetActiveScene().name;
        SaveLoadManager.InitializeSceneState(currentSceneName);
    }
}