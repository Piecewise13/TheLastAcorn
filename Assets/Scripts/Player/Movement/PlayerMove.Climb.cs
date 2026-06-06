using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerMove : MonoBehaviour
{
    

    [Header("Climb")]
    /// <summary>
    /// Distance to check for climbable objects.
    /// </summary>
    [SerializeField] private float climbCheckReach = 0.2f;

    /// <summary>
    /// LayerMask for identifying climbable objects.
    /// </summary>
    [SerializeField] private LayerMask climbableLayer;

    /// <summary>
    /// The layers that the player can climb without collision.
    /// </summary>
    [SerializeField] private LayerMask noCollisionClimbLayer;

    /// <summary>
    /// Transform used as the origin for climb checking.
    /// </summary>
    [SerializeField] private Transform climbCheckOrigin;

    /// <summary>
    /// Speed at which the player climbs.
    /// </summary>
    [SerializeField] private float climbSpeed = 5.0f;

    /// <summary>
    /// Maximum time allowed for climbing.
    /// </summary>
    [SerializeField] private float maxClimbTime;

    [Range(0.01f, 3f)]
    [SerializeField] private float recoverSpeed = .25f;

    [SerializeField] private float climbRechargeDelay = 1f;

    /// <summary>
    /// Current elapsed climb time.
    /// </summary>
    private float climbTime;
    
    private float attachVelocity;

    [SerializeField] private bool isAttachedToMoss = false;

    [SerializeField] private float mossSlipSpeed;
    private float mossSlipAmount;
    [SerializeField] private float mossDetachTime;

    [SerializeField] private AnimationCurve climbRechargeCurve;

    // Slippery surface settings: when overlapping these layers while climbing the player will accelerate downward
    [Header("Slippery")]
    [SerializeField] private LayerMask slipperyLayer;
    [SerializeField] private float slipAcceleration = 8f; // units/s^2 downward while slipping
    private float currentSlipVelocity = 0f;


    /// <summary>
    /// Original local position of the graphic for shake effect reset.
    /// </summary>
    private Vector3 graphicOriginalLocalPos;





    /// <summary>
    /// Handles attach input for starting or stopping climbing.
    /// </summary>
    /// <param name="context">Input action callback context.</param>
    private void Attach(InputAction.CallbackContext context)
    {


        print("attach input: " + context.phase);

        // Prevent attaching if stunned
        if (PlayerStateManager.Instance.CurrentState == PlayerStateManager.PlayerState.STUNNED)
        {
            return;
        }

        // Stop climbing if already climbing and button released
        if (PlayerStateManager.Instance.CurrentState == PlayerStateManager.PlayerState.Climb && context.canceled)
        {
            StopClimb();
            return;
        }

        if (context.canceled)
        {
            return;
        }

        if (climbTime > maxClimbTime)
        {
            return;
        }

        // Find all climbable colliders in reach
        Collider2D[] climbableColliders = Physics2D.OverlapCircleAll(climbCheckOrigin.position, climbCheckReach, climbableLayer);

        if (climbableColliders.Length > 0)
        {
            // Find the closest climbable collider to the player
            Collider2D closest = climbableColliders[0];
            float minDist = Vector2.Distance(transform.position, closest.ClosestPoint(transform.position));
            for (int i = 1; i < climbableColliders.Length; i++)
            {

                float dist = Vector2.Distance(transform.position, climbableColliders[i].ClosestPoint(transform.position));
                if (dist < minDist)
                {
                    closest = climbableColliders[i];
                    minDist = dist;
                }
            }

            // Smoothly move towards the closest climbable collider's edge
            Vector2 targetPos = new Vector2(closest.ClosestPoint(transform.position).x, transform.position.y);
            // Use Lerp for smooth transition
            transform.position = Vector2.Lerp(transform.position, targetPos, 0.2f);

            StartClimb();
        }
    }


    [SerializeField] private AnimationCurve climbSpeedAnimationCurve;
    [SerializeField] private float normalClimbSpeed, fastClimbSpeed;
    private float currentMaxClimbSpeed;
    private float currentClimbSpeed;


    /// <summary>
    /// Handles climbing movement and shake effect while climbing.
    /// </summary>
    private void Climb()
    {

        if (climbTime > maxClimbTime)
        {
            animator.SetTrigger("detachClimb");
            StopClimb();
            return;
        }

        climbTime += Time.deltaTime;
        effectsManager.UpdateClimbParticles(climbTime / maxClimbTime);
        effectsManager.UpdateClimbFatigueColor(climbTime / maxClimbTime);

        float climbSpeedFactor = Mathf.Lerp(currentMaxClimbSpeed, 0, climbSpeedAnimationCurve.Evaluate(climbTime / maxClimbTime));
        currentClimbSpeed = climbSpeedFactor; // Store for leap calculation

        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;

        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        // Find all climbable colliders currently overlapping
        Collider2D[] overlappingColliders = Physics2D.OverlapCircleAll(transform.position, 0.5f, climbableLayer);

        if (overlappingColliders.Length == 0)
        {
            StopClimb();
            return;
        }

        effectsManager.ApplyClimbShake(climbTime / maxClimbTime);
        effectsManager.ControllerRumble(climbTime / maxClimbTime);

        climbEndTime = Time.time;

        // Calculate intended move location
        Vector2 moveLocation = transform.position + (Vector3)(Vector3.right * moveInput.x * Time.deltaTime * climbSpeedFactor + Vector3.up * climbSpeedFactor * Time.deltaTime);
        Debug.DrawLine(transform.position, moveLocation, Color.red, 0.1f);

        // Check if moveLocation is still inside any climbable collider
        bool insideAny = false;

        isAttachedToMoss = false;
        bool isOnSlippery = false;
        bool isOnNormalClimbable = false;

        foreach (var col in overlappingColliders)
        {

            if (col.gameObject.CompareTag("Moss"))
            {
                isAttachedToMoss = true;
                break;
            }

            // detect slippery layers by layer mask
            if ((slipperyLayer.value & (1 << col.gameObject.layer)) != 0)
            {
                isOnSlippery = true;
            }
            else
            {
                // any collider not on the slippery layer counts as normal climbable
                isOnNormalClimbable = true;
            }

            if (col.OverlapPoint(moveLocation))
            {
                insideAny = true;
            }
        }

        // If overlapping both slippery and normal climbable, treat as normal climb
        if (isOnNormalClimbable)
        {
            isOnSlippery = false;
        }


        if (isAttachedToMoss)
        {
            mossSlipAmount += Time.deltaTime * mossSlipSpeed;

            moveLocation -= Vector2.up * mossSlipAmount * Time.deltaTime;


            transform.position = moveLocation;
            return;
        }

        // If on slippery surface while climbing, accelerate downward over time
        if (isOnSlippery)
        {
            // integrate slip velocity (v = v0 + a * dt)
            currentSlipVelocity += slipAcceleration * Time.deltaTime;
            // apply slip displacement (dy = v * dt)
            moveLocation += Vector2.down * currentSlipVelocity * Time.deltaTime;
        }
        else
        {
            // reset slip velocity when not on slippery
            currentSlipVelocity = 0f;
        }


        if (insideAny)
        {
            transform.position = moveLocation;
        }
        else
        {
            // Try to find the closest climbable collider to transition to
            Collider2D closest = null;
            float minDist = float.MaxValue;
            foreach (var col in overlappingColliders)
            {
                Vector2 closestPoint = col.ClosestPoint(moveLocation);
                float dist = Vector2.Distance(moveLocation, closestPoint);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = col;
                }
            }

            if (closest != null && minDist < 0.5f)
            {
                // Smoothly move towards the edge of the next collider
                Vector2 targetPos = closest.ClosestPoint(moveLocation);
                transform.position = Vector2.Lerp(transform.position, targetPos, 0.2f);
            }
            else
            {
                StopClimb();
            }
        }

        animator.SetBool("isClimbMoving", moveInput != Vector2.zero);
    }



    /// <summary>
    /// Starts climbing by updating state and disabling collider.
    /// </summary>
    private void StartClimb()
    {
        // Store attach velocity for reward calculation
        attachVelocity = rb.linearVelocity.magnitude;

        currentMaxClimbSpeed = (attachVelocity >= glideSuperSpeedMin) ? fastClimbSpeed : normalClimbSpeed;


        PlayerStateManager.Instance.ChangeState(PlayerStateManager.PlayerState.Climb);

        playerCollider.excludeLayers = noCollisionClimbLayer;

        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;

        animator.SetBool("isClimbing", true);
        animator.SetBool("isGliding", false);
    }

    /// <summary>
    /// Stops climbing and resets relevant properties.
    /// </summary>
    public void StopClimb()
    {
        animator.SetBool("isClimbing", false);

        if (PlayerStateManager.Instance.CurrentState == PlayerStateManager.PlayerState.Climb)
        {
            PlayerStateManager.Instance.ChangeState(PlayerStateManager.PlayerState.Fall);
        }

        playerCollider.excludeLayers = 0;

        //Moss reset
        mossSlipAmount = 0;
        isAttachedToMoss = false;

        rb.gravityScale = 1;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        effectsManager.StopControllerRumble();

        // Reset graphic position when climb ends
        if (graphic != null)
            graphic.transform.localPosition = graphicOriginalLocalPos;

        // Require button release before next glide
        glideButtonReleasedSinceClimb = false;

        // reset slip velocity when climb ends
        currentSlipVelocity = 0f;
    }

    private void ResetClimb()
    {

        // Reset climb time and particle emission
        climbTime = 0;

        effectsManager.UpdateClimbFatigueColor(0);
        effectsManager.UpdateClimbParticles(0);

        // ensure slip velocity is cleared when resetting climb
        currentSlipVelocity = 0f;

    }

}
