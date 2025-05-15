using UnityEngine;

public class Collector : MonoBehaviour
{
    [SerializeField] private Collider2D triggerCollider;
    [SerializeField] private AudioPlayer sfxPlayer;
    [SerializeField] private CompletionBar  completionBar;

    private void Awake()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponentInChildren<Collider2D>();
            if (triggerCollider == null)
                Debug.LogError("Collector: no Collider2D assigned or found on children.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.enabled) return;
        if (!other.TryGetComponent<ICollectible>(out var collectible)) return;

        other.enabled = false;
        ScoreManager.Instance.AddScore(collectible.Value);

        // Feedback
        completionBar?.Add(collectible.Value);
        sfxPlayer?.Play();
        if (other.TryGetComponent(out GrowAndShrink gs))
            gs.ShrinkAndDisable();
        

        if (completionBar != null && completionBar.IsComplete)
        {
            Debug.Log("All items collected!");
            // GameManager.LevelComplete(); 
        }
    }
}
