using UnityEngine;

// Ensures this GameObject has a trigger collider
[RequireComponent(typeof(Collider2D))]
public class Collector : MonoBehaviour
{
    [Tooltip("Reference to the ScoreManager... if empty, reference in runtime")]
    [SerializeField] private ScoreManager scoreManager;

    private void Awake()
    {
        // Fallback - find the ScoreManager in the scene
        if (scoreManager == null)
        {
            scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager == null) Debug.LogError("ScoreManager not found in scene. Please add one or assign in Inspector.");
        }

        // collider set as trigger
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Look for ICollectible on collided object
        var collectible = other.GetComponent<ICollectible>();
        if (collectible != null)
        {
            scoreManager.AddScore(collectible.Value); // add value (figure out if we want to have different ones)
            // pickup SFX goes here :)
            Destroy(other.gameObject); // may wanna have a grow shrink script instead
        }
    }
}
