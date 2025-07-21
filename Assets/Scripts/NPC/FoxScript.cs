using UnityEngine;

public class FoxScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Transform player;
    [SerializeField] public float minDistance = 100f;
    [SerializeField] private float walkMultiplier = 0.3f;
    public float speed = 3f;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private CircleCollider2D attackRadius;
    private bool isRunning = false;
    private bool inAttackRadius = false;
    private float walkTimer = 0f;
    private float maxWalkTime = 1f;
    private float speedMultiplier = 1f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
        {
            Debug.LogWarning("Player not found! Make sure the player has the 'Player' tag.");
        }
    }
    void Start()
    {
        animator.SetBool("isRunning", false);
        animator.SetBool("isWalking", false);
        animator.SetBool("isAttacking", false);
    }

    // Update is called once per frame
    void Update()
    {
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            bool sameLevel = Mathf.Abs(transform.position.y - player.position.y) < 0.5f;

            if (distance < minDistance && sameLevel && !inAttackRadius)
            {
                Vector3 direction = (player.position - transform.position).normalized;
                if (direction.magnitude < 0.1f) // Prevents jittering when very close
                {
                    direction = Vector3.zero;
                }
                if (direction.x < 0)
                {
                    spriteRenderer.flipX = true; // Face left
                    animator.SetBool("isFlipped", true);
                }
                else if (direction.x > 0)
                {
                    spriteRenderer.flipX = false; // Face right
                    animator.SetBool("isFlipped", false);
                }
                walkTimer += Time.deltaTime;
                if (walkTimer >= maxWalkTime)
                {
                    if (!isRunning)
                    {
                        animator.SetBool("isWalking", false);
                        animator.SetBool("isRunning", true);
                        speedMultiplier = 1f;
                        isRunning = true;
                    }
                }
                else
                {
                    animator.SetBool("isWalking", true);
                    speedMultiplier = walkMultiplier;
                }
                direction.y = 0; // Ignore vertical movement
                transform.position += direction * speed * speedMultiplier * Time.deltaTime;


            }
            else
            {
                if (isRunning)
                {
                    animator.SetBool("isRunning", false);
                    isRunning = false;
                }

                if (animator.GetBool("isWalking"))
                {
                    animator.SetBool("isWalking", false);
                }

                walkTimer = 0f; // Reset walk timer when not chasing
                speedMultiplier = 1f; // Reset speed multiplier
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            inAttackRadius = true; // Player has entered the attack radius
            Debug.Log("Player entered attack radius: " + inAttackRadius);
            Attack(other);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            inAttackRadius = false; // Reset attack radius when player exits
            Debug.Log("Player exited attack radius: " + inAttackRadius);
        }
    }

    void Attack(Collider2D collision)
    {
        animator.SetTrigger("attack");
        if (inAttackRadius)
        {
            PlayerLifeManager playerLifeManager = player.transform.root.GetComponent<PlayerLifeManager>();
            Debug.Log("Player Life Manager found: " + (playerLifeManager != null));
            if (playerLifeManager != null)
            {
                Debug.Log("Attacking player");
                playerLifeManager.DamagePlayer(Vector2.up * 10f); // Adjust the launch force as needed
            }
        }
        Invoke("ResetAttack", 2.0f); // Adjust the delay as needed
    }

    void ResetAttack()
    {
        inAttackRadius = false;
    }
}
