using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class VineSegment : MonoBehaviour
{



    /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerGameControls playerMovementMap;

    InputAction attachAction;

    private Rigidbody2D rb;

    [SerializeField] Transform vineRoot;

    private Rigidbody2D playerRb;
    private Transform playerTransform;

    private PlayerMove playerMove;

    private bool playerAttached = false;

    private static bool canAttach = true;

    private Vector2 vineAttachPoint;

    private float vineDetachTime = 0.25f;

    private float vineDetachTimer = 0f;

    [Header("Vine Settings")]
    [Space(10)]

    [Header("Default Values")]
    [SerializeField] private float defaultMass = 1f;

    [Header("Attached Values")]
    [SerializeField] private float attachedMass = 20f;


    private bool shouldAccelerate = true;

    private float attachVeloMag;


    void Awake()
    {
        // Initialize input action map
        playerMovementMap = new PlayerGameControls();

        attachAction = playerMovementMap.Gameplay.Attach;
        attachAction.performed += AttachInput;


        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (playerAttached)
        {
            // Keep the player's position aligned with the vine
            playerTransform.position = transform.TransformPoint(vineAttachPoint);
        }

        if (!canAttach)
        {
            if (vineDetachTimer > vineDetachTime)
            {
                canAttach = true;
                vineDetachTimer = 0f;
            }
            else
            {
                vineDetachTimer += Time.deltaTime;
            }
        }
    }

    void FixedUpdate()
    {
        if (!playerAttached)
        {
            return;
        }
        
        if(rb.linearVelocity.magnitude < 1f)
        {
            shouldAccelerate = false;
            return;
        }

        Debug.DrawRay(transform.position, rb.linearVelocity);

        if (shouldAccelerate)
        {

            Vector2 dirToRoot = (vineRoot.position - transform.position).normalized;

            Vector2 tangentDir = Quaternion.Euler(0, 0, -90) * dirToRoot; // Rotate dirToRoot 90 degrees CCW using RotateAngle
            rb.linearVelocity += tangentDir * attachVeloMag * Time.fixedDeltaTime * 0.6f;
            //rb.linearVelocity += rb.linearVelocity.normalized * attachVeloMag * Time.fixedDeltaTime;
            // rb.linearVelocity += Vector2.up * attachVeloMag * Time.fixedDeltaTime * 0.5f
            //         + Vector2.right * attachVeloMag * Time.fixedDeltaTime * 0.5f;
        }


    }




    //if the player is attached, make the linear dampening low
    // if the player is not attached, make the linear dampening high
    //maybe speed the vine up when the player is attached?
    void AttachInput(InputAction.CallbackContext context)
    {

        print("Attach input received");

        if (playerAttached)
        {
            DetachPlayer();
        }

        if (!canAttach)
        {
            return;
        }

        AttachPlayer();
    }

    void AttachPlayer()
    {
        shouldAccelerate = true;
        playerAttached = true;

        playerMove.StartVineSwing(); // Start vine swinging state

        vineAttachPoint = transform.InverseTransformPoint(playerTransform.position);
        rb.linearVelocity = playerRb.linearVelocity; // Stop the vine's movement

        print(playerRb.linearVelocity);

        attachVeloMag = playerRb.linearVelocity.x;

        playerRb.linearVelocity = Vector2.zero; // Stop the player's movement

        rb.mass = attachedMass;
        //rb.gravityScale = attachedGravityScale;
    }

    void DetachPlayer()
    {
        playerAttached = false;

        playerRb.linearVelocity = rb.linearVelocity;

        //print(playerRb.linearVelocity);

        playerMove.EndVineSwing();

        canAttach = false;

        rb.mass = defaultMass;
        //rb.gravityScale = defaultGravityScale;

        attachAction.Disable();


        //print(rb.linearVelocity);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        attachAction.Enable();

        playerTransform = other.transform.root;


        if (playerMove == null)
        {
            playerMove = playerTransform.GetComponent<PlayerMove>();
        }

        if (playerRb == null)
        {
            playerRb = playerTransform.GetComponent<Rigidbody2D>();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (playerAttached)
        {
            return;
        }

        print("input disabled: " + gameObject.name);
        attachAction.Disable();
    }
}
