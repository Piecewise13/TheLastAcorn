using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
public partial class PlayerMoveManager : MonoBehaviour
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
    /// Maximum time allowed for climbing.
    /// </summary>
    [SerializeField] private float maxClimbTime;
    
    [SerializeField] private float baselineClimbSpeed, fastClimbSpeed;

    /// <summary>
    /// Climb direction with the stick all the way left or right, in degrees from straight up.
    /// Upward speed stays the same, so steeper angles move faster sideways.
    /// </summary>
    [Range(0f, 85f)]
    [SerializeField] private float maxClimbAngle = 60f;
    private float currentMaxClimbSpeed;
    private float currentClimbSpeed;

    [Range(0.01f, 3f)]
    [SerializeField] private float recoverSpeed = .25f;

    [SerializeField] private float climbRechargeDelay = 1f;

    /// <summary>
    /// Sideways push back onto the surface when a climb move would leave it, as a fraction of climb speed.
    /// Keeps the climb going up instead of stalling on the edge.
    /// </summary>
    [SerializeField] private float climbEdgeNudge = 0.5f;

    /// <summary>
    /// How far inside the collider's bounds the soft edge stops sideways movement, in world units.
    /// </summary>
    [SerializeField] private float climbEdgeMargin = 0.05f;

    /// <summary>
    /// Width of the soft edge zone inside the margin, in world units. Sideways input eases from full to none across it.
    /// </summary>
    [SerializeField] private float climbEdgeZone = 0.5f;

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
        if (PlayerStateManager.Instance.CurrentState == PlayerState.STUNNED)
        {
            return;
        }

        // Stop climbing if already climbing and button released
        if (PlayerStateManager.Instance.CurrentState == PlayerState.Climb && context.canceled)
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

        currentClimbSpeed = Mathf.Lerp(currentMaxClimbSpeed, 0, climbSpeedAnimationCurve.Evaluate(climbTime / maxClimbTime));

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

        // Calculate intended move location: along the middle of the trunk, or the plain soft edge climb
        // while the guide cannot read the surface.
        if (!useClimbGuide || !TryGuideClimb(moveInput, out Vector2 moveLocation))
        {
            // Sideways speed per unit of upward speed: full stick gives maxClimbAngle from straight up,
            // easing off near the edge being steered toward.
            float steer = Mathf.Tan(maxClimbAngle * Mathf.Deg2Rad) * moveInput.x * SoftEdgeFactor(overlappingColliders, moveInput.x);

            moveLocation = transform.position + (Vector3)(Vector3.right * (steer * Time.deltaTime * currentClimbSpeed) + Vector3.up * (currentClimbSpeed * Time.deltaTime));
        }
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
                Vector2 targetPos = closest.ClosestPoint(moveLocation);
                float towardSurface = targetPos.x - moveLocation.x;

                // Climb assist: the move ran off the side of the trunk. Keep the upward progress and
                // nudge back toward the middle instead of stalling on the edge.
                Vector2 assisted = new Vector2(
                    transform.position.x + Mathf.Sign(towardSurface) * climbEdgeNudge * currentClimbSpeed * Time.deltaTime,
                    moveLocation.y);

                if (Mathf.Abs(towardSurface) > 0.001f && Physics2D.OverlapPoint(assisted, climbableLayer) != null)
                {
                    transform.position = assisted;
                }
                else
                {
                    // Smoothly move towards the edge of the next collider
                    transform.position = Vector2.Lerp(transform.position, targetPos, 0.2f);
                }
            }
            else
            {
                StopClimb();
            }
        }

        animator.SetBool("isClimbMoving", moveInput != Vector2.zero);
    }



    /// <summary>
    /// Soft edge assist: how much of the sideways input to keep, 1 in the middle of the trunk easing to 0 at the
    /// margin from the edge being steered toward. Stays 1 when something climbable continues past that edge, so
    /// moving onto a branch is not slowed, and while not yet on the surface, so the approach after attaching is not.
    /// </summary>
    private float SoftEdgeFactor(Collider2D[] overlapping, float inputX)
    {
        if (Mathf.Abs(inputX) < 0.001f) return 1f;

        Vector2 position = transform.position;
        Collider2D surface = null;
        foreach (var col in overlapping)
        {
            if (col.OverlapPoint(position))
            {
                surface = col;
                break;
            }
        }
        if (surface == null) return 1f;

        float direction = Mathf.Sign(inputX);
        float edgeX = direction > 0f ? surface.bounds.max.x : surface.bounds.min.x;
        if (Physics2D.OverlapPoint(new Vector2(edgeX + direction * climbEdgeMargin, position.y), climbableLayer) != null) return 1f;

        float distance = Mathf.Abs(edgeX - position.x) - climbEdgeMargin;
        return Mathf.SmoothStep(0f, 1f, distance / Mathf.Max(climbEdgeZone, 0.001f));
    }

    /// <summary>
    /// Matches the collider's platform exclusion to the current state, every physics step.
    /// Climb is latched by state rather than by StartClimb/StopClimb because the states that
    /// interrupt a climb — stun, lock, owl, vine — never reach StopClimb.
    /// </summary>
    private void SyncClimbCollisionExclusion()
    {
        int excluded = PlayerStateManager.Instance.CurrentState == PlayerState.Climb
            ? noCollisionClimbLayer.value
            : 0;

        if (playerCollider.excludeLayers.value != excluded)
        {
            playerCollider.excludeLayers = excluded;
        }
    }

    /// <summary>
    /// Starts climbing by updating state and pinning the body in place.
    /// Platform pass-through follows from the state, see SyncClimbCollisionExclusion.
    /// </summary>
    private void StartClimb()
    {
        // Store attach velocity for reward calculation
        attachVelocity = rb.linearVelocity.magnitude;

        currentMaxClimbSpeed = (attachVelocity >= glideSuperSpeedMin) ? fastClimbSpeed : baselineClimbSpeed;

        // A new climb starts heading straight up and picks its arm afresh.
        climbHeadingAngle = 0f;
        climbGuideTargetX = float.NaN;


        PlayerStateManager.Instance.ChangeState(PlayerState.Climb);

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

        if (PlayerStateManager.Instance.CurrentState == PlayerState.Climb)
        {
            PlayerStateManager.Instance.ChangeState(PlayerState.Fall);
        }

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

    /// <summary>
    /// Sets the maximum time the player is allowed to climb before fatiguing.
    /// Used by the upgrade system.
    /// </summary>
    public void SetMaxClimbTime(float value)
    {
        maxClimbTime = value;
    }

    /// <summary>
    /// Sets the normal climb speed used by the Climb Skill upgrade.
    /// </summary>
    public void SetClimbSpeed(float value)
    {
        baselineClimbSpeed = value;
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
}
