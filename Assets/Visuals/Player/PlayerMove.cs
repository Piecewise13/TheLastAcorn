
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerMove : MonoBehaviour
{

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

        if( currentState == PlayerState.Glide)
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

    private void EnableMove()
    {
        moveAction.Enable();
    }

    private void DisableMove()
    {
        moveAction.Disable();
    }


    private enum PlayerState
    {
        Grounded,
        Climb,
        Glide,
        Fall,
        STUNNED

    }
}
