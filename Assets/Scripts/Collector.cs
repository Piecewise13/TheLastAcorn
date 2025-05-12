using UnityEngine;

public class Collector : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private Collider2D   triggerCollider;
    [SerializeField] private AudioPlayer  sfxPlayer;

    private void Awake()
    {
        if (scoreManager == null)
        {
            scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager == null)
                Debug.LogError("ScoreManager not found—add one or assign it in the Inspector.");
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponentInChildren<Collider2D>();
            if (triggerCollider == null)
                Debug.LogError("Collector: no Collider2D assigned or found on children.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<ICollectible>(out var collectible)) return;

        scoreManager.AddScore(collectible.Value);
        sfxPlayer?.Play();

        if (other.TryGetComponent(out GrowAndShrink gs))
            gs.ShrinkAndDisable();
    }
}