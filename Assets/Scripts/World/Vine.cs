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

    private Vector2 vineAttachPoint;


    void Awake()
    {
        // Initialize input action map
        playerMovementMap = new PlayerGameControls();

        attachAction = playerMovementMap.Gameplay.Attach;
        attachAction.performed += AttachInput;
        attachAction.canceled += DetachInput;

        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (playerAttached)
        {
            // Keep the player's position aligned with the vine
            playerTransform.position = transform.TransformPoint(vineAttachPoint);
        }
    }


    //if the player is attached, make the linear dampening low
    // if the player is not attached, make the linear dampening high
//maybe speed the vine up when the player is attached?
    void AttachInput(InputAction.CallbackContext context)
    {
        if (!playerAttached)
        {

            playerAttached = true;

            playerMove.StartVineSwing(); // Start vine swinging state

            vineAttachPoint = transform.InverseTransformPoint(playerTransform.position);
            rb.linearVelocity = playerRb.linearVelocity; // Stop the vine's movement

            print(playerRb.linearVelocity);

            playerRb.linearVelocity = Vector2.zero; // Stop the player's movement
            return;
        }

        playerAttached = false;

        playerRb.linearVelocity = rb.linearVelocity;

        playerMove.EndVineSwing();


        print(rb.linearVelocity);
        

    }

    void DetachInput(InputAction.CallbackContext context)
    {


        // Reset player's position to the vine's attach point
        //playerTransform.position = transform.TransformPoint(vineAttachPoint);
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
