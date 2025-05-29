using System.Collections;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLifeManager : MonoBehaviour
{
    [SerializeField] private int maxLives = 3;
    [SerializeField] private Sprite[] lifeIcons;
    [SerializeField] private Image lifeUI;

    [Header("Feedback")]
    [SerializeField] private AudioPlayer hurtSfx;
    [SerializeField] private AudioPlayer uiSfx;

    int currentLives;

    void Start() { currentLives = maxLives; }

    public void DamagePlayer()
    {
        currentLives--;
        lifeUI.sprite = lifeIcons[currentLives];
        hurtSfx?.Play();
        uiSfx?.Play();

        if (currentLives <= 0)
            StartCoroutine(ReloadSceneAfterDelay());
    }

    IEnumerator ReloadSceneAfterDelay()
    {
        yield return new WaitForSeconds(0.75f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}