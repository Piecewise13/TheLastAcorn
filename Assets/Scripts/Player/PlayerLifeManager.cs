using System.Collections;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLifeManager : MonoBehaviour
{
    private Rigidbody2D rb;

    private PlayerMove playerMove;

    private Animator animator;

    [Header("Player Components")]

    [SerializeField] private SpriteRenderer playerSprite;


    [Header("Damage Settings")]

    [SerializeField] private float damageTime;
    private float damageTimer;

    [SerializeField] private float immuneTime;

    private float immuneTimer;

    private IEnumerator immuneCoroutine;

    [SerializeField] private float damageLaunchForce = 40f;

    [SerializeField] private int maxLives = 3;

    [Header("UI Settings")]
    [SerializeField] private Sprite[] lifeIcons;
    [SerializeField] private Image lifeUI;

    [Header("Feedback")]
    [SerializeField] private AudioPlayer hurtSfx;
    [SerializeField] private AudioPlayer uiSfx;

    private bool isHurt = false;

    private bool isImmune = false;

    int currentLives;

    private Vector2 lastGroundLocation;

    void Start()
    {
        // DebugSettings.Instance.DisablePersistence is read-only and cannot be set here.
        currentLives = maxLives;
        playerMove = GetComponent<PlayerMove>();
        rb = GetComponent<Rigidbody2D>();

        animator = GetComponent<Animator>();
    }
    public void DamagePlayer()
    {

        DamagePlayer(Vector2.up * damageLaunchForce);
    }

    public void StunPlayer() // stuns player without damaging them
    {
        animator.SetTrigger("Hurt");
        playerMove.StunPlayer();
        isHurt = true;
        damageTimer = 0;
    }

    public void DamagePlayer(Vector2 launchDir)
    {
        if (isImmune)
        {
            return;
        }

        currentLives--;
        lifeUI.sprite = lifeIcons[currentLives];
        hurtSfx?.Play();
        uiSfx?.Play();

        rb.linearVelocity = Vector2.zero; // Reset velocity to prevent sliding;
        rb.AddForce(launchDir, ForceMode2D.Impulse); // Adjust 10f for desired launch force

        playerMove.StunPlayer();
        isHurt = true;
        damageTimer = 0;

        isImmune = true;
        immuneCoroutine = MakeImmune(0.2f, 0.25f, 1f);
        StartCoroutine(immuneCoroutine);
        immuneTimer = 0f;



        if (currentLives <= 0)
            StartCoroutine(RespawnPlayerAfterDelay());
    }

    public void DamagePlayerAndRelocate(Vector2 respawnPos)
    {

        currentLives--;
        lifeUI.sprite = lifeIcons[currentLives];
        hurtSfx?.Play();
        uiSfx?.Play();


        if (currentLives <= 0)
        {
            StartCoroutine(RespawnPlayerAfterDelay());
        }
        else
        {

            // Respawn at last ground location
            rb.linearVelocity = Vector2.zero; // Reset velocity to prevent sliding
            transform.position = respawnPos;
        }
    }

    void Update()
    {

        if (playerMove.GetPlayerState() == PlayerState.Grounded)
        {
            lastGroundLocation = transform.position; // Update last ground location when grounded
        }

        if (isHurt)
        {
            if (damageTimer > damageTime)
            {
                playerMove.StopStun();
                isHurt = false;
            }
            damageTimer += Time.deltaTime;
        }

        if (isImmune)
        {
            if (immuneTimer > immuneTime)
            {
                isImmune = false;
                StopCoroutine(immuneCoroutine);
                Color c = playerSprite.color;
                c.a = 1f;
                playerSprite.color = c;
            }
            immuneTimer += Time.deltaTime;
        }
    }

    IEnumerator MakeImmune(float alphaTime, float targetAlphaMin, float targetAlphaMax)
    {
        float originalAlpha = targetAlphaMax;
        float elapsed = 0f;

        while (true)
        {
            // Fade out
            elapsed = 0f;
            while (elapsed < alphaTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / alphaTime;
                float currentAlpha = Mathf.Lerp(originalAlpha, targetAlphaMin, t);
                if (playerSprite != null)
                {
                    Color c = playerSprite.color;
                    c.a = currentAlpha;
                    playerSprite.color = c;
                }
                yield return null;
            }

            // Fade in
            elapsed = 0f;
            while (elapsed < alphaTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / alphaTime;
                float currentAlpha = Mathf.Lerp(originalAlpha, targetAlphaMax, t);
                if (playerSprite != null)
                {
                    Color c = playerSprite.color;
                    c.a = currentAlpha;
                    playerSprite.color = c;
                }
                yield return null;
            }
        }
    }

    IEnumerator RespawnPlayerAfterDelay()
    {
        yield return new WaitForSeconds(0.75f);

        // Reset current scene acorns and adjust total score
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetCurrentSceneScore();
        }
        
        SaveLoadManager.ResetCurrentSceneAcorns();

        CheckpointManager.Instance.RespawnPlayer(gameObject);
        currentLives = maxLives;
        lifeUI.sprite = lifeIcons[currentLives];
    }

    IEnumerator ReloadSceneAfterDelay()
    {
        yield return new WaitForSeconds(0.75f);

        // Reset current scene acorns and adjust total score
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetCurrentSceneScore();
        }
        SaveLoadManager.ResetCurrentSceneAcorns();

        // Reload the scene to respawn acorns
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}