using System.Collections; 
using UnityEngine;
using UnityEngine.SceneManagement;

public class Totem : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string nextSceneName = "NextScene";

    [Header("Feedback")]
    [SerializeField] private ParticleSystem confetti;
    [SerializeField] private GrowAndShrink growAndShrink;
    [SerializeField] private AudioPlayer audioPlayer;

    const float DELAY_BEFORE_LOAD = 3f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        StartCoroutine(TriggerFeedbackAndLoad());
    }

    IEnumerator TriggerFeedbackAndLoad()
    {
        if (confetti) confetti.Play();
        if (growAndShrink) growAndShrink.Grow();
        if (audioPlayer) audioPlayer.Play();
        
        yield return new WaitForSeconds(DELAY_BEFORE_LOAD);
        SceneManager.LoadScene(nextSceneName);
    }
}