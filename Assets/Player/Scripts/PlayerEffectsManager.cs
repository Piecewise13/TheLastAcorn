using UnityEngine;
using UnityEngine.InputSystem;

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
