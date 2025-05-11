using System.Collections;
using System.Collections.Generic;
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

    [SerializeField] private GameObject graphic;
    [SerializeField] private Collider2D playerCollider;

    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;



    [SerializeField] private float climbCheckReach = 0.2f;
    [SerializeField] private LayerMask climbableLayer;
    [SerializeField] private Transform climbCheckOrigin;
    [SerializeField] private float climbSpeed = 5.0f;


    [SerializeField] private float maxGlideSpeed = 10f;
    [SerializeField] private float initialGlideSpeed = 5f;
    
    [SerializeField] private float jumpForce = 5.0f;

    [SerializeField] private float moveSpeed = 5.0f;

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
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {


    }



    private void FixedUpdate()
    {

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
            rb.gravityScale = 1.2f;
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
        }
    }

    private void Jump(InputAction.CallbackContext context)
    {
        if (currentState != PlayerState.Grounded)
        {
            return;
        }

        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        currentState = PlayerState.Fall;
    }

    private void GlideInput(InputAction.CallbackContext context)
    {
        if(currentState == PlayerState.Glide){
            currentState = PlayerState.Fall;
            return;
        }
        
        if(currentState != PlayerState.Fall){
            return;
        }

        // Store the downward speed when starting to glide
        initialGlideSpeed = Mathf.Abs(rb.linearVelocity.y);

        currentState = PlayerState.Glide;
    }

    private void Glide()
    {
        // Only apply glide if falling downwards
        if (rb.linearVelocity.y < 0)
        {
            // Use the greater of current downward speed or the initial downward speed
            float glideX = Mathf.Max(initialGlideSpeed, Mathf.Abs(rb.linearVelocity.y));

            // Clamp to maxGlideSpeed if desired
            glideX = Mathf.Min(glideX, maxGlideSpeed);

            // Preserve direction the player is facing
            float direction = graphic.transform.eulerAngles.y == 0 ? 1f : -1f;
            rb.linearVelocity = new Vector2(glideX * direction, rb.linearVelocity.y * 0.7f);
        }
        else
        {
            // If not falling, exit glide state
            currentState = PlayerState.Fall;
        }
    }

    private void Climb()
    {
        rb.gravityScale = 0;

        rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation;
        //srb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var moveInput = moveAction.ReadValue<Vector2>();

        //Physics2D.queriesStartInColliders = true;

        var treeCollider = Physics2D.OverlapCircle(transform.position, 0.5f, climbableLayer);

        if (treeCollider == null)
        {
            StopClimb();
            return;
        }

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
    }

    private void StopClimb()
    {
        currentState = PlayerState.Fall;

        playerCollider.enabled = true;

        rb.gravityScale = 1;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void GroundCheck()
    {
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (isGrounded && currentState != PlayerState.Grounded)
        {
            currentState = PlayerState.Grounded;
        }
        else if (!isGrounded && currentState == PlayerState.Grounded)
        {
            currentState = PlayerState.Fall;
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
        Fall

    }
}
