using System.Collections;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLifeManager : MonoBehaviour
{
    private Rigidbody2D rb;

    private PlayerMove playerMove;

    private Animator animator;

    [SerializeField] private float damageTime;
    private float damageTimer;

    [SerializeField] private float damageLaunchForce = 40f;

    [SerializeField] private int maxLives = 3;
    [SerializeField] private Sprite[] lifeIcons;
    [SerializeField] private Image lifeUI;

    [Header("Feedback")]
    [SerializeField] private AudioPlayer hurtSfx;
    [SerializeField] private AudioPlayer uiSfx;

    private bool isHurt = false;

    int currentLives;

    void Start()
    {
        currentLives = maxLives;
        playerMove = GetComponent<PlayerMove>();
        rb = GetComponent<Rigidbody2D>();

        animator = GetComponent<Animator>();
    }
    public void DamagePlayer()
    {

        DamagePlayer(Vector2.up);
    }

    public void DamagePlayer(Vector2 launchDir)
    {
        currentLives--;
        lifeUI.sprite = lifeIcons[currentLives];
        hurtSfx?.Play();
        uiSfx?.Play();

        rb.linearVelocity = Vector2.zero; // Reset velocity to prevent sliding;
        rb.AddForce(launchDir, ForceMode2D.Impulse); // Adjust 10f for desired launch force

        playerMove.StunPlayer();
        isHurt = true;

        damageTimer = 0;

        if (currentLives <= 0)
            StartCoroutine(ReloadSceneAfterDelay());
    }

    void Update()
    {
        if (isHurt)
        {
            if (damageTimer > damageTime)
            {
                playerMove.StopStun();
                isHurt = false;
            }
            damageTimer += Time.deltaTime;
        }
    }

    IEnumerator ReloadSceneAfterDelay()
    {
        yield return new WaitForSeconds(0.75f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}