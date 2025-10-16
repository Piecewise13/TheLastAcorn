using Unity.VisualScripting;
using UnityEngine;

public class FoxScript : MonoBehaviour
{

    private Rigidbody2D rb;

    [SerializeField] private FoxState currentState = FoxState.Idle;

    private GameObject player;
    private PlayerMove playerMove;

    private bool isGrounded = false;


    [Header("Movement")]
    private GameObject moveTarget = null;

    [Tooltip("Force applied when the fox jumps.")]
    [SerializeField] private float jumpForce = 5f;

    [Tooltip("The y force to x force ratio when jumping.")]
    [SerializeField] private float jumpForceRatio = 1.2f;
    [SerializeField] private float airMoveSpeed = 1f;

    [Space(5)]
    [Header("Stalk")]
    [SerializeField] private float stalkMoveSpeed = 2f;
    [SerializeField] private float stalkDuration = 2f;
    private float stalkTimer = 0f;
    [SerializeField] private float playerDetectionDistance = 50f;

    [Space(5)]
    [Header("Chase")]
    [SerializeField] private float chaseMoveSpeed = 4f;
    [SerializeField] private float maxDistanceBetweenFoxAndPlayer = 70f;
    [SerializeField] private float pounceForce = 10f;


    [Space(20)]
    [Header("Front Raycast (Pathfinding)")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private GameObject groundCheck;
    [SerializeField] private float rayDistance = 1f;

    [SerializeField] private GameObject rayOriginObject;
    [SerializeField] private float rayAngle = 30f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

    }
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform.root.gameObject;
        playerMove = player.GetComponent<PlayerMove>();

        moveTarget = player;

        currentState = FoxState.Idle;
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            //if in idle state, search for target and return
            case FoxState.Idle:
                SearchForTarget();
                return;
            case FoxState.Jump:
                //do nothing, wait until grounded
                transform.position += transform.right * transform.localScale.x * Time.deltaTime * airMoveSpeed / 10;
                return;
            case FoxState.Pounce:
                return;

            case FoxState.Chase:
                ChaseTarget();
                break;
            case FoxState.Stalk:
                StalkTarget();
                break;
            case FoxState.GiveUp:
                ReturnToBush();
                break;

            default:
                return;
        }
    }

    void FixedUpdate()
    {
        GroundCheck();
    }

    /// <summary>
    /// Handles the horizontal movement of the fox.
    /// </summary>
    void Move()
    {
        float moveSpeed = (currentState == FoxState.Chase) ? chaseMoveSpeed : stalkMoveSpeed;
        transform.position += transform.right * transform.localScale.x * Time.deltaTime * moveSpeed / 10;
    }

    void Jump()
    {
        if (!isGrounded)
        {
            return;
        }

        if (currentState == FoxState.Jump)
        {
            return;
        }

        currentState = FoxState.Jump;

        rb.AddForce(new Vector2(Mathf.Sign(transform.localScale.x) * jumpForce, jumpForce * jumpForceRatio), ForceMode2D.Impulse);
    }

    void StartStalk()
    {
        currentState = FoxState.Stalk;
    }

    void StalkTarget()
    {
        if (currentState == FoxState.Stalk)
        {
            stalkTimer += Time.deltaTime;
            if (stalkTimer >= stalkDuration)
            {
                stalkTimer = 0f;
                StartChase();
            }
        }
        PathFinding();


        FaceTarget(player.transform.position);
        Move();
    }

    void StartChase()
    {
        currentState = FoxState.Chase;
    }

    void ChaseTarget()
    {
        if (Vector2.Distance(transform.position, player.transform.position) > maxDistanceBetweenFoxAndPlayer)
        {
            GiveUp();
            return;
        }

        PathFinding();



        //Check if the player is above us and within range to pounce
        if (Mathf.Abs(player.transform.position.x - transform.position.x) < 1f && player.transform.position.y > transform.position.y + 2f)
        {
            currentState = FoxState.Pounce;
            Vector2 direction = (player.transform.position - transform.position).normalized;
            rb.AddForce(direction * pounceForce, ForceMode2D.Impulse);
            return;
        }


        FaceTarget(player.transform.position);
        Move();
    }

    void ReturnToBush()
    {
        if (Vector2.Distance(transform.position, moveTarget.transform.position) < 1f)
        {
            FoxBush.ResetFoxSpawn();
            Destroy(gameObject);
            return;
        }
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

            Vector2 origin = (Vector2)rayOriginObject.transform.position;

            hits[i] = Physics2D.Raycast(origin, rayDirection, rayDistance, obstacleMask);
            Debug.DrawRay(origin, rayDirection * rayDistance, hits[i].collider != null ? Color.red : Color.green);

        }

        if (hits[0].collider != null && hits[1].collider == null)
        {
            Jump();
            return;
        }

        if (hits[0].collider != null && hits[1].collider != null)
        {
            currentState = FoxState.Idle;
            return;
        }

    }

    void GroundCheck()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.transform.position, 0.1f, obstacleMask);

        if (isGrounded && currentState == FoxState.Jump)
        {
            currentState = FoxState.Chase;
            return;
        }

        if (isGrounded && currentState == FoxState.Pounce)
        {
            GiveUp();
            return;
        }


    }
    void SearchForTarget()
    {
        if (Vector2.Distance(transform.position, player.transform.position) > playerDetectionDistance)
        {
            return;
        }


        // if (Physics2D.Linecast(transform.position, player.transform.position, obstacleMask))
        // {
        //     return;
        // }

        if (playerMove.GetPlayerState() == PlayerMove.PlayerState.Grounded)
        {
            StartStalk();
        }
    }


    void GiveUp()
    {
    
        currentState = FoxState.GiveUp;
        //make the move target a bush
    }

    enum FoxState
    {
        Idle,
        Jump,
        Pounce,
        Stalk,
        Chase,
        Attack,
        GiveUp
    }
}
