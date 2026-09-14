using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
public class PlayerEffectsManager : MonoBehaviour
{

        // --- Variables copied from PlayerMove.cs ---
    private Rigidbody2D rb;
    private Animator animator;
    [SerializeField] private GameObject graphic;
    [SerializeField] private SpriteRenderer graphicSprite;
    [SerializeField] private GameObject stunnedEffect;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private ParticleSystem climbParticle;
    [SerializeField] private float climbParticleRateOverTime = 20f;
    [SerializeField] private Color climbFatigueColor;
    [SerializeField] private float maxShakeIntensity = 0.2f;
    private Vector3 graphicOriginalLocalPos;

    [Header("Damage")]
    [SerializeField] private AudioPlayer hurtSfx;

    [Tooltip("Seconds per fade step of the immunity flicker.")]
    [SerializeField] private float immunityFlashStep = 0.2f;

    [SerializeField] private float immunityFlashMinAlpha = 0.25f;

    private CancellationTokenSource immunityFlashCts;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        graphicOriginalLocalPos = graphic.transform.localPosition;
    }

    // Update is called once per frame
    void Update()
    {

    }
    
    public void StartStunEffect()
    {
        stunnedEffect.SetActive(true);
        animator.SetBool("isStunned", true);
        animator.SetBool("isFalling", false);
        animator.SetBool("isGliding", false);
        animator.SetBool("isClimbing", false);
        animator.SetBool("isClimbMoving", false);

    }

    public void EndStunEffect()
    {

        stunnedEffect.SetActive(false);
        animator.SetBool("isStunned", false);
        animator.SetBool("isFalling", false);
        animator.SetBool("isGliding", false);
        animator.SetBool("isClimbing", false);
        animator.SetBool("isClimbMoving", false);


    }

    /// <summary>
    /// The player-body half of taking a hit: hurt animation, knockback impulse and the hurt sound.
    /// PlayerLifeManager owns whether damage happens; this owns what it looks like.
    /// </summary>
    public void PlayDamageFeedback(Vector2 launchDir)
    {
        animator.SetTrigger("Hurt");

        rb.linearVelocity = Vector2.zero; // reset first so the impulse is not fighting old momentum
        rb.AddForce(launchDir, ForceMode2D.Impulse);

        hurtSfx?.Play();
    }

    /// <summary>Starts the alpha flicker that marks the post-hit immunity window.</summary>
    public void StartImmunityFlash()
    {
        StopImmunityFlash();
        immunityFlashCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        ImmunityFlash(immunityFlashCts.Token).Forget();
    }

    /// <summary>Stops the immunity flicker and restores full opacity.</summary>
    public void StopImmunityFlash()
    {
        immunityFlashCts?.Cancel();
        immunityFlashCts?.Dispose();
        immunityFlashCts = null;

        SetGraphicAlpha(1f);
    }

    private async UniTask ImmunityFlash(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await FadeGraphicAlpha(1f, immunityFlashMinAlpha, cancellationToken);
            await FadeGraphicAlpha(immunityFlashMinAlpha, 1f, cancellationToken);
        }
    }

    private async UniTask FadeGraphicAlpha(float from, float to, CancellationToken cancellationToken)
    {
        float elapsed = 0f;
        while (elapsed < immunityFlashStep)
        {
            elapsed += Time.deltaTime;
            SetGraphicAlpha(Mathf.Lerp(from, to, elapsed / immunityFlashStep));
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    private void SetGraphicAlpha(float alpha)
    {
        if (graphicSprite == null) return;

        Color c = graphicSprite.color;
        c.a = alpha;
        graphicSprite.color = c;
    }

        public void UpdateClimbParticles(float t)
    {
        var emission = climbParticle.emission;
        emission.rateOverTime = Mathf.Lerp(0, climbParticleRateOverTime, t);
    }

    public void UpdateClimbFatigueColor(float t)
    {
        graphicSprite.color = Color.Lerp(Color.white, climbFatigueColor, t);
    }

    public void ApplyClimbShake(float t)
    {
        if (graphic == null) return;

        float shakeRatio = Mathf.Clamp01(t);
        float shakeAmount = maxShakeIntensity * shakeRatio;
        Vector3 shakeOffset = new Vector3(
            Random.Range(-shakeAmount, shakeAmount),
            Random.Range(-shakeAmount, shakeAmount),
            0f
        );
        graphic.transform.localPosition = graphicOriginalLocalPos + shakeOffset;
    }

    public void EndClimbShake()
    {
        var emission = climbParticle.emission;
        emission.rateOverTime = 0;

        graphicSprite.color = Color.white;
    }

    public void ControllerRumble(float t)
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float intensity = Mathf.Clamp01(t);
            gamepad.SetMotorSpeeds(intensity, intensity);
        }
    }

    public void StopControllerRumble()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(0, 0);
        }
    }
}
}
