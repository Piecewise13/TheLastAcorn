
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerMove : MonoBehaviour {
/// <summary>
/// Reference to the PlayerControls input action map.
/// </summary>
private PlayerControls playerMovementMap;

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
[SerializeField] private float moveSpeed = 5.0f;

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

/// <summary>
/// Maximum intensity of the shake effect while climbing.
/// </summary>
[SerializeField] private float maxShakeIntensity = 0.2f;

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

/// <summary>
/// Force applied when jumping.
/// </summary>
[SerializeField] private float jumpForce = 5.0f;

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
    playerMovementMap = new PlayerControls();

    // Assign movement action and enable it
    moveAction = playerMovementMap.Keyboard.Move;
    EnableMove();

    // Assign attach action and subscribe to event
    attachAction = playerMovementMap.Keyboard.Attach;
    attachAction.performed += Attach;
    attachAction.Enable();

    // Assign glide action and subscribe to event
    glideAction = playerMovementMap.Keyboard.Glide;
    glideAction.performed += GlideInput;
    glideAction.Enable();

    // Assign jump action and subscribe to event
    jumpAction = playerMovementMap.Keyboard.Jump;
    jumpAction.performed += Jump;
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
    if(currentState == PlayerState.STUNNED){
        GroundCheck();
    }
}

/// <summary>
/// Handles physics-based updates and state-specific movement logic.
/// </summary>
private void FixedUpdate()
{
    // Prevent movement if stunned
    if(currentState == PlayerState.STUNNED){
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

    // Handle gliding logic
    if(currentState == PlayerState.Glide)
    {
        Glide();
        return;
    }

    GravityAdjustment();
    
    // Handle ground movement if move action is in progress
    if (moveAction.inProgress)
    {
        GroundMovement();
        return;
    }

    if(currentState == PlayerState.Grounded)
    {
        // Reset horizontal movement and update animation
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        animator.SetBool("isRunning", false);
    }
}

private void GravityAdjustment()
{
    // Adjust gravity scale based on player state
    if (currentState == PlayerState.Grounded)
    {
        rb.gravityScale = 1;
    }
    else if (currentState == PlayerState.Fall)
    {
        rb.gravityScale = 1.8f;
    }
}

/// <summary>
/// Handles player movement while grounded.
/// </summary>
private void GroundMovement()
{
    // Prevent movement if climbing
    if (currentState == PlayerState.Climb)
    {
        return;
    }

    // Read movement input
    var moveInput = moveAction.ReadValue<Vector2>();
    // Apply horizontal velocity
    rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);

    // Flip graphic and update running animation if moving
    if (moveInput.x != 0)
    {
        float targetYRotation = moveInput.x > 0 ? 0f : 180f;
        Vector3 rotation = graphic.transform.eulerAngles;
        rotation.y = targetYRotation;
        graphic.transform.eulerAngles = rotation;
        animator.SetBool("isRunning", true);
    }
}

/// <summary>
/// Handles jump input and applies jump force.
/// </summary>
/// <param name="context">Input action callback context.</param>
private void Jump(InputAction.CallbackContext context)
{
    // Only allow jumping if grounded and not stunned
    if (currentState != PlayerState.Grounded || currentState == PlayerState.STUNNED)
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
}

/// <summary>
/// Handles glide input and toggles gliding state.
/// </summary>
/// <param name="context">Input action callback context.</param>
private void GlideInput(InputAction.CallbackContext context)
{
    // If already gliding, stop gliding
    if(currentState == PlayerState.Glide){
        animator.SetBool("isGliding", false);
        currentState = PlayerState.Fall;
        return;
    }
    
    // Only allow gliding if falling and not stunned
    if(currentState != PlayerState.Fall){
        return;
    }

    if(currentState == PlayerState.STUNNED){
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

/// <summary>
/// Handles climbing movement and shake effect while climbing.
/// </summary>
private void Climb()
{
    // Stop climbing if max climb time exceeded
    if(climbTime > maxClimbTime){
        print("Climb is out!");
        StopClimb();
        return;
    }

    // Increment climb time
    climbTime += Time.deltaTime;

    // Disable gravity and freeze position
    rb.gravityScale = 0;
    rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;

    // Read movement input
    var moveInput = moveAction.ReadValue<Vector2>();

    // Check for climbable object nearby
    var treeCollider = Physics2D.OverlapCircle(transform.position, 0.5f, climbableLayer);

    if (treeCollider == null)
    {
        StopClimb();
        return;
    }

    // Apply shake effect to graphic based on climb time
    if (graphic != null)
    {
        float shakeRatio = Mathf.Clamp01(climbTime / maxClimbTime);
        float shakeAmount = maxShakeIntensity * shakeRatio;
        Vector3 shakeOffset = new Vector3(
            Random.Range(-shakeAmount, shakeAmount),
            Random.Range(-shakeAmount, shakeAmount),
            0f
        );
        graphic.transform.localPosition = graphicOriginalLocalPos + shakeOffset;
    }

    // Find closest point on tree collider
    var closestPoint = treeCollider.ClosestPoint(transform.position);

    // Calculate intended move location
    Vector2 moveLocation = transform.position + Vector3.right * moveInput.x * Time.deltaTime * 2 * climbSpeed; 

    // Move player if within tree collider, otherwise snap to closest point
    if(treeCollider.OverlapPoint(moveLocation))
    {
        transform.position += (Vector3)(moveInput * Time.deltaTime * climbSpeed);
    } else{
        transform.position = closestPoint + Vector2.up * moveInput.y * Time.deltaTime * climbSpeed;
    }
}

/// <summary>
/// Handles attach input for starting or stopping climbing.
/// </summary>
/// <param name="context">Input action callback context.</param>
private void Attach(InputAction.CallbackContext context)
{
    // Prevent attaching if stunned
    if(currentState == PlayerState.STUNNED){
        return;
    }

    // Stop climbing if already climbing
    if(currentState == PlayerState.Climb)
    {
        StopClimb();
        return;
    }

    // Raycast to check for climbable object to the right
    RaycastHit2D rightHit = Physics2D.Raycast(climbCheckOrigin.position, graphic.transform.right, climbCheckReach, climbableLayer);

    Debug.DrawRay(climbCheckOrigin.position, graphic.transform.right * climbCheckReach, Color.green);
    //Debug.DrawRay(climbCheckOrigin.position, Vector2.left * climbCheckReach, Color.blue);
    
    // Start climbing if climbable object found
    if (rightHit.collider != null)
    {
        Debug.Log("Climbable");
        transform.position = new Vector2(rightHit.point.x + 0.5f, rightHit.point.y);
        StartClimb();
    }
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

    climbTime = 0;
}

/// <summary>
/// Stops climbing and resets relevant properties.
/// </summary>
private void StopClimb()
{
    climbTime = 0;
    currentState = PlayerState.Fall;

    playerCollider.enabled = true;

    rb.gravityScale = 1;
    rb.constraints = RigidbodyConstraints2D.FreezeRotation;

    // Reset graphic position when climb ends
    if (graphic != null)
        graphic.transform.localPosition = graphicOriginalLocalPos;
}

/// <summary>
/// Checks if the player is grounded and updates state accordingly.
/// </summary>
private void GroundCheck()
{
    // Check for ground using overlap circle
    bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

    // If grounded, update state and animations
    if (isGrounded && currentState != PlayerState.Grounded)
    {
        if(currentState == PlayerState.STUNNED){
            stunnedEffect.SetActive(false);
        }

        currentState = PlayerState.Grounded;
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
        // Ignore if velocity is below threshold
        if (Mathf.Abs(collision.relativeVelocity.x) < glideHitVelocityThreshold)
        {
            return;
        }

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

}
/*
    private PlayerControls playerMovementMap;
    private InputAction moveAction;
    private InputAction attachAction;
    private InputAction glideAction;
    private InputAction jumpAction;

    private Rigidbody2D rb;
    private Animator animator;

    [Header("Components")]
    [SerializeField] private GameObject graphic;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private GameObject stunnedEffect;

    [Space(20)]
    [Header("Ground Check")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;


    [Space(20)]
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5.0f;

    [Header("Climb")]

    [SerializeField] private float climbCheckReach = 0.2f;
    [SerializeField] private LayerMask climbableLayer;
    [SerializeField] private Transform climbCheckOrigin;
    [SerializeField] private float climbSpeed = 5.0f;
    [SerializeField] private float maxClimbTime;
    private float climbTime;

    [SerializeField] private float maxShakeIntensity = 0.2f; // You can tweak this value
    private Vector3 graphicOriginalLocalPos;

    [Header("Glide")]

    [SerializeField] private float maxGlideSpeed = 10f;
    [SerializeField] private float initialGlideSpeed = 5f;
    private float glideSpeedMultiplier = 1f;

    [SerializeField] private float glideTreeHitXVelo = 10f;
    [SerializeField] private float glideTreeHitYVelo = 5f;
    [SerializeField] private float glideHitVelocityThreshold = 5f;
    
    [SerializeField] private float jumpForce = 5.0f;


    [SerializeField]private PlayerState currentState = PlayerState.Grounded;

    void Awake()
    {

        playerMovementMap = new PlayerControls();

        moveAction = playerMovementMap.Keyboard.Move;
        EnableMove();

        attachAction = playerMovementMap.Keyboard.Attach;
        attachAction.performed += Attach;
        attachAction.Enable();

        glideAction = playerMovementMap.Keyboard.Glide;
        glideAction.performed += GlideInput;
        glideAction.Enable();

        jumpAction = playerMovementMap.Keyboard.Jump;
        jumpAction.performed += Jump;
        jumpAction.Enable();
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        if (graphic != null)
            graphicOriginalLocalPos = graphic.transform.localPosition;
    }

    private void Update()
    {
        if(currentState == PlayerState.STUNNED){
            GroundCheck();
        }
    }



    private void FixedUpdate()
    {

        if(currentState == PlayerState.STUNNED){
            return;
        }

        if (currentState == PlayerState.Climb)
        {
            Climb();
            return;
        }
        
        GroundCheck();

        if(currentState == PlayerState.Glide)
        {
            Glide();
            return;
        }

        if(currentState == PlayerState.Fall)
        {
            rb.gravityScale = 1.8f;
        } else if(currentState == PlayerState.Grounded)
        {
            rb.gravityScale = 1;
        }
        

        if (moveAction.inProgress)
        {
            GroundMovement();
            return;
        }
        
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        animator.SetBool("isRunning", false);
    }

    private void GroundMovement()
    {
        if (currentState == PlayerState.Climb)
        {
            return;
        }

        var moveInput = moveAction.ReadValue<Vector2>();
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);

        if (moveInput.x != 0)
        {
            float targetYRotation = moveInput.x > 0 ? 0f : 180f;
            Vector3 rotation = graphic.transform.eulerAngles;
            rotation.y = targetYRotation;
            graphic.transform.eulerAngles = rotation;
            animator.SetBool("isRunning", true);
        }
    }

    private void Jump(InputAction.CallbackContext context)
    {
        if (currentState != PlayerState.Grounded || currentState == PlayerState.STUNNED)
        {
            return;
        }
        

        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        animator.SetTrigger("Jump");

        currentState = PlayerState.Fall;

        animator.SetBool("isFalling", true);
    }

    private void GlideInput(InputAction.CallbackContext context)
    {
        if(currentState == PlayerState.Glide){
            animator.SetBool("isGliding", false);
            currentState = PlayerState.Fall;
            return;
        }
        
        if(currentState != PlayerState.Fall){
            return;
        }

        if(currentState == PlayerState.STUNNED){
            return;
        }

        // Store the downward speed when starting to glide

        glideSpeedMultiplier = 1f;

        currentState = PlayerState.Glide;
        
        animator.SetBool("isGliding", true);
        
    }

    private void Glide()
    {
        // Only apply glide if falling downwards
        if (rb.linearVelocity.y < 0)
        {
            // Use the greater of current downward speed or the initial downward speed
            float glideX = Mathf.Max(initialGlideSpeed, Mathf.Abs(rb.linearVelocity.y));

            // Clamp to maxGlideSpeed if desired
            //glideX = Mathf.Min(glideX, maxGlideSpeed);
            glideSpeedMultiplier += Time.deltaTime;

            // Preserve direction the player is facing
            float direction = graphic.transform.eulerAngles.y == 0 ? 1f : -1f;
            rb.linearVelocity = new Vector2((glideX * glideSpeedMultiplier) * direction, rb.linearVelocity.y * 0.9f);
        }
        else
        {
            // If not falling, exit glide state
            currentState = PlayerState.Fall;
            animator.SetBool("isGliding", false);
            animator.SetBool("isFalling", true);
        }
    }

    private void Climb()
    {

        if(climbTime > maxClimbTime){
            print("Climb is out!");
            StopClimb();
            return;
        }

        climbTime += Time.deltaTime;

        rb.gravityScale = 0;

        rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;

        var moveInput = moveAction.ReadValue<Vector2>();

        var treeCollider = Physics2D.OverlapCircle(transform.position, 0.5f, climbableLayer);

        if (treeCollider == null)
        {
            StopClimb();
            return;
        }

        // --- SHAKE EFFECT START ---
        if (graphic != null)
        {
            float shakeRatio = Mathf.Clamp01(climbTime / maxClimbTime);
            float shakeAmount = maxShakeIntensity * shakeRatio;
            Vector3 shakeOffset = new Vector3(
                Random.Range(-shakeAmount, shakeAmount),
                Random.Range(-shakeAmount, shakeAmount),
                0f
            );
            graphic.transform.localPosition = graphicOriginalLocalPos + shakeOffset;
        }
        // --- SHAKE EFFECT END ---

        var closestPoint = treeCollider.ClosestPoint(transform.position);

        Vector2 moveLocation = transform.position + Vector3.right * moveInput.x * Time.deltaTime * 2 * climbSpeed; 

        if(treeCollider.OverlapPoint(moveLocation))
        {
            transform.position += (Vector3)(moveInput * Time.deltaTime * climbSpeed);
        } else{
            transform.position = closestPoint + Vector2.up * moveInput.y * Time.deltaTime * climbSpeed;
        }

    }

    private void Attach(InputAction.CallbackContext context)
    {

        if(currentState == PlayerState.STUNNED){
            return;
        }

        if(currentState == PlayerState.Climb)
        {
            StopClimb();
            return;
        }

        RaycastHit2D rightHit = Physics2D.Raycast(climbCheckOrigin.position, graphic.transform.right, climbCheckReach, climbableLayer);

        Debug.DrawRay(climbCheckOrigin.position, graphic.transform.right * climbCheckReach, Color.green);
        //Debug.DrawRay(climbCheckOrigin.position, Vector2.left * climbCheckReach, Color.blue);
        

        if (rightHit.collider != null)
        {
            Debug.Log("Climbable");
            transform.position = new Vector2(rightHit.point.x + 0.5f, rightHit.point.y);
            StartClimb();
        }

    }

    private void StartClimb()
    {
        currentState = PlayerState.Climb;

        playerCollider.enabled = false;

        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;

        climbTime = 0;
    }

    private void StopClimb()
    {
        climbTime = 0;
        currentState = PlayerState.Fall;

        playerCollider.enabled = true;

        rb.gravityScale = 1;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Reset graphic position when climb ends
        if (graphic != null)
            graphic.transform.localPosition = graphicOriginalLocalPos;
    }

    private void GroundCheck()
    {
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded && currentState != PlayerState.Grounded)
        {

                        if(currentState == PlayerState.STUNNED){
                stunnedEffect.SetActive(false);
            }

            currentState = PlayerState.Grounded;
            animator.SetBool("isFalling", false);
            animator.SetBool("isGliding", false);

        }
        else if (!isGrounded && currentState == PlayerState.Grounded)
        {
            currentState = PlayerState.Fall;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState == PlayerState.Glide && ((1 << collision.gameObject.layer) & climbableLayer) != 0)
        {

            if (Mathf.Abs(collision.relativeVelocity.x) < glideHitVelocityThreshold)
            {
                return;
            }

            var contactNormal = collision.GetContact(0).normal;

            rb.linearVelocity = Vector3.zero;
            rb.AddForce(Vector2.up * glideTreeHitYVelo + Vector2.right * contactNormal * glideTreeHitXVelo, ForceMode2D.Impulse);

            // Enter "fall" state and freeze horizontal movement until grounded
            currentState = PlayerState.Fall;
            animator.SetBool("isGliding", false);
            animator.SetBool("isFalling", true);

            currentState = PlayerState.STUNNED;
            stunnedEffect.SetActive(true);

        }
    }

    public PlayerState GetPlayerState()
    {
        return currentState;
    }   

    private void EnableMove()
    {
        moveAction.Enable();
    }

    private void DisableMove()
    {
        moveAction.Disable();
    }
}
*/

    public enum PlayerState
    {
        Grounded,
        Climb,
        Glide,
        Fall,
        STUNNED

    }