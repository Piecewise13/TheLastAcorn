using System.Collections;
using TMPro;
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
    [SerializeField] private GameObject notReadyMessage;

    [SerializeField] private TMP_Text count;

    [Header("Fade-Out Sources")] 
    [SerializeField] private AudioSceneManager audioManager;
    
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
        confetti?.Play();
        growAndShrink?.Grow();
        sfxPlayer?.Play();
        audioManager?.FadeOutAudio();

        yield return new WaitForSeconds(DELAY_BEFORE_LOAD);
        SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator NotReadySequence()
    {
        count.text = (ScoreManager.Instance.GetMaxScore() - ScoreManager.Instance.GetScore()) + "";
        notReadyMessage?.SetActive(true);
        growAndShrink?.Grow();
        notReadySfx?.Play();

        yield return new WaitForSeconds(2f);
        GetComponent<Collider2D>().enabled = true;
        notReadyMessage?.SetActive(false);
        growAndShrink?.Shrink(new Vector3(5, 5, 5), 0.5f);
    }
}
