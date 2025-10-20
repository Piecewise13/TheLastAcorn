using System;
using Unity.VisualScripting;
using UnityEngine;

public class FoxScript : MonoBehaviour, IProximityAlert
{

    private Rigidbody2D rb;

    [SerializeField] private Animator animator;

    [SerializeField] private FoxState currentState = FoxState.Idle;


    private GameObject player;
    private PlayerMove playerMove;
    private PlayerLifeManager playerLifeManager;

    private bool isGrounded = false;

    [SerializeField] private float lifeTime = 20f;
    private float lifeTimer = 0f;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 2f;
    private float lastAttackTime = 0f;



    [Header("Movement")]
    private GameObject moveTarget = null;

    [Tooltip("Force applied when the fox jumps.")]
    [SerializeField] private float jumpForce = 5f;

    [Tooltip("The y force to x force ratio when jumping.")]
    [SerializeField] private float jumpForceRatio = 1.2f;
    [SerializeField] private float airMoveSpeed = 1f;

    private float jumpCooldown = 0.25f;
    private float jumpCooldownTimer = 0f;

    private float jumpBufferTimer = 0f;
    private float jumpBufferDuration = 0.2f;

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
    [SerializeField] private float pounceDuration = 3f;
    private float waitTimer = 0f;


    [Space(20)]
    [Header("Front Raycast (Pathfinding)")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private GameObject[] groundCheck;
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
        playerLifeManager = player.GetComponent<PlayerLifeManager>();

        moveTarget = player;

        currentState = FoxState.Idle;

        SearchForTarget();
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            //if in idle state, search for target and return
            case FoxState.Idle:
                if (waitTimer < pounceDuration)
                {

                    FaceTarget(player.transform.position);
                    waitTimer += Time.deltaTime;
                    return;
                }
                GiveUp();
                return;
            case FoxState.Jump:
                //do nothing, wait until grounded
                transform.position += transform.right * transform.localScale.x * Time.deltaTime * airMoveSpeed / 10;
                return;

            case FoxState.Attack:
                if (lastAttackTime + attackCooldown < Time.time)
                {
                    StartStalk();
                }
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

    void StartJump()
    {

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

        jumpBufferTimer = 0f;
        animator.SetBool("isJumping", true);

        RaycastHit2D hit = Physics2D.Raycast(rayOriginObject.transform.position, (Vector2)transform.right * Mathf.Sign(transform.localScale.x), rayDistance, obstacleMask);


        float verticalForce = Mathf.Lerp(1f, 3f, hit.distance / rayDistance);
        currentState = FoxState.Jump;

        Vector2 forceDirection = Vector2.up * verticalForce + Vector2.right * Mathf.Sign(transform.localScale.x);

        //Mathf.Sign(transform.localScale.x) * jumpForce
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    void StartStalk()
    {
        currentState = FoxState.Stalk;
        animator.SetBool("isStalking", true);
        animator.SetBool("isChasing", false);
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
        animator.SetBool("isChasing", true);
        animator.SetBool("isStalking", false);
    }

    void ChaseTarget()
    {
        if (Vector2.Distance(transform.position, moveTarget.transform.position) > maxDistanceBetweenFoxAndPlayer)
        {
            GiveUp();
            return;
        }

        PathFinding();



        //Check if the player is above us and within range to pounce
        if (Mathf.Abs(player.transform.position.x - transform.position.x) < 1f && player.transform.position.y > transform.position.y + 2f)
        {
            Pounce();
            return;
        }


        FaceTarget(player.transform.position);
        Move();
    }

    void Pounce()
    {
        jumpBufferTimer = 0f;
        animator.SetBool("isJumping", true);
        waitTimer = 0f;
        currentState = FoxState.Pounce;
        Vector2 direction = (player.transform.position - transform.position).normalized;
        rb.AddForce(direction * pounceForce, ForceMode2D.Impulse);
    }

    void ReturnToBush()
    {
        animator.SetBool("isStalking", true);
        if (Vector2.Distance(transform.position, moveTarget.transform.position) < 4f || lifeTimer <= 0f)
        {

            FoxBush.ResetFoxSpawn();
            Destroy(gameObject);
            return;
        }
        
        lifeTimer -= Time.deltaTime;

        FaceTarget(moveTarget.transform.position);
        PathFinding();
        Move();
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
            if (jumpCooldownTimer > 0)
            {
                jumpCooldownTimer -= Time.deltaTime;
                return;
            }

            Jump();
            return;
        }

        if (hits[0].collider != null && hits[1].collider != null)
        {
            GiveUp();
            return;
        }

    }

    void GroundCheck()
    {
        if (jumpBufferTimer < jumpBufferDuration)
        {
            jumpBufferTimer += Time.deltaTime;
            return;
        }

        isGrounded = false;
        foreach (var gc in groundCheck)
        {
            if (gc == null) continue;
            if (Physics2D.OverlapCircle(gc.transform.position, 0.1f, obstacleMask))
            {
                isGrounded = true;
                break;
            }
        }

        if (!isGrounded)
        {
            return;
        }

        print("Landed");

        if (currentState == FoxState.Jump)
        {
            animator.SetBool("isJumping", false);

            jumpCooldownTimer = jumpCooldown;

            if (stalkTimer < stalkDuration)
            {
                StartStalk();
                print("Landed from Jump to Stalk");
                return;
            }

            StartChase();

            return;
        }

        if (currentState == FoxState.Pounce)
        {
            currentState = FoxState.Idle;
            print("Landed from Pounce");
            animator.SetBool("isChasing", false);
            animator.SetBool("isStalking", false);
            animator.SetBool("isJumping", false);

        }


    }
    void SearchForTarget()
    {
        if (Vector2.Distance(transform.position, player.transform.position) > playerDetectionDistance)
        {
            GiveUp();
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

    public void PlayerLandedOnGround()
    {
        if (currentState == FoxState.Idle)
        {
            StartChase();
            return;
        }

        if (currentState == FoxState.GiveUp)
        {
            SearchForTarget();
            return;
        }
    }


    void GiveUp()
    {
        animator.SetBool("isChasing", false);
        currentState = FoxState.GiveUp;
        var closestBushes = FoxBush.GetClosestBushes(transform.position);

        lifeTimer = lifeTime;

        if (closestBushes.Length == 0)
        {
            // No bushes found, just destroy the fox
            FoxBush.ResetFoxSpawn();
            Destroy(gameObject);
        }

        //make the move target a bush
        moveTarget = closestBushes[0].transform.gameObject;
    }
    
    private void Attack()
    {
        currentState = FoxState.Attack;
        animator.SetTrigger("attack");
        lastAttackTime = Time.time; 
        playerLifeManager.DamagePlayer();
    }

    public void PlayerInProximity(GameObject player)
    {
        if (attackCooldown + lastAttackTime < Time.time)
        {
            Attack();
            return;
        }
        
    }

    public void PlayerOutOfProximity(GameObject player)
    {
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
