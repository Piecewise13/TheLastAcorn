using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{

    /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerKeyboardControls playerMovementMap;

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

    [Space(20)]
    [Header("Movement")]
    /// <summary>
    /// Speed at which the player moves.
    /// </summary>
    [SerializeField] private float groundMoveSpeed = 12.0f;
    [SerializeField] private float airMoveSpeed = 9.0f;

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

    /// <summary>
    /// Current elapsed climb time.
    /// </summary>
    private float climbTime;

    [SerializeField] private bool canClimb = true;

    /// <summary>
    /// Maximum velocity for auto-attach to climbable objects.
    /// </summary>
    [SerializeField] private float autoAttachMaxVelo = 10f;

    /// <summary>
    /// Maximum intensity of the shake effect while climbing.
    /// </summary>
    [SerializeField] private float maxShakeIntensity = 0.2f;

    /// <summary>
    /// Reference to the climb particle system.
    ///     </summary>

    [SerializeField] private ParticleSystem climbParticle;

    /// <summary>
    /// Maximum rate over time for the climb particle emission.
    /// </summary>
    [SerializeField] private float climbParticleRateOverTime = 20f;

    [SerializeField] private Color climbFatigueColor;

    /// <summary>
    /// Original local position of the graphic for shake effect reset.
    /// </summary>
    private Vector3 graphicOriginalLocalPos;

    [Header("Glide")]
    /// <summary>
    /// Maximum horizontal speed while gliding.
    /// </summary>
    [SerializeField] private float maxGlideSpeed = 10f;

    /// <summary>
    /// Initial horizontal speed when starting to glide.
    /// </summary>
    [SerializeField] private float initialGlideSpeed = 5f;

    /// <summary>
    /// Multiplier for glide speed, increases over time.
    /// </summary>
    private float glideSpeedMultiplier = 1f;

    /// <summary>
    /// Horizontal velocity applied when hitting a tree while gliding.
    /// </summary>
    [SerializeField] private float glideTreeHitXVelo = 10f;

    /// <summary>
    /// Vertical velocity applied when hitting a tree while gliding.
    /// </summary>
    [SerializeField] private float glideTreeHitYVelo = 5f;

    /// <summary>
    /// Minimum velocity threshold to trigger a glide hit.
    /// </summary>
    [SerializeField] private float glideHitVelocityThreshold = 5f;

    private bool glideButtonReleasedSinceClimb = true;

    /// <summary>
    /// Force applied when jumping.
    /// </summary>
    [SerializeField] private float jumpForce = 5.0f;

    private float jumpBufferTime = 0.1f; // Adjust as needed (0.1s = ~6 frames at 60fps)
    private float jumpBufferTimer = 0f;

    private bool isJumpHeld = false;

    /// <summary>
    /// Current state of the player.
    /// </summary>
    [SerializeField] private PlayerState currentState = PlayerState.Grounded;

    /// <summary>
    /// Initializes input actions and sets up event handlers.
    /// </summary>
    void Awake()
    {
        // Initialize input action map
        playerMovementMap = new PlayerKeyboardControls();

        // Assign movement action and enable it
        moveAction = playerMovementMap.Keyboard.Move;
        EnableMove();

        // Assign attach action and subscribe to event
        attachAction = playerMovementMap.Keyboard.Attach;
        attachAction.performed += Attach;
        attachAction.canceled += Attach;
        attachAction.Enable();

        // Assign glide action and subscribe to event
        glideAction = playerMovementMap.Keyboard.Glide;
        glideAction.performed += GlideInput;
        glideAction.canceled += ctx => glideButtonReleasedSinceClimb = true;
        glideAction.canceled += GlideInput;
        glideAction.Enable();

        // Assign jump action and subscribe to event
        jumpAction = playerMovementMap.Keyboard.Jump;
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
        // Store original graphic position for shake effect
        if (graphic != null)
            graphicOriginalLocalPos = graphic.transform.localPosition;
    }

    /// <summary>
    /// Handles per-frame logic, such as ground checking when stunned.
    /// </summary>
    private void Update()
    {
        // Only perform ground check if stunned
        if (currentState == PlayerState.STUNNED)
        {
            GroundCheck();
        }

    }

    /// <summary>
    /// Handles physics-based updates and state-specific movement logic.
    /// </summary>
    private void FixedUpdate()
    {
        // Prevent movement if stunned
        if (currentState == PlayerState.STUNNED)
        {
            return;
        }

        // Handle climbing logic
        if (currentState == PlayerState.Climb)
        {
            Climb();
            return;
        }

        // Check if player is grounded
        GroundCheck();

        FallingLogic();

        // Handle gliding logic
        if (currentState == PlayerState.Glide)
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

        if (currentState == PlayerState.Grounded)
        {
            // Reset horizontal movement and update animation
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            animator.SetBool("isRunning", false);
        }
    }

    private void FallingLogic()
    {
        // Adjust gravity scale based on player state
        if (currentState == PlayerState.Grounded)
        {
            rb.gravityScale = 1.5f;
            return;
        }

        if (climbTime > 0)
        {

            climbTime -= Time.deltaTime;
            UpdateClimbFatigueColor();
            UpdateClimbParticles();
        }


        if (currentState == PlayerState.Glide)
        {
            return;
        }

        if (rb.linearVelocity.y < 0)
        {
            if (isJumpHeld && glideButtonReleasedSinceClimb)
            {
                GlideInput(new InputAction.CallbackContext());
            }

            rb.gravityScale = 2.8f;
        }
    }
    private void SideMovement()
    {
        if (currentState == PlayerState.Climb)
            return;

        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        float moveSpeed = (currentState == PlayerState.Fall) ? airMoveSpeed : groundMoveSpeed;

        // Handle running animation
        animator.SetBool("isRunning", moveInput.x != 0 && currentState == PlayerState.Grounded);

        // Apply horizontal velocity
        if (currentState == PlayerState.Fall && ShouldApplyAirControl(moveInput.x, rb.linearVelocity.x))
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x + moveInput.x * moveSpeed * Time.deltaTime,
                rb.linearVelocity.y
            );
        }
        else
        {
            rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
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

        if (currentState == PlayerState.STUNNED)
        {
            return;
        }

        if (currentState == PlayerState.Fall && isJumpHeld)
        {
            print("jump held");
            GlideInput(new InputAction.CallbackContext());
            return;
        }

        // Only allow jumping if grounded and not stunned
        if (currentState != PlayerState.Grounded)
        {
            return;
        }

        // Apply upward force for jump
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        // Trigger jump animation
        animator.SetTrigger("Jump");

        // Set state to falling and update animation
        currentState = PlayerState.Fall;
        animator.SetBool("isFalling", true);

        jumpBufferTimer = jumpBufferTime;
    }

    /// <summary>
    /// Handles glide input and toggles gliding state.
    /// </summary>
    /// <param name="context">Input action callback context.</param>
    private void GlideInput(InputAction.CallbackContext context)
    {

        if(context.canceled)
        {
            if(currentState == PlayerState.Glide)
            {
                print("Already glidig");
                animator.SetBool("isGliding", false);
                currentState = PlayerState.Fall;
            }
            return;
        }

        // Only allow gliding if falling and not stunned
        if (currentState != PlayerState.Fall || currentState == PlayerState.STUNNED)
        {
            return;
        }

        if (!glideButtonReleasedSinceClimb) {
            return;
        }


        Collider2D climbableCollider = Physics2D.OverlapCircle(climbCheckOrigin.position, climbCheckReach, climbableLayer);
        if (climbableCollider != null)
        {
            return;
        }

        // Reset glide speed multiplier
        glideSpeedMultiplier = 1f;

        // Enter glide state and update animation
        currentState = PlayerState.Glide;
        animator.SetBool("isGliding", true);
    }

    /// <summary>
    /// Handles gliding movement and transitions out of glide state if not falling.
    /// </summary>
    private void Glide()
    {
        // Only apply glide if falling downwards
        if (rb.linearVelocity.y < 0)
        {
            // Calculate glide speed based on downward velocity
            float glideX = Mathf.Max(initialGlideSpeed, Mathf.Abs(rb.linearVelocity.y));

            // Increase glide speed multiplier over time
            glideSpeedMultiplier += Time.deltaTime;

            // Determine direction based on graphic rotation
            float direction = graphic.transform.eulerAngles.y == 0 ? 1f : -1f;
            rb.linearVelocity = new Vector2((glideX * glideSpeedMultiplier) * direction, rb.linearVelocity.y * 0.9f);
        }
        else
        {
            // Exit glide state if not falling
            currentState = PlayerState.Fall;
            animator.SetBool("isGliding", false);
            animator.SetBool("isFalling", true);
        }
    }

    #region Climb Region

    /// <summary>
    /// Handles attach input for starting or stopping climbing.
    /// </summary>
    /// <param name="context">Input action callback context.</param>
    private void Attach(InputAction.CallbackContext context)
    {
        // Prevent attaching if stunned
        if (currentState == PlayerState.STUNNED)
        {
            return;
        }

        // Stop climbing if already climbing
        if (currentState == PlayerState.Climb && context.canceled)
        {
            StopClimb();
            return;
        }

        if (climbTime > maxClimbTime)
        {
            return;
        }


        Collider2D climbableCollider = Physics2D.OverlapCircle(climbCheckOrigin.position, climbCheckReach, climbableLayer);
        if (climbableCollider != null)
        {
            // Attach to the climbable object
            transform.position = new Vector2(climbableCollider.ClosestPoint(transform.position).x, transform.position.y);
            StartClimb();
        }
    }



    /// <summary>
    /// Handles climbing movement and shake effect while climbing.
    /// </summary>
    private void Climb()
    {
        if (climbTime > maxClimbTime)
        {
            StopClimb();
            return;
        }

        climbTime += Time.deltaTime;
        UpdateClimbParticles();
        UpdateClimbFatigueColor();

        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;

        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        Collider2D treeCollider = Physics2D.OverlapCircle(transform.position, 0.5f, climbableLayer);

        if (treeCollider == null)
        {
            StopClimb();
            return;
        }

        ApplyClimbShake();

        Vector2 moveLocation = transform.position + graphic.transform.right * Time.deltaTime * 3 * climbSpeed;
        Debug.DrawLine(transform.position, moveLocation, Color.red, float.MaxValue);

        if (treeCollider.OverlapPoint(moveLocation))
        {
            transform.position += (Vector3)(moveInput * Time.deltaTime * climbSpeed);
        }
        else
        {
            StopClimb();
        }

        animator.SetBool("isClimbMoving", moveInput != Vector2.zero);
    }

    private void UpdateClimbParticles()
    {
        var emission = climbParticle.emission;
        emission.rateOverTime = Mathf.Lerp(0, climbParticleRateOverTime, climbTime / maxClimbTime);
    }

    private void UpdateClimbFatigueColor()
    {
        graphicSprite.color = Color.Lerp(Color.white, climbFatigueColor, climbTime / maxClimbTime);
    }

    private void ApplyClimbShake()
    {
        if (graphic == null) return;

        float shakeRatio = Mathf.Clamp01(climbTime / maxClimbTime);
        float shakeAmount = maxShakeIntensity * shakeRatio;
        Vector3 shakeOffset = new Vector3(
            Random.Range(-shakeAmount, shakeAmount),
            Random.Range(-shakeAmount, shakeAmount),
            0f
        );
        graphic.transform.localPosition = graphicOriginalLocalPos + shakeOffset;
    }

    /// <summary>
    /// Starts climbing by updating state and disabling collider.
    /// </summary>
    private void StartClimb()
    {
        currentState = PlayerState.Climb;

        playerCollider.enabled = false;

        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;

        animator.SetBool("isClimbing", true);
    }

    /// <summary>
    /// Stops climbing and resets relevant properties.
    /// </summary>
    private void StopClimb()
    {
        animator.SetBool("isClimbing", false);

        currentState = PlayerState.Fall;

        playerCollider.enabled = true;

        rb.gravityScale = 1;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Reset graphic position when climb ends
        if (graphic != null)
            graphic.transform.localPosition = graphicOriginalLocalPos;

        // Require button release before next glide
        glideButtonReleasedSinceClimb = false;
    }

    private void ResetClimb()
    {

        // Reset climb time and particle emission
        climbTime = 0;
        var emission = climbParticle.emission;
        emission.rateOverTime = 0;

        graphicSprite.color = Color.white;
    }

    #endregion

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
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // If grounded, update state and animations
        if (isGrounded && currentState != PlayerState.Grounded)
        {
            if (currentState == PlayerState.STUNNED)
            {
                stunnedEffect.SetActive(false);
            }

            ResetClimb();

            currentState = PlayerState.Grounded;
            print("Grounded");
            animator.SetBool("isFalling", false);
            animator.SetBool("isGliding", false);

        }
        // If not grounded, set state to falling
        else if (!isGrounded && currentState == PlayerState.Grounded)
        {
            currentState = PlayerState.Fall;
        }
    }

    /// <summary>
    /// Handles collision events, such as hitting a tree while gliding.
    /// </summary>
    /// <param name="collision">Collision data.</param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // If gliding and hit a climbable object
        if (((1 << collision.gameObject.layer) & climbableLayer) != 0)
        {

            //print(collision.relativeVelocity);

            // Ignore if velocity is below threshold
            if (Mathf.Abs(collision.relativeVelocity.x) >= glideHitVelocityThreshold)
            {
                // Get contact normal for force direction
                var contactNormal = collision.GetContact(0).normal;

                // Stop current velocity and apply bounce force
                rb.linearVelocity = Vector3.zero;
                rb.AddForce(Vector2.up * glideTreeHitYVelo + Vector2.right * contactNormal * glideTreeHitXVelo, ForceMode2D.Impulse);

                // Enter fall and stunned states, update animations and effects
                currentState = PlayerState.Fall;
                animator.SetBool("isGliding", false);
                animator.SetBool("isFalling", true);

                currentState = PlayerState.STUNNED;
                stunnedEffect.SetActive(true);
                return;
            }

            // Attach to the tree and start climbing
            // transform.position = collision.GetContact(0).point;
            // StartClimb();
        }
    }

    /// <summary>
    /// Gets the current player state.
    /// </summary>
    /// <returns>The current PlayerState.</returns>
    public PlayerState GetPlayerState()
    {
        return currentState;
    }

    /// <summary>
    /// Enables the movement input action.
    /// </summary>
    private void EnableMove()
    {
        moveAction.Enable();
    }

    /// <summary>
    /// Disables the movement input action.
    /// </summary>
    private void DisableMove()
    {
        moveAction.Disable();
    }

    private void OnDisable()
    {
        playerMovementMap.Disable();
    }

}


public enum PlayerState
{
    Grounded,
    Climb,
    Glide,
    Fall,
    STUNNED

}