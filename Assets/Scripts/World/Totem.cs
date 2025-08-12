using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Totem : MonoBehaviour
{


    [Header("Scene")]
    [SerializeField] private string nextSceneName = "NextScene";

    [Header("Score")]
    [SerializeField] private int targetScore = 5;

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

        if (ScoreManager.Instance.CurrentScore >= targetScore)
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

        //Since the player is interacting with the totem, we assume they are moving forward in the game.
        SceneInitializer.SetLevelDirection(true);

        //Scene management
        SaveLoadManager.SaveLevelData(SceneManager.GetActiveScene().name);
        SaveLoadManager.SaveCurrentLevelName(nextSceneName);
        SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator NotReadySequence()
    {
        count.text = (targetScore) + "";
        notReadyMessage?.SetActive(true);
        growAndShrink?.Grow();
        notReadySfx?.Play();

        yield return new WaitForSeconds(2f);
        GetComponent<Collider2D>().enabled = true;
        notReadyMessage?.SetActive(false);
        growAndShrink?.Shrink(new Vector3(5, 5, 5), 0.5f);
    }
}
