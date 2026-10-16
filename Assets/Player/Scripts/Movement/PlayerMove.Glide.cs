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
    /// the glide loses height more slowly. Used by <see cref="Glide"/>.
    /// </summary>
    [Range(0f, 1f)]
    [SerializeField] private float glideYDecline = 0.9f;

    [SerializeField] private float glideSuperSpeedMin;

    private bool inGust = false;

    /// <summary>
    /// Migration rate toward the glide target velocity at full speed. Used by <see cref="Glide"/> and
    /// set by the upgrade system via <see cref="SetEnergyShiftSpeed"/>.
    /// </summary>
    [SerializeField] private float energyShiftSpeed = 2f;

    /// <summary>
    /// Migration rate toward the glide target velocity at a standstill. The rate is lerped between
    /// this and <see cref="energyShiftSpeed"/> by how fast the player is already travelling, so a
    /// fast entry snaps onto the glide line and a slow one eases onto it.
    /// </summary>
    [SerializeField] private float glideMigrationMinRate = 0.6f;

/// <summary>
/// the angle (0 at down, 90 to the side) that the player will move toward when gliding.
/// </summary>
    [SerializeField] private float glideDirectionAngle = 70f;

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
    /// Starts a glide without a glide button press reaching this player, for sequences that hand
    /// control back mid-air on that press (the glide unlock). Glide only holds while descending,
    /// so a player at a standstill is pushed down at <paramref name="downwardSpeed"/> first. Does
    /// nothing unless the player is falling.
    /// </summary>
    public void StartGlide(float downwardSpeed)
    {
        if (PlayerStateManager.Instance.CurrentState != PlayerState.Fall) return;

        if (rb.linearVelocity.y >= 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -Mathf.Abs(downwardSpeed));
        }

        GlideInput(new InputAction.CallbackContext());
    }

    /// <summary>
    /// Handles gliding movement and transitions out of glide state if not falling.
    /// </summary>
    private void Glide()
    {
        // Only apply glide if falling downwards
        if (rb.linearVelocity.y < 0 || inGust)
        {
            // Angle is measured from straight down, so 0 dives and 90 is level.
            // Graphic faces right at 0 degrees and left at 180, so mirror the target's X onto whichever way the player is facing.
            float facingDirection = IsGraphicFacingRight ? 1f : -1f;
            var targetDirection = new Vector2(Mathf.Sin(glideDirectionAngle * Mathf.Deg2Rad) * facingDirection,
                -Mathf.Cos(glideDirectionAngle * Mathf.Deg2Rad));

            float maxSpeed = inGust ? maxGlideSpeedInGust : maxGlideSpeed;
            Vector2 targetVelocity = targetDirection * maxSpeed;

            // Exponential approach: never overshoots, and stays frame-rate independent. The rate
            // scales with current speed, so momentum carried into the glide buys a faster migration.
            float speedFraction = Mathf.Clamp01(rb.linearVelocity.magnitude / maxSpeed);
            float migrationRate = Mathf.Lerp(glideMigrationMinRate, energyShiftSpeed, speedFraction);
            float migrationStep = 1f - Mathf.Exp(-migrationRate * Time.fixedDeltaTime);

            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, migrationStep);
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
    /// Signed velocity, both axes. <see cref="HorizontalSpeed"/> drops the sign because the zoom ramp
    /// only cares how fast the player travels; the camera's look-ahead has to know which way, so it
    /// reads this instead.
    /// </summary>
    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;

    /// <summary>
    /// Sets how fast velocity migrates onto the glide target velocity at full speed. Used by the upgrade system.
    /// </summary>
    public void SetEnergyShiftSpeed(float value)
    {
        energyShiftSpeed = value;
    }

    #endregion

}
}
