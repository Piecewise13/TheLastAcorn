using UnityEngine;

public class BatScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [Tooltip("Speed of the bat's movement")]
    [SerializeField] private float speed = 100f;
    [Tooltip("The angle the bat will move towards, in degrees")]
    [SerializeField] private float targetAngle;

    private bool isAttached = false; // Flag to check if the bat is attached to the environment
    private bool isMoving = false; // Flag to check if the bat is currently moving

    private void Awake()
    {
        // Initialize any components or variables here if needed
        // Get the circle collider component
        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider == null)
        {
            circleCollider = gameObject.AddComponent<CircleCollider2D>();
        }
        circleCollider.isTrigger = true; // Set the collider as a trigger
                                         // Want the circle collider to act as a trigger, so it doesn't physically interact with the player
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        boxCollider.isTrigger = false; // This collider will be used for physical collisions
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;  // Disable gravity if needed
            rb.freezeRotation = true; // Prevent rotation
        }
    }

    private void FixedUpdate()
    {
        // If the bat is attached, it should not move
        if (isAttached) return;
        if (isMoving) return; // If already moving, do not process further
        // move up until the box collider collides with the tilemap collider
        Vector2 direction = Vector2.up; // Calculate the direction based on the angle
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Debug.Log("speed: " + speed);
            rb.linearVelocity = direction * speed * Time.fixedDeltaTime; // Set the velocity to move
            isMoving = true; // Set the moving flag to true
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // Only respond to the player entering the trigger (circle collider)
        if (other.CompareTag("Player"))
        {
            // Start moving the bat at the specified angle
            Vector2 direction = Quaternion.Euler(0, 0, targetAngle) * Vector2.right;
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = direction * speed * Time.fixedDeltaTime;
                isMoving = true;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Only respond to collisions with the environment (e.g., tilemap)
        if (!isAttached && !collision.collider.CompareTag("Player"))
        {
            // Stop the bat's movement when it collides with the environment
            isMoving = false; // Set the moving flag to false
            Vector2 collisionNormal = collision.contacts[0].normal; // Get the normal of the collision
            float facingAngle = Vector2.SignedAngle(Vector2.up, collisionNormal); // Calculate the angle based on the collision normal
            transform.rotation = Quaternion.Euler(0, 0, facingAngle); // Rotate the bat to face the collision normal

            // Optionally, you can also stop the Rigidbody2D's velocity
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
        else if (collision.collider.CompareTag("Player"))
        {
            // dont want to attach the bat to the player
            // only want to stun the player
            PlayerLifeManager playerLifeManager = collision.transform.root.GetComponent<PlayerLifeManager>();
            if (playerLifeManager != null)
            {
                playerLifeManager.DamagePlayer(Vector2.up * 20f); // Apply damage or stun effect
            }
        }
    }
}
