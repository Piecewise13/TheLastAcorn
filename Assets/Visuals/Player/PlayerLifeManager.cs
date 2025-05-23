using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLifeManager : MonoBehaviour
{

    [SerializeField] private int maxLives = 3;
    private int currentLives;

    [SerializeField] private Sprite[] lifeIcons;

    [SerializeField] private Image lifeUI;

    void Start()
    {
        currentLives = maxLives;
    }

    public void DamagePlayer()
    {
        currentLives--;
        lifeUI.sprite = lifeIcons[currentLives];
        if (currentLives <= 0)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
