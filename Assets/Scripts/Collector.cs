using UnityEngine;
using UnityEngine.Serialization;

public class Collector : MonoBehaviour
{
    [Tooltip("Reference to the ScoreManager. If left empty it’s resolved at runtime.")]
    [SerializeField] private ScoreManager scoreManager;

    [Tooltip("Trigger collider that detects collectibles (can be on a child GameObject).")]
    [SerializeField] private Collider2D triggerCollider;

    [SerializeField] private AudioPlayer sfxPlayer;

    private void Awake()
    {
        if (scoreManager == null)
        {
            scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager == null) 
                Debug.LogError("ScoreManager not found in scene. Please add one or assign it in the Inspector.");
        }
        
        if (triggerCollider == null)
        {
            triggerCollider = GetComponentInChildren<Collider2D>();
            if (triggerCollider == null)
                Debug.LogError("Collector: no Collider2D found on this GameObject or its children.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Look for an ICollectible on the collided object
        var collectible = other.GetComponent<ICollectible>();
        if (collectible == null) return;

        scoreManager.AddScore(collectible.Value);  
        sfxPlayer?.Play();

        Destroy(other.gameObject);      // Replace with an animation
    }
}