using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Takes the player from the bottom of the branch-break hole into the cave. When the player falls
/// into this trigger the next scene loads straight away; that scene's CheckpointManager puts the
/// player at its start point.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NextLevelTrigger : MonoBehaviour
{
    [Tooltip("Scene to load. Must be in the build settings.")]
    [SerializeField] private string nextSceneName = "Level2-Cave";

    private bool hasRun;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasRun || !other.CompareTag("Player")) return;

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("NextLevelTrigger: no next scene name set.", this);
            return;
        }

        hasRun = true;

        SaveLoadManager.SaveLevelData(SceneManager.GetActiveScene().name);
        SaveLoadManager.SaveCurrentLevelName(nextSceneName);

        SceneManager.LoadScene(nextSceneName);
    }
}
