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

            // Calculate glide speed based on downward velocity
            //float glideX = Mathf.Max(initialGlideSpeed, Mathf.Abs(rb.linearVelocity.y));

            // Increase glide speed multiplier over time
            //glideSpeedMultiplier += Time.deltaTime / flightMultiper;

            // Determine direction based on graphic rotation
            float graphicY = graphic.transform.eulerAngles.y;
            float direction = graphicY == 0 ? 1f : -1f;

            float glideSpeedCap = inGust ? maxGlideSpeedInGust : maxGlideSpeed;

            float yDecline = inGust ? 0.99f : 0.9f;


            // rb.linearVelocity = new Vector2(Mathf.Clamp(Mathf.Lerp(Mathf.Abs(rb.linearVelocity.x), glideSpeedCap, Time.deltaTime * flightMultiper), 0f, glideSpeedCap) * direction, rb.linearVelocity.y * 0.90f);
            rb.linearVelocity = new Vector2(Mathf.Lerp(Mathf.Abs(rb.linearVelocity.x), glideSpeedCap, Time.deltaTime * flightMultiper) * direction, rb.linearVelocity.y * yDecline);
            LogGlideDebug(velocityBefore, rb.linearVelocity, graphicY, direction);
            //            print(rb.linearVelocity.x);
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

    #endregion

}
}
