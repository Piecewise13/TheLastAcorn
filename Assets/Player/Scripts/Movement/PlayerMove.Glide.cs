using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
public partial class PlayerMoveManager : MonoBehaviour
{
      
    [Header("Glide")]

    /// <summary>
    /// Initial horizontal speed when starting to glide.
    /// </summary>
    [SerializeField] private float defaultGlideSpeed = 10f;
    private float initialGlideSpeed;

    [SerializeField] private float maxGlideSpeed = 35f;
    [SerializeField] private float maxGlideSpeedInGust = 50f;

    /// <summary>
    /// Fraction of downward speed kept each frame while gliding outside a gust. Closer to 1 means
    /// the glide loses height more slowly. Used by <see cref="Glide"/> and set by the upgrade
    /// system via <see cref="SetGlideYDecline"/>.
    /// </summary>
    [Range(0f, 1f)]
    [SerializeField] private float glideYDecline = 0.9f;

    [SerializeField] private float glideSuperSpeedMin;


    private bool inGust = false;

    [SerializeField] private float flightMultiper = 2f;

    private bool glideButtonReleasedSinceClimb = true;

    private const float GlideDebugInterval = 0.25f;

    private float nextGlideDebugLogTime;
    private float lastGlideDebugDirection;
    private float lastGlideDebugVelocitySign;


    // Update is called once per frame
    void Update()
    {
        
    }


    #region Glide Region

    /// <summary>
    /// Handles glide input and toggles gliding state.
    /// </summary>
    /// <param name="context">Input action callback context.</param>
    private void GlideInput(InputAction.CallbackContext context)
    {

        if(!abilityManager.IsAbilityUnlocked(PlayerAbilityManager.Abilities.Glide))
        {
            return;
        }

        if (context.canceled)
        {
            if (PlayerStateManager.Instance.CurrentState == PlayerState.Glide)
            {

                animator.SetBool("isGliding", false);
                PlayerStateManager.Instance.ChangeState(PlayerState.Fall);
            }
            return;
        }

        // Only allow gliding if falling and not stunned
        if (PlayerStateManager.Instance.CurrentState != PlayerState.Fall
            || PlayerStateManager.Instance.CurrentState == PlayerState.STUNNED)
        {
            return;
        }


        initialGlideSpeed = defaultGlideSpeed;

        // Enter glide state and update animation
        PlayerStateManager.Instance.ChangeState(PlayerState.Glide);
        animator.SetBool("isGliding", true);
    }

    /// <summary>
    /// Handles gliding movement and transitions out of glide state if not falling.
    /// </summary>
    private void Glide()
    {
        // Only apply glide if falling downwards
        if (rb.linearVelocity.y < 0 || inGust)
        {
            Vector2 velocityBefore = rb.linearVelocity;
            
            // Determine direction based on graphic rotation
            float graphicY = graphic.transform.eulerAngles.y;
            float direction = graphicY == 0 ? 1f : -1f;

            float glideSpeedCap = inGust ? maxGlideSpeedInGust : maxGlideSpeed;

            float yDecline = inGust ? 0.99f : glideYDecline;

            rb.linearVelocity = new Vector2(Mathf.Lerp(Mathf.Abs(rb.linearVelocity.x), glideSpeedCap, Time.deltaTime * flightMultiper) * direction, rb.linearVelocity.y * yDecline);
            LogGlideDebug(velocityBefore, rb.linearVelocity, graphicY, direction);
        }
        else
        {
            // Exit glide state if not falling
            PlayerStateManager.Instance.ChangeState(PlayerState.Fall);
            animator.SetBool("isGliding", false);
            animator.SetBool("isFalling", true);
        }
    }


    void ResetGlide()
    {

    }

    private void LogGlideDebug(Vector2 velocityBefore, Vector2 velocityAfter, float graphicY, float direction)
    {
        float velocitySign = Mathf.Sign(velocityAfter.x);
        bool directionChanged = lastGlideDebugDirection != 0f && !Mathf.Approximately(lastGlideDebugDirection, direction);
        bool velocitySignChanged = lastGlideDebugVelocitySign != 0f && !Mathf.Approximately(lastGlideDebugVelocitySign, velocitySign);
        bool shouldLog = Time.time >= nextGlideDebugLogTime || directionChanged || velocitySignChanged;
        if (!shouldLog) return;

        nextGlideDebugLogTime = Time.time + GlideDebugInterval;
        lastGlideDebugDirection = direction;
        lastGlideDebugVelocitySign = velocitySign;

        Vector3 graphicsLocal = graphic != null ? graphic.transform.localPosition : Vector3.zero;
        Debug.Log($"[GlideDebug] t={Time.time:F2} state={PlayerStateManager.Instance.CurrentState} " +
                  $"playerPos={transform.position} graphicsLocal={graphicsLocal} graphicY={graphicY:F3} " +
                  $"dir={direction:F0} dirFlip={directionChanged} velSignFlip={velocitySignChanged} " +
                  $"velBefore={velocityBefore} velAfter={velocityAfter} inGust={inGust}", this);
    }

    /// <summary>
    /// Sets the maximum horizontal speed the player can reach while gliding.
    /// Used by the upgrade system.
    /// </summary>
    public void SetMaxGlideSpeed(float value)
    {
        maxGlideSpeed = value;
    }

    /// <summary>
    /// The top horizontal glide speed at the player's current upgrade level. The camera's velocity
    /// zoom normalizes against this rather than an absolute ceiling, so its ramp keeps the same
    /// shape at every level and only the size it lands on changes.
    /// </summary>
    public float MaxGlideSpeed => maxGlideSpeed;

    /// <summary>
    /// Horizontal speed, ignoring sign and ignoring descent. This is the quantity the glide caps
    /// (see <see cref="Glide"/>), so it is also what the camera's velocity zoom reads — magnitude
    /// would zoom out hard on a vertical drop where the player is barely travelling.
    /// </summary>
    public float HorizontalSpeed => rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;

    /// <summary>
    /// Sets the fraction of downward speed kept each frame while gliding outside a gust. Higher is
    /// a flatter, longer glide. Used by the upgrade system; the in-gust decline is authored and
    /// left alone.
    /// </summary>
    public void SetGlideYDecline(float value)
    {
        glideYDecline = Mathf.Clamp01(value);
    }

    #endregion

}
}
