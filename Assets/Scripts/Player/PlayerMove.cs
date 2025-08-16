
using System.ComponentModel;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{

    private PlayerLifeManager lifeManager;

    /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerGameControls playerMovementMap;

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

    [SerializeField] private PlayerCamera playerCamera;

    [Header("Collision")]
    /// <summary>
    /// LayerMask for identifying layers that cause damage on collision (e.g., trees).
    /// </summary>
    [SerializeField] private LayerMask collideDamageLayer;

    [SerializeField] private float minDamageSpeed;

    /// <summary>
    /// Horizontal velocity applied when hitting a tree while gliding.
    /// </summary>
    [SerializeField] private float damageHitXVelo = 10f;

    /// <summary>
    /// Vertical velocity applied when hitting a tree while gliding.
    /// </summary>
    [SerializeField] private float damageHitYVelo = 5f;

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

    /// <summary>
    /// Current elapsed climb time.
    /// </summary>
    private float climbTime;


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

    /// <summary>
    /// Maximum number of times player can spam climb to reach a branch.
    /// </summary>
    [SerializeField] private int spamCheeseMax = 5;
    private int spamCheeseCount = 0;

    [Header("Glide")]

    /// <summary>
    /// Initial horizontal speed when starting to glide.
    /// </summary>
    [SerializeField] private float defaultGlideSpeed = 10f;
    private float initialGlideSpeed;

    [SerializeField] private float maxGlideSpeed = 35f;
    [SerializeField] private float maxGlideSpeedInGust = 50f;


    private bool inGust = false;

    [SerializeField] private float flightMultiper = 2f;

    /// <summary>
    /// Multiplier for glide speed, increases over time.
    /// </summary>
    private float glideSpeedMultiplier = 1f;

    /// <summary>
    /// Minimum velocity threshold to trigger a glide hit.
    /// </summary>
    [SerializeField] private float glideHitVelocityThreshold = 5f;

    private bool glideButtonReleasedSinceClimb = true;

    /// <summary>
    /// Force applied when jumping.
    /// </summary>
    [SerializeField] private float jumpForce = 5.0f;

    private float jumpHeldDuration = 0f;

    private float jumpBufferTime = 0.1f; // Adjust as needed (0.1s = ~6 frames at 60fps)
    private float jumpBufferTimer = 0f;

    private bool isJumpHeld = false;

    [Header("Camera Zoom Settings")]
    [SerializeField] private float maxSideMovementZoom = 40f;
    [SerializeField] private float sideSpeedThreshold = 15f;
    [SerializeField] private AnimationCurve sideMovementZoomCurve;

    /// <summary>
    /// Current state of the player.
    /// </summary>
    [SerializeField] private PlayerState currentState = PlayerState.Grounded;

    public static event System.Action Jumped;


    /// <summary>
    /// Initializes input actions and sets up event handlers.
    /// </summary>
    void Awake()
    {
        // Initialize input action map
        playerMovementMap = new PlayerGameControls();

        lifeManager = GetComponent<PlayerLifeManager>();

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
        playerCamera = GetComponentInChildren<PlayerCamera>();
        // Store original graphic position for shake effect
        if (graphic != null)
            graphicOriginalLocalPos = graphic.transform.localPosition;
    }

    /// <summary>
    /// Handles physics-based updates and state-specific movement logic.
    /// </summary>
    private void FixedUpdate()
    {
        // Prevent movement if stunned
        if (currentState == PlayerState.STUNNED
        || currentState == PlayerState.RidingOwl
        || currentState == PlayerState.VineSwinging)
        {
            return;
        }

        SideMovementCameraZoom();

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
            jumpHeldDuration = 0f;
            playerCamera.EndForceZoom(PlayerCamera.CameraState.GlideZoom);
            return;
        }

        if (climbTime > 0)
        {
            climbTime -= Time.deltaTime / 2f;
            UpdateClimbFatigueColor();
            UpdateClimbParticles();
        }

        if (currentState == PlayerState.Glide)
        {
            jumpHeldDuration = 0f;
            rb.gravityScale = inGust ? 0.0f : 2.8f;
            return;
        }

        animator.SetBool("isFalling", true);

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
                rb.linearVelocity.x + moveInput.x * airMoveSpeed * Time.deltaTime,
                rb.linearVelocity.y
            );
        }
        else if (currentState == PlayerState.Fall)
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

        private void SideMovementCameraZoom()
    {

        if (currentState != PlayerState.Glide && currentState != PlayerState.Fall)
        {
            playerCamera.EndForceZoom(PlayerCamera.CameraState.GlideZoom);
            return;
        }

        float sideMovementSpeed = rb.linearVelocity.magnitude;

        if (sideMovementSpeed > sideSpeedThreshold)
        {
            playerCamera.StartForceZoom(Mathf.Lerp(playerCamera.GetDefaultZoom(), maxSideMovementZoom, sideMovementZoomCurve.Evaluate(sideMovementSpeed / maxGlideSpeed)), PlayerCamera.CameraState.GlideZoom);
        }

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

        //If we want to detach from the owl when jumping, we can uncomment this section

        // if (currentState == PlayerState.RidingOwl)
        // {
        //     DetachFromOwl();
        //     return;
        // }

        // Only allow jumping if grounded and not stunned
        if (currentState != PlayerState.Grounded)
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
        currentState = PlayerState.Fall;
        animator.SetBool("isFalling", true);

        jumpBufferTimer = jumpBufferTime;
    }

    #region Glide Region

    /// <summary>
    /// Handles glide input and toggles gliding state.
    /// </summary>
    /// <param name="context">Input action callback context.</param>
    private void GlideInput(InputAction.CallbackContext context)
    {

        if (context.canceled)
        {
            if (currentState == PlayerState.Glide)
            {

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


        initialGlideSpeed = defaultGlideSpeed;

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
            currentState = PlayerState.Fall;
            animator.SetBool("isGliding", false);
            animator.SetBool("isFalling", true);
        }
    }


    void ResetGlide()
    {
        glideSpeedMultiplier = 1f;
    }

    #endregion

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

        // Stop climbing if already climbing and button released
        if (currentState == PlayerState.Climb && context.canceled)
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
        UpdateClimbParticles();
        UpdateClimbFatigueColor();

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

        ApplyClimbShake();
        ControllerRumble();

        // Calculate intended move location
        Vector2 moveLocation = transform.position + (Vector3)(Vector3.right * moveInput.x * Time.deltaTime * climbSpeed + Vector3.up * climbSpeed * Time.deltaTime);
        Debug.DrawLine(transform.position, moveLocation, Color.red, 0.1f);

        // Check if moveLocation is still inside any climbable collider
        bool insideAny = false;
        foreach (var col in overlappingColliders)
        {
            if (col.OverlapPoint(moveLocation))
            {
                insideAny = true;
                break;
            }
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

    private void ControllerRumble()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float intensity = Mathf.Clamp01(climbTime / maxClimbTime);
            gamepad.SetMotorSpeeds(intensity, intensity);
        }
    }

    private void StopControllerRumble()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(0, 0);
        }
    }

    /// <summary>
    /// Starts climbing by updating state and disabling collider.
    /// </summary>
    private void StartClimb()
    {



        currentState = PlayerState.Climb;

        playerCollider.excludeLayers = noCollisionClimbLayer;

        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;

        animator.SetBool("isClimbing", true);
        animator.SetBool("isGliding", false);
    }

    /// <summary>
    /// Stops climbing and resets relevant properties.
    /// </summary>
    private void StopClimb()
    {
        animator.SetBool("isClimbing", false);

        currentState = PlayerState.Fall;

        playerCollider.excludeLayers = 0;

        rb.gravityScale = 1;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        StopControllerRumble();

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

            ResetGlide();


            currentState = PlayerState.Grounded;
            //print("Grounded");
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
        if (((1 << collision.gameObject.layer) & collideDamageLayer) != 0)
        {

            //            print("collision velo: " + collision.relativeVelocity);

            // Ignore if velocity is below threshold
            if (Mathf.Abs(collision.relativeVelocity.x) >= minDamageSpeed)
            {
                // Get contact normal for force direction
                var contactNormal = collision.GetContact(0).normal;

                if (Vector2.Angle(contactNormal, Vector2.up) < 45f)
                {
                    return;
                }

                var launchDir = Vector2.up * damageHitYVelo + Vector2.right * contactNormal * damageHitXVelo;

                lifeManager.DamagePlayer(launchDir);

                return;
            }

            // Attach to the tree and start climbing
            // transform.position = collision.GetContact(0).point;
            // StartClimb();
        }
    }



    public void StunPlayer()
    {
        if (currentState == PlayerState.STUNNED)
        {
            return;
        }

        DisableMove();

        currentState = PlayerState.STUNNED;
        stunnedEffect.SetActive(true);
        animator.SetBool("isStunned", true);
        animator.SetBool("isFalling", false);
        animator.SetBool("isGliding", false);
        animator.SetBool("isClimbing", false);
        animator.SetBool("isClimbMoving", false);
    }

    public void StopStun()
    {
        if (currentState != PlayerState.STUNNED)
        {
            return;
        }

        ResetGlide();

        EnableMove();

        currentState = PlayerState.Fall;

        stunnedEffect.SetActive(false);
        animator.SetBool("isStunned", false);
        animator.SetBool("isFalling", false);
        animator.SetBool("isGliding", false);
        animator.SetBool("isClimbing", false);
        animator.SetBool("isClimbMoving", false);

        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
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

    #region Owl Riding

    public void AttachToOwl()
    {
        currentState = PlayerState.RidingOwl;
        rb.gravityScale = 0f;
        animator.SetBool("isFalling", false);
        animator.SetBool("isGliding", false);
        animator.SetBool("isClimbing", false);
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

        playerCamera.EndForceZoom(PlayerCamera.CameraState.GlideZoom);
        currentState = PlayerState.Fall;
        rb.gravityScale = 1f;
        animator.SetBool("isFalling", true);
        animator.SetBool("isGliding", false);
        animator.SetBool("isClimbing", false);
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
        currentState = PlayerState.VineSwinging;
        //animator.SetBool("isVineSwinging", true);
    }

    public void EndVineSwing()
    {
        EnableMove();
        rb.gravityScale = 1f;
        currentState = PlayerState.Fall;
        //animator.SetBool("isVineSwinging", false);
    }


    #endregion

    /// <summary>
    /// Gets the current player state.
    /// </summary>
    /// <returns>The current PlayerState.</returns>
    public PlayerState GetPlayerState()
    {
        return currentState;
    }
}


public enum PlayerState
{
    Grounded,
    Climb,
    Glide,
    Fall,
    RidingOwl,
    VineSwinging,
    STUNNED

}