using UnityEngine;
using UnityEngine.InputSystem;

public class Vine : MonoBehaviour
{



    /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerGameControls playerMovementMap;

    InputAction attachAction;

    private Rigidbody2D rb;

    private Rigidbody2D playerRb;
    private Transform playerTransform;

    private PlayerMove playerMove;

    private bool playerAttached = false;

    private static bool canAttach = true;

    private Vector2 vineAttachPoint;

    private float vineDetachTime = 0.25f;

    private float vineDetachTimer = 0f;

    [SerializeField] private float defaultMass = 1f;
    [SerializeField] private float attachedMass = 20f;


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
            if(vineDetachTimer > vineDetachTime)
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


    //if the player is attached, make the linear dampening low
    // if the player is not attached, make the linear dampening high
    //maybe speed the vine up when the player is attached?
    void AttachInput(InputAction.CallbackContext context)
    {


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
        playerAttached = true;

        playerMove.StartVineSwing(); // Start vine swinging state

        vineAttachPoint = transform.InverseTransformPoint(playerTransform.position);
        rb.linearVelocity = playerRb.linearVelocity; // Stop the vine's movement

        print(playerRb.linearVelocity);

        playerRb.linearVelocity = Vector2.zero; // Stop the player's movement

        rb.mass = attachedMass;
    }

    void DetachPlayer()
    {
        playerAttached = false;

        playerRb.linearVelocity = rb.linearVelocity;

        playerMove.EndVineSwing();

        canAttach = false;

        rb.mass = defaultMass;


        print(rb.linearVelocity);
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
        attachAction.Disable();
    }
}
