using UnityEngine.UI;
using UnityEngine;

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
            // Handle player death
            Debug.Log("Player is dead");
        }
    }
}
