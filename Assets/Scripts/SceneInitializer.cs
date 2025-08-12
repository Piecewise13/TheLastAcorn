using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneInitializer : MonoBehaviour
{

    private GameObject player;

    private GameObject owl;

    private CheckpointManager checkpointManager;

    /// <summary>
    /// This direction the player is moving through the levels. True is forward (1, 2, 3...), false is backward (3, 2, 1...).
    /// </summary>
    private static bool playerMovingForward = true;

    private void Start()
    {
        print("[SceneInitializer] Initializing scene...");
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



        player = GameObject.FindGameObjectWithTag("Player");
        player = player.transform.root.gameObject; // Ensure we get the root player object


        checkpointManager = FindAnyObjectByType<CheckpointManager>();

        owl = FindAnyObjectByType<Owl>().gameObject;
        owl.SetActive(false); // Initially hide the owl

        InitializeScene();

    }


    private void InitializeScene()
    {

        if (DebugSettings.Instance != null && DebugSettings.Instance.DefaultSpawnPoint)
        {
            SetSpawnPoint();
        }

        if (!SaveLoadManager.HasLevelBeenLoaded(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (owl != null) {
            owl.SetActive(true);
        }
    }

    private void SetSpawnPoint()
    {
        if (!playerMovingForward)
        {
            checkpointManager.SpawnAtEndingLocation(player);
            return;
        }

        checkpointManager.SpawnAtInitalLocation(player);
        
    }

    static public void SetLevelDirection(bool direction)
    {
        playerMovingForward = direction;
    }
}