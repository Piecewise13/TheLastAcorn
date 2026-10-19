using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
public partial class PlayerMoveManager : MonoBehaviour
{
    // Lives are now a persistent, game-wide service (not a component on the player), so resolve
    // it on demand rather than caching a reference that may not exist at Awake time.
    private PlayerLifeManager lifeManager => PlayerLifeManager.Instance;

    // Ability progression is now a persistent, game-wide service (not a component on the player),
    // so resolve it on demand rather than caching a reference that may not exist at Awake time.
    private PlayerAbilityManager abilityManager => PlayerAbilityManager.Instance;

    /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerGameControls playerMovementMap;

    private PlayerEffectsManager effectsManager;

    /// <summary>
    /// Input action for player movement.
    /// </summary>
    private InputAction moveAction;

    /// <summary>
    /// Input action for attaching (climbing).
    /// </summary>
    private InputAction attachAction;

    /// <summary>
    /// Input action for gliding.
    /// </summary>
    private InputAction glideAction;

    /// <summary>
    /// Input action for jumping.
    /// </summary>
    private InputAction jumpAction;

    /// <summary>
    /// Reference to the Rigidbody2D component.
    /// </summary>
    private Rigidbody2D rb;


    /// <summary>
    /// Reference to the Animator component.
    /// </summary>
    private Animator animator;

#region Component Variables
    [Header("Components")]
    /// <summary>
    /// Reference to the player's graphic GameObject.
    /// </summary>
    [SerializeField] private GameObject graphic;

    [SerializeField] private SpriteRenderer graphicSprite;

    /// <summary>
    /// Reference to the player's Collider2D.
    /// </summary>
    [SerializeField] private Collider2D playerCollider;

    /// <summary>
    /// Reference to the stunned effect GameObject.
    /// </summary>
    [SerializeField] private GameObject stunnedEffect;

    [SerializeField] private PlayerCameraManager playerCamera;

    [Header("Collision")]
    /// <summary>
    /// LayerMask for identifying layers that cause damage on collision (e.g., trees).
    /// </summary>
    [SerializeField] private LayerMask collideDamageLayer;


    /// <summary>
    /// Horizontal velocity applied when hitting a tree while gliding.
    /// </summary>
    [SerializeField] private float collisionLaunchForce = 10f;

    [Space(20)]
    [Header("Ground Check")]
    /// <summary>
    /// Radius for ground check overlap circle.
    /// </summary>
    [SerializeField] private float groundCheckRadius = 0.2f;

    /// <summary>
    /// Transform used as the origin for ground checking.
    /// </summary>
    [SerializeField] private Transform groundCheck;

    /// <summary>
    /// LayerMask for identifying ground.
    /// </summary>
    [SerializeField] private LayerMask groundLayer;

    #endregion

    [Space(20)]
    [Header("Movement")]
    /// <summary>
    /// Speed at which the player moves.
    /// </summary>
    [SerializeField] private float groundMoveSpeed = 12.0f;
    [SerializeField] private float airMoveSpeed = 9.0f;

    [SerializeField] private ParticleSystem speedLineParticles;

    

    /// <summary>
    /// Force applied when jumping.
    /// </summary>
    [SerializeField] private float jumpForce = 5.0f;

    private float jumpHeldDuration = 0f;

    private float jumpBufferTime = 0.1f; // Adjust as needed (0.1s = ~6 frames at 60fps)
    private float jumpBufferTimer = 0f;

    private bool isJumpHeld = false;

    public static event System.Action Jumped;

    private CancellationTokenSource stunReleaseCts;


    /// <summary>
    /// Initializes input actions and sets up event handlers.
    /// </summary>
    void Awake()
    {
        // Initialize input action map
        playerMovementMap = new PlayerGameControls();

        effectsManager = GetComponent<PlayerEffectsManager>();

        // Assign movement action and enable it
        moveAction = playerMovementMap.Gameplay.Move;
        moveAction.Enable();

        // Assign attach action and subscribe to event
        attachAction = playerMovementMap.Gameplay.Attach;
        attachAction.performed += Attach;
        attachAction.canceled += Attach;
        attachAction.Enable();

        // Assign glide action and subscribe to event
        glideAction = playerMovementMap.Gameplay.Glide;
        glideAction.performed += GlideInput;
        glideAction.canceled += ctx => glideButtonReleasedSinceClimb = true;
        glideAction.canceled += GlideInput;
        glideAction.Enable();

        // Assign jump action and subscribe to event
        jumpAction = playerMovementMap.Gameplay.Jump;
        jumpAction.performed += Jump;
        jumpAction.performed += ctx => isJumpHeld = true;
        jumpAction.canceled += ctx => isJumpHeld = false;
        jumpAction.Enable();

    }

    /// <summary>
    /// Initializes references and sets up initial values.
    /// </summary>
    void Start()
    {
        // Get Animator and Rigidbody2D components
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        playerCamera = GetComponentInChildren<PlayerCameraManager>();
        // Store original graphic position for shake effect
        if (graphic != null)
            graphicOriginalLocalPos = graphic.transform.localPosition;

        // Subscribed here rather than in Awake: PlayerStateManager is a component on this same
        // GameObject, so during Awake its own Awake may not have run and Instance is still null.
        PlayerStateManager.Instance.OnLockChange += CheckPlayerLocked;
        PlayerStateManager.Instance.OnStateChanged += HandleStateChanged;

        playerCamera.OnZoomStarted += DisableMove;
        playerCamera.OnZoomEnded += EnableMove;
    }
    

    void HandleStateChanged(PlayerState from, PlayerState to)
    {
        animator.SetBool("isGliding",  to == PlayerState.Glide);
        animator.SetBool("isFalling",  to == PlayerState.Fall);
        animator.SetBool("isClimbing", to == PlayerState.Climb);
        animator.SetBool("isRunning",  false); // will be overridden by movement each frame

        // Every way out of a climb lands here, including stun, lock, owl and vine, which skip StopClimb.
        if (from == PlayerState.Climb)
            TiltGraphic(0f);
    }

    /// <summary>
    /// Handles physics-based updates and state-specific movement logic.
    /// </summary>
    private void FixedUpdate()
    {
        // Climb is the only state that passes through platforms. Deriving that from the state each
        // step means a climb cut short by a stun, lock or ride cannot leave it latched on.
        SyncClimbCollisionExclusion();

        // Locked can be entered via ChangeState (which never fires OnLockChange), so the private
        // LockPlayer that zeros gravity may never run. Enforce a fully inert body here instead of
        // trusting that path, otherwise Unity's physics keeps pulling the player down while locked.
        if (PlayerStateManager.Instance.CurrentState == PlayerState.Locked)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // These states own the body entirely; running FallingLogic would fight their own gravity.
        if (PlayerStateManager.Instance.CurrentState == PlayerState.STUNNED
        || PlayerStateManager.Instance.CurrentState == PlayerState.RidingOwl
        || PlayerStateManager.Instance.CurrentState == PlayerState.VineSwinging)
        {
            return;
        }

        // Handle climbing logic
        if (PlayerStateManager.Instance.CurrentState == PlayerState.Climb)
        {
            Climb();
            return;
        }

        // Check if player is grounded
        GroundCheck();

        FallingLogic();



        // Handle gliding logic
        if (PlayerStateManager.Instance.CurrentState == PlayerState.Glide)
        {
            Glide();
            return;
        }


        // Handle ground movement if move action is in progress
        if (moveAction.inProgress)
        {
            SideMovement();
            return;
        }

        if (PlayerStateManager.Instance.CurrentState == PlayerState.Grounded)
        {
            // Reset horizontal movement and update animation
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            animator.SetBool("isRunning", false);
        }
    }

    private float climbEndTime;




    private void FallingLogic()
    {

        // Adjust gravity scale based on player state
        if (PlayerStateManager.Instance.CurrentState == PlayerState.Grounded)
        {
            rb.gravityScale = 1.5f;
            jumpHeldDuration = 0f;
            return;
        }

        if (rb.linearVelocity.magnitude > glideSuperSpeedMin )
        {
            if (!speedLineParticles.isPlaying)
            {
                speedLineParticles.Play();
                print("playing");
            }
        } else
        {
            speedLineParticles.Stop();
        }

        if (climbTime > 0 && Time.time - climbEndTime > climbRechargeDelay)
        {
            float t = (Time.time  - climbEndTime);

            //float lerpSpeed = climbRechargeCurve.Evaluate(Mathf.InverseLerp(climbEndTime, 0f, climbTime));
            climbTime = Mathf.Lerp(climbTime, 0f, recoverSpeed * Mathf.Pow(t, 2) * Time.deltaTime);

            effectsManager.UpdateClimbFatigueColor(climbTime / maxClimbTime);
            effectsManager.UpdateClimbParticles(climbTime / maxClimbTime);
        }



        if (PlayerStateManager.Instance.CurrentState == PlayerState.Glide)
        {
            jumpHeldDuration = 0f;
            rb.gravityScale = inGust ? 0.0f : 2.8f;
            return;
        }

        if (rb.linearVelocity.y < 0)
        {
            if (isJumpHeld)
            {
                GlideInput(new InputAction.CallbackContext());
            }

            rb.gravityScale = 2.8f;
        }
        else
        {
            rb.gravityScale = 1.8f;
        }
    }
    private void SideMovement()
    {
        if (PlayerStateManager.Instance.CurrentState == PlayerState.Climb)
            return;

        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        float moveSpeed = (PlayerStateManager.Instance.CurrentState == PlayerState.Fall) ? airMoveSpeed : groundMoveSpeed;

        // Handle running animation
        animator.SetBool("isRunning", moveInput.x != 0 && PlayerStateManager.Instance.CurrentState == PlayerState.Grounded);

        // Apply horizontal velocity
        if (PlayerStateManager.Instance.CurrentState == PlayerState.Fall && ShouldApplyAirControl(moveInput.x, rb.linearVelocity.x))
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x + moveInput.x * airMoveSpeed * Time.deltaTime,
                rb.linearVelocity.y
            );
        }
        else if (PlayerStateManager.Instance.CurrentState == PlayerState.Fall)
        {
            if (Mathf.Abs(moveInput.x * airMoveSpeed) > Mathf.Abs(rb.linearVelocity.x))
            {
                rb.linearVelocity = new Vector2(
    moveInput.x * airMoveSpeed,
    rb.linearVelocity.y
);
            }

        }
        else
        {
            rb.linearVelocity = new Vector2(moveInput.x * groundMoveSpeed, rb.linearVelocity.y);
        }

        // Flip graphic if moving horizontally
        if (moveInput.x != 0)
            FlipGraphic(moveInput.x);
    }

    private bool ShouldApplyAirControl(float inputX, float velocityX)
    {
        // Only apply air control if input direction is opposite to current velocity
        return (inputX > 0) ^ (velocityX > 0);
    }

    /// <summary>
    /// True when the graphic faces right. Facing left is a 180° turn about Y; read with a tolerance because a
    /// climb tilt about Z can leave the Y angle a hair off 0 or 180.
    /// </summary>
    private bool IsGraphicFacingRight => Mathf.Abs(Mathf.DeltaAngle(graphic.transform.eulerAngles.y, 0f)) < 90f;

    private void FlipGraphic(float inputX)
    {
        float targetYRotation = inputX > 0 ? 0f : 180f;
        Vector3 rotation = graphic.transform.eulerAngles;
        rotation.y = targetYRotation;
        graphic.transform.eulerAngles = rotation;
    }

    /// <summary>
    /// Handles jump input and applies jump force.
    /// </summary>
    /// <param name="context">Input action callback context.</param>
    private void Jump(InputAction.CallbackContext context)
    {

        if (PlayerStateManager.Instance.CurrentState == PlayerState.STUNNED)
        {
            return;
        }

        //If we want to detach from the owl when jumping, we can uncomment this section

        // if (PlayerStateManager.Instance.CurrentState == PlayerState.RidingOwl)
        // {
        //     DetachFromOwl();
        //     return;
        // }
        
        // Handle tree leap when climbing
        if (PlayerStateManager.Instance.CurrentState == PlayerState.Climb)
        {
            LeapFromTree();
            return;
        }

        // Only allow jumping if grounded and not stunned
        if (PlayerStateManager.Instance.CurrentState != PlayerState.Grounded)
        {
            return;
        }

        rb.linearVelocity = Vector2.zero;
        // Apply upward force for jump
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        Jumped?.Invoke();

        // Trigger jump animation
        animator.SetTrigger("Jump");

        // Set state to falling and update animation
        PlayerStateManager.Instance.ChangeState(PlayerState.Fall);

        jumpBufferTimer = jumpBufferTime;
    }
    
   

    /// <summary>
    /// Checks if the player is grounded and updates state accordingly.
    /// </summary>
    private void GroundCheck()
    {
        // Skip ground check if jump buffer is active
        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime;
            return;
        }

        // Check for ground using overlap circle
        Collider2D col = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        bool isGrounded = col != null;

        // If grounded, update state and animations
        if (isGrounded && PlayerStateManager.Instance.CurrentState != PlayerState.Grounded)
        {
            if (PlayerStateManager.Instance.CurrentState == PlayerState.STUNNED)
            {
                stunnedEffect.SetActive(false);
            }

            ResetClimb();

            ResetGlide();

            if (col.gameObject.layer == LayerMask.NameToLayer("Terrain"))
            {
                FoxBush.TrySpawnFoxAtPlayer(transform.position);
            }


            PlayerStateManager.Instance.ChangeState(PlayerState.Grounded);

        }
        // If not grounded, set state to falling
        else if (!isGrounded && PlayerStateManager.Instance.CurrentState == PlayerState.Grounded)
        {
            PlayerStateManager.Instance.ChangeState(PlayerState.Fall);
        }
    }

    /// <summary>
    /// Handles collision events, such as hitting a tree while gliding.
    /// </summary>
    /// <param name="collision">Collision data.</param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // If gliding and hit a climbable object
        if (((1 << collision.gameObject.layer) & collideDamageLayer) != 0)
        {

            print("collision velo: " + collision.relativeVelocity.magnitude);

            // Ignore if velocity is below threshold
            if (Mathf.Abs(collision.relativeVelocity.magnitude) >= glideSuperSpeedMin)
            {
                // Get contact normal for force direction
                var contactNormal = collision.GetContact(0).normal;

                var launchDir = collision.relativeVelocity.normalized * collisionLaunchForce;

                lifeManager?.DamagePlayer(launchDir);

                return;
            }

            // Attach to the tree and start climbing
            // transform.position = collision.GetContact(0).point;
            // StartClimb();
        }
    }

    /// <summary>Stuns the player, then releases the stun after <paramref name="duration"/> seconds.</summary>
    public void StunPlayer(float duration)
    {
        // A fresh stun replaces any pending release so overlapping hits cannot end it early.
        stunReleaseCts?.Cancel();
        stunReleaseCts?.Dispose();
        stunReleaseCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        StunPlayer();
        ReleaseStunAfter(duration, stunReleaseCts.Token).Forget();
    }

    private async UniTask ReleaseStunAfter(float duration, CancellationToken cancellationToken)
    {
        bool canceled = await UniTask.WaitForSeconds(duration, cancellationToken: cancellationToken)
                                     .SuppressCancellationThrow();
        if (canceled) return;

        StopStun();
    }

    public void StunPlayer()
    {
        if (PlayerStateManager.Instance.CurrentState == PlayerState.STUNNED)
        {
            return;
        }

        DisableMove();

        PlayerStateManager.Instance.ChangeState(PlayerState.STUNNED);

        effectsManager.StartStunEffect();
    }


    public void StopStun()
    {
        stunReleaseCts?.Cancel();

        if (PlayerStateManager.Instance.CurrentState != PlayerState.STUNNED)
        {
            return;
        }

        ResetGlide();

        EnableMove();

        PlayerStateManager.Instance.ChangeState(PlayerState.Fall);

        effectsManager.EndStunEffect();

        rb.constraints = RigidbodyConstraints2D.FreezeRotation;


    }

    private void CheckPlayerLocked(bool isLocked)
    {
        
        if (isLocked)
        {
            LockPlayer();
            return;
        }
        
        UnlockPlayer();
    }

    private void LockPlayer()
    {
        DisableMove();
        rb.gravityScale = 0f;
        // Kill leftover momentum (e.g. the glide-unlock fall entry seeds downward velocity),
        // otherwise the body keeps drifting even with gravity off.
        rb.linearVelocity = Vector2.zero;
        ForceGroundedPose();
    }

    // Locking sets CurrentState = Locked directly, so OnStateChanged never fires and the animator
    // keeps whatever pose it held before the lock (gliding, climbing, falling...). Force the grounded
    // pose so a locked player always reads as grounded regardless of what it was doing.
    private void ForceGroundedPose()
    {
        animator.SetBool("isGliding", false);
        animator.SetBool("isFalling", false);
        animator.SetBool("isClimbing", false);
        animator.SetBool("isClimbMoving", false);
        animator.SetBool("isRunning", false);
    }

    private void UnlockPlayer()
    {
        EnableMove();
        rb.gravityScale = 1f;
    }
    
    /// <summary>
    /// Enables the movement input action.
    /// </summary>
    public void EnableMove()
    {
        playerMovementMap.Enable();
        moveAction.Enable();
        attachAction.Enable();
        glideAction.Enable();
        jumpAction.Enable();
    }

    /// <summary>
    /// Disables the movement input action.
    /// </summary>
    public void DisableMove()
    {
        playerMovementMap.Disable();
        moveAction.Disable();
        attachAction.Disable();
        glideAction.Disable();
        jumpAction.Disable();
    }

    private void OnDisable()
    {
        playerMovementMap.Disable();
    }

    private void OnEnable()
    {
        playerMovementMap.Enable();
    }

    private void OnDestroy()
    {
        // PlayerStateManager outlives the player, so drop our handlers or they leak.
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnLockChange -= CheckPlayerLocked;
            PlayerStateManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    #region Owl Riding

    public void AttachToOwl()
    {
        PlayerStateManager.Instance.ChangeState(PlayerState.RidingOwl) ;
        rb.gravityScale = 0f;
        animator.SetBool("isClimbMoving", false);

        moveAction.Disable();
        attachAction.Disable();
        glideAction.Disable();
        jumpAction.Disable();
    }

    public void DetachFromOwl()
    {

        // Detach the player from the owl's transform
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        PlayerStateManager.Instance.ChangeState(PlayerState.Fall);
        rb.gravityScale = 1f;
        animator.SetBool("isClimbMoving", false);

        moveAction.Enable();
        attachAction.Enable();
        glideAction.Enable();

        jumpAction.Enable();
    }

    #endregion

    #region Wind Gust Region

    public void EnterGust()
    {
        inGust = true;
    }

    public void ExitGust()
    {
        inGust = false;
    }

    #endregion

    #region Vine Swing Region

    public void StartVineSwing()
    {
        DisableMove();
        rb.gravityScale = 0f;
        playerCollider.enabled = false;
        PlayerStateManager.Instance.ChangeState(PlayerState.VineSwinging);
        //animator.SetBool("isVineSwinging", true);
    }

    public void EndVineSwing()
    {
        EnableMove();
        rb.gravityScale = 1f;
        playerCollider.enabled = true;
        PlayerStateManager.Instance.ChangeState(PlayerState.Fall);
        //animator.SetBool("isVineSwinging", false);
    }


    #endregion

    /// <summary>
    /// Gets the current player state.
    /// </summary>
    /// <returns>The current PlayerState.</returns>
    public PlayerState GetPlayerState()
    {
        return PlayerStateManager.Instance.CurrentState;
    }

}
}


