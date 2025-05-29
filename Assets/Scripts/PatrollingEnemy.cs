using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PatrollingEnemy : MonoBehaviour
{
    [Tooltip("World-space points the enemy will move between")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [Tooltip("Units / second")]
    [SerializeField] private float speed = 2f;

    [Header("Feedback")] 
    [SerializeField] private AudioPlayer snakeAttack;

    private Rigidbody2D rb;
    private Vector2 target;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;  
        rb.freezeRotation = true;

        GetComponent<Collider2D>().isTrigger = true;
        target = pointB.position;
    }

    private void FixedUpdate()
    {
        // Move toward the current target point
        Vector2 newPos = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        // Reached the target?  Flip to the other one.
        if (Vector2.Distance(newPos, target) < 0.05f)
            target = (target == (Vector2)pointA.position) ? pointB.position : pointA.position;
        
        if (transform.localScale.x != Mathf.Sign(target.x - newPos.x))
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Sign(target.x - newPos.x) * Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        if (rb.linearVelocity.x != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = -Mathf.Sign(rb.linearVelocity.x) * Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        snakeAttack?.Play();
        PlayerLifeManager playerLifeManager = other.transform.root.GetComponent<PlayerLifeManager>();
        playerLifeManager.DamagePlayer();
    }
}
