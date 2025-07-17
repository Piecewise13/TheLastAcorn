using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneInitializer : MonoBehaviour
{
    private void Start()
    {
        // Initialize scene state tracking for acorn collection
        string currentSceneName = SceneManager.GetActiveScene().name;
        SaveLoadManager.InitializeSceneState(currentSceneName);
        
        // If debug mode is enabled, reset scores to simulate first-time loading
        if (DebugSettings.Instance != null && DebugSettings.Instance.DisablePersistence)
        {
            // Reset the global score to 0 for debug mode
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ResetScore();
            }
            
            // Reset level score to 0 for debug mode
            if (LevelScoreManager.Instance != null)
            {
                LevelScoreManager.Instance.ResetLevelScore();
            }
            
            if (DebugSettings.Instance.ShowDebugLogs)
            {
                Debug.Log($"[SceneInitializer] DEBUG MODE: Reset scores for scene {currentSceneName}");
            }
        }
    }
}