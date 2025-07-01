using UnityEngine;

public class FoxScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Transform player;
    [SerializeField] public float minDistance = 100f;
    public float speed = 3f;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private bool isRunning = false;

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
    }

    // Update is called once per frame
    void Update()
    {
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            bool sameLevel = Mathf.Abs(transform.position.y - player.position.y) < 0.5f;

            if (distance < minDistance && sameLevel)
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
                direction.y = 0; // Ignore vertical movement
                transform.position += direction * speed * Time.deltaTime;

                if (!isRunning)
                {
                    animator.SetBool("isRunning", true);
                    isRunning = true;
                }
            }
            else
            {
                if (isRunning)
                {
                    animator.SetBool("isRunning", false);
                    isRunning = false;
                }
            }
        }
    }
}
