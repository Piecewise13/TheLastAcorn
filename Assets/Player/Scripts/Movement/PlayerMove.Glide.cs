using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerMove : MonoBehaviour
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
            if (PlayerStateManager.Instance.CurrentState == PlayerStateManager.PlayerState.Glide)
            {

                animator.SetBool("isGliding", false);
                PlayerStateManager.Instance.ChangeState(PlayerStateManager.PlayerState.Fall);
            }
            return;
        }

        // Only allow gliding if falling and not stunned
        if (PlayerStateManager.Instance.CurrentState != PlayerStateManager.PlayerState.Fall
            || PlayerStateManager.Instance.CurrentState == PlayerStateManager.PlayerState.STUNNED)
        {
            return;
        }


        initialGlideSpeed = defaultGlideSpeed;

        // Enter glide state and update animation
        PlayerStateManager.Instance.ChangeState(PlayerStateManager.PlayerState.Glide);
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
            // Calculate glide speed based on downward velocity
            //float glideX = Mathf.Max(initialGlideSpeed, Mathf.Abs(rb.linearVelocity.y));

            // Increase glide speed multiplier over time
            //glideSpeedMultiplier += Time.deltaTime / flightMultiper;

            // Determine direction based on graphic rotation
            float direction = graphic.transform.eulerAngles.y == 0 ? 1f : -1f;

            float glideSpeedCap = inGust ? maxGlideSpeedInGust : maxGlideSpeed;

            float yDecline = inGust ? 0.99f : 0.9f;


            // rb.linearVelocity = new Vector2(Mathf.Clamp(Mathf.Lerp(Mathf.Abs(rb.linearVelocity.x), glideSpeedCap, Time.deltaTime * flightMultiper), 0f, glideSpeedCap) * direction, rb.linearVelocity.y * 0.90f);
            rb.linearVelocity = new Vector2(Mathf.Lerp(Mathf.Abs(rb.linearVelocity.x), glideSpeedCap, Time.deltaTime * flightMultiper) * direction, rb.linearVelocity.y * yDecline);
            //            print(rb.linearVelocity.x);
        }
        else
        {
            // Exit glide state if not falling
            PlayerStateManager.Instance.ChangeState(PlayerStateManager.PlayerState.Fall);
            animator.SetBool("isGliding", false);
            animator.SetBool("isFalling", true);
        }
    }


    void ResetGlide()
    {

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
