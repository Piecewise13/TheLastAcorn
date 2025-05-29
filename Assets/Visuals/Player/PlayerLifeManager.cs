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
    
    private int currentLives;

    void Start() { currentLives = maxLives; }

    public void DamagePlayer()
    {
        currentLives--;
        lifeUI.sprite = lifeIcons[currentLives];
        hurtSfx?.Play();
        if (currentLives <= 0)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
