using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Totem : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string nextSceneName = "NextScene";

    [Header("Success Feedback")]
    [SerializeField] private ParticleSystem confetti;
    [SerializeField] private GrowAndShrink growAndShrink;
    [SerializeField] private AudioPlayer sfxPlayer;   

    [Header("Not-Ready Feedback")]
    [SerializeField] private AudioPlayer notReadySfx;   

    [Header("Fade-Out Sources")]
    [SerializeField] private AudioPlayer ambiencePlayer;
    [SerializeField] private AudioPlayer musicPlayer;

    const float FADE_TIME = 1.5f;
    const float DELAY_BEFORE_LOAD = 3f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        GetComponent<Collider2D>().enabled = false;

        if (CompletionBar.AllCollected)
            StartCoroutine(ReadySequence());
        else
            StartCoroutine(NotReadySequence());
    }

    IEnumerator ReadySequence()
    {
        ambiencePlayer?.FadeOut(FADE_TIME);
        musicPlayer?.FadeOut(FADE_TIME);

        confetti?.Play();
        growAndShrink?.Grow();
        sfxPlayer?.Play();

        yield return new WaitForSeconds(DELAY_BEFORE_LOAD);
        SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator NotReadySequence()
    {
        growAndShrink?.Grow();
        notReadySfx?.Play();

        yield return new WaitForSeconds(2f);
        GetComponent<Collider2D>().enabled = true;
        growAndShrink?.Shrink(new Vector3(5, 5, 5), 0.5f);
    }
}
