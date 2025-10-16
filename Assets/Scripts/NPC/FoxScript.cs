using Unity.VisualScripting;
using UnityEngine;

public class FoxScript : MonoBehaviour
{

    private Rigidbody2D rb;

    [SerializeField]private FoxState currentState = FoxState.Idle;

    private GameObject player;

    private bool isGrounded = false;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;

    [SerializeField] private float jumpForce = 5f;

    [Header("Front Raycast (Pathfinding)")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private GameObject groundCheck;
    [SerializeField] private float rayDistance = 1f;

    [SerializeField] private GameObject rayOriginObject;
    [SerializeField] private float rayAngle;
    [SerializeField] private int rayCount = 3;
    [SerializeField] private float raySpread = 0.5f; // spread along the object's up axis

    private bool obstacleAhead = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        currentState = FoxState.Chase;
    }

    // Update is called once per frame
    void Update()
    {

        if(currentState == FoxState.Jump)
        {
            return;
        }

        if (currentState == FoxState.Chase)
        {
            PathFinding();
        }

        FaceTarget(player.transform.position);
        Move();
    }

    void FixedUpdate()
    {
        GroundCheck();

    }

    void Move()
    {
        transform.position += transform.right * transform.localScale.x * Time.deltaTime * moveSpeed / 10;
    }

    void FaceTarget(Vector2 targetPosition)
    {
        if (targetPosition.x < transform.position.x)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    void StartJump()
    {
        currentState = FoxState.Jump;

        rb.AddForce(new Vector2(Mathf.Sign(transform.localScale.x) * jumpForce, jumpForce * 2), ForceMode2D.Impulse);
    }

    void PathFinding()
    {
        // Determine forward direction considering possible flipped scale
        Vector2 forward = (Vector2)transform.right * Mathf.Sign(transform.localScale.x);

        RaycastHit2D[] hits = new RaycastHit2D[2];

        // Ray origins are spread along the local up axis
        for (int i = 0; i < 2; i++)
        {
            float angleDeg = (i == 0) ? 0f : Mathf.Sign(transform.localScale.x) * rayAngle;
            Vector2 rayDirection = (Vector2)(Quaternion.Euler(0f, 0f, angleDeg) * (Vector3)forward);

            Vector2 origin = (Vector2)transform.position;

            hits[i] = Physics2D.Raycast(rayOriginObject.transform.position, rayDirection, rayDistance, obstacleMask);
            Debug.DrawRay(origin, rayDirection * rayDistance, hits[i].collider != null ? Color.red : Color.green);

        }

        if (hits[0].collider != null && hits[1].collider == null)
        {
            StartJump();
            return;
        }

    }

    void GroundCheck()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.transform.position, 0.1f, obstacleMask);

        if(isGrounded && currentState == FoxState.Jump)
        {
            currentState = FoxState.Chase;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {

    }

    void OnTriggerExit2D(Collider2D other)
    {

    }

    void GetTargetPoint()
    {
        
    }

    void StartStalk()
    {
        currentState = FoxState.Stalk;
    }

    void StartChase()
    {
        currentState = FoxState.Chase;
    }

    void Attack(Collider2D collision)
    {

    }

    void ResetAttack()
    {

    }



    enum FoxState
    {
        Idle,
        Jump,
        Stalk,
        Chase,
        Attack,
    }
}
