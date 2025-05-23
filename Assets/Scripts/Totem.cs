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
    [SerializeField] private AudioPlayer sfxPlayer;

    [Header("Disable Sources")]
    [SerializeField] private AudioPlayer ambiencePlayer;
    [SerializeField] private AudioPlayer musicPlayer;

    const float FADE_TIME = 1.5f;
    const float DELAY_BEFORE_LOAD = 3f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        GetComponent<Collider2D>().enabled = false;
        StartCoroutine(TriggerFeedbackAndLoad());
    }

    IEnumerator TriggerFeedbackAndLoad()
    {
        if (ambiencePlayer) ambiencePlayer.FadeOut(FADE_TIME);
        if (musicPlayer)    musicPlayer.FadeOut(FADE_TIME);

        if (confetti) confetti.Play();
        if (growAndShrink) growAndShrink.Grow();
        if (sfxPlayer) sfxPlayer.Play();

        yield return new WaitForSeconds(DELAY_BEFORE_LOAD);
        SceneManager.LoadScene(nextSceneName);
    }
}
