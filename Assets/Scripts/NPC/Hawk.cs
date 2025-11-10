using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Hawk : ResetOnDeathObject, IProximityAlert
{
    private enum HawkState
    {
        Idle,
        Returning,
        Chase,
        GoingToDiveStart,
        Targeting,
        DiveBombing
    }

    [SerializeField] private HawkState currentState;

    [SerializeField] private Transform nest;

    [SerializeField] private float damageRadius = 1f;

    [SerializeField] private LayerMask playerLayer;

    [Header("Flight Settings")]
    [SerializeField] private float defaultFlightSpeed;
    [SerializeField] private float flightSpeed;
    [SerializeField] private float acceleration;

    private float targetFlightSpeed;

    [SerializeField] private float maxNestDistance;
    private Vector2 moveDirection;
    private Vector2 previousMoveDirection;

    [SerializeField] private float turnSpeed = 2f;

    [SerializeField] private float minTurnSpeed, maxTurnSpeed;
    
    [Header("Turn Detection")]
    [SerializeField] private float straightLineSpeedMultiplier = 1.5f;
    [SerializeField] private float turnSpeedMultiplier = 0.7f;
    [SerializeField] private float turnThreshold = 0.1f; // How much direction change counts as a turn

    [Header("Targeting Settings")]
    [SerializeField] private float targetingDuration;
    private float targetingTimer;
    private bool hasTargetingPos = false;
    private Vector3 targetingStartPosition;
    [SerializeField] private float maxTargetShake;
    private Vector3 targetingBasePosition;

    [Header("Chase Settings")]
    [SerializeField] private float chaseSpeed;

    private Queue<Vector3> recentPlayerPositions = new Queue<Vector3>();
    private int positionUpdateTickRate = 20;
    private int currentTick = 0;

    [SerializeField] private float maxChaseTime = 10f;
    private float currentChaseTime = 0f;


    [Header("Dive Settings")]
    [SerializeField] private float diveAttackSpeed;
    private Vector3 diveTarget;
    private GameObject player;
    private PlayerLifeManager playerLifeManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    new void Start()
    {
        base.Start();
        player = GameObject.FindGameObjectWithTag("Player").transform.root.gameObject;
        playerLifeManager = player.GetComponent<PlayerLifeManager>();
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            case HawkState.Idle:
                targetFlightSpeed = defaultFlightSpeed;
                break;
            case HawkState.Returning:
                targetFlightSpeed = defaultFlightSpeed;
                ReturnToNest();
                break;
            case HawkState.Chase:
                ChasePlayer();
                CheckForPlayerCollision();
                break;
            case HawkState.DiveBombing:
                // Implement diving behavior
                flightSpeed = diveAttackSpeed;
                targetFlightSpeed = diveAttackSpeed;
                DiveBombPlayer();
                CheckForPlayerCollision();
                break;
            case HawkState.GoingToDiveStart:
                targetFlightSpeed = defaultFlightSpeed;
                MoveTowardsDiveStart();
                CheckForPlayerCollision();
                break;
            case HawkState.Targeting:
                TargetPlayer();
                break;

        }
    }

    private void FlyTowardsTarget(Vector3 target)
    {
        Vector2 targetDirection = (target - transform.position).normalized;
        
        // Detect if we're turning by comparing current and target direction
        float directionChange = Vector2.Dot(moveDirection, targetDirection);
        bool isTurning = directionChange < (1f - turnThreshold);
        
        // Adjust target speed based on whether we're turning or going straight
        float speedModifier = isTurning ? turnSpeedMultiplier : straightLineSpeedMultiplier;
        float adjustedTargetSpeed = targetFlightSpeed * speedModifier;
        
        // Accelerate/decelerate to target speed
        flightSpeed = Mathf.Lerp(flightSpeed, adjustedTargetSpeed, acceleration * Time.deltaTime);
        
        // Adjust turn speed based on whether we're turning
        float currentTurnSpeed = isTurning ? maxTurnSpeed : minTurnSpeed;
        turnSpeed = Mathf.Lerp(minTurnSpeed, maxTurnSpeed, flightSpeed / chaseSpeed);
        
        // Update movement direction
        previousMoveDirection = moveDirection;
        moveDirection = Vector2.Lerp(moveDirection, targetDirection, currentTurnSpeed * Time.deltaTime).normalized;
        
        // Move the hawk
        transform.position = moveDirection * flightSpeed * Time.deltaTime + (Vector2)transform.position;
    }

    private void ReturnToNest()
    {

        FlyTowardsTarget(nest.position);
        float distanceToNest = Vector3.Distance(transform.position, nest.position);
        if (distanceToNest <= 2f)
        {
            transform.position = nest.position;
            currentState = HawkState.Idle;
        }
    }

    private void StartChase()
    {
        currentState = HawkState.Chase;

        currentChaseTime = 0f;
    }

    private void ChasePlayer()
    {
        if (currentChaseTime >= maxChaseTime)
        {
            StartDiveBomb();
            return;
        }
        currentChaseTime += Time.deltaTime;

        if (Vector2.Distance(transform.position, nest.transform.position) > maxNestDistance)
        {
            ReturnToNest();
            return;
        }


        targetFlightSpeed = chaseSpeed;
        FlyTowardsTarget(player.transform.position);
    }


    #region Dive Attack Methods

    private void StartDiveBomb()
    {
        targetingSetupTimer = 0;
        targetFlightSpeed = defaultFlightSpeed;
        currentState = HawkState.GoingToDiveStart;
        
        // Initialize circle angle to current position relative to player
        Vector3 directionToHawk = transform.position - player.transform.position;
        circleAngle = Mathf.Atan2(directionToHawk.y, directionToHawk.x);

    }
    private void DiveBombPlayer()
    {
        FlyTowardsTarget(diveTarget);

        if (Vector2.Distance(transform.position, diveTarget) <= 1f)
        {
            EndDiveAttack();
            return;
        }

    }

    private void EndDiveAttack()
    {
        if (Vector3.Distance(transform.position, nest.position) > maxNestDistance)
        {
            currentState = HawkState.Returning;
            return;
        }

        StartChase();

        /*
        SetDiveStartLocation();
        currentState = HawkState.GoingToDiveStart;
        */

    }


    private void TargetPlayer()
    {
        if (targetingTimer >= targetingDuration)
        {
            currentState = HawkState.DiveBombing;

            targetingTimer = 0f;

            ResetTargetingShake();
            return;
        }

        if (Vector2.Distance(nest.position, player.transform.position) > maxNestDistance * 1.5f)
        {
            currentState = HawkState.Returning;
            return;
        }

        moveDirection = (player.transform.position - transform.position).normalized;

        diveTarget = (player.transform.position - transform.position) * 1.25f + transform.position;
        targetingTimer += Time.deltaTime;
        ShakeBasedOnTargeting();
    }

    private void ShakeBasedOnTargeting()
    {
        if (targetingDuration <= 0f) return;

        float ratio = Mathf.Clamp01(targetingTimer / targetingDuration); // 0..1
        float intensity = maxTargetShake * ratio; // scales shake by timer/duration

        // 2D shake on X and Y only
        Vector3 shakeOffset = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            0f
        ) * intensity;

        transform.position = targetingBasePosition + shakeOffset;
    }

    private void ResetTargetingShake()
    {
        transform.position = targetingBasePosition;
    }

    Vector3 targetingOffset;
    [SerializeField] private float targetingSetupDuration = 5f;
    [SerializeField] private float targetingSetupTimer = 0f;
    
    [Header("Circling Settings")]
    [SerializeField] private float circleRadius = 15f;
    [SerializeField] private float circleHeight = 15f;
    [SerializeField] private float circleSpeed = 2f;
    private float circleAngle = 0f;


    private void MoveTowardsDiveStart()
    {
        if (targetingSetupDuration < targetingSetupTimer)
        {
            currentState = HawkState.Targeting;
            targetingBasePosition = transform.position;
            return;
        }

        targetingSetupTimer += Time.deltaTime;
        
        // Update circle angle for circular motion
        circleAngle += circleSpeed * Time.deltaTime;
        
        // Calculate circular position around player
        Vector3 circleOffset = new Vector3(
            Mathf.Cos(circleAngle) * circleRadius,
            Mathf.Sin(circleAngle) * circleRadius + circleHeight,
            0f
        );
        
        Vector3 targetPosition = player.transform.position + circleOffset;

        // Implement going to dive start behavior
        FlyTowardsTarget(targetPosition);
    }

    #endregion

    private void CheckForPlayerCollision()
    {

        Collider2D hit = Physics2D.OverlapCircle(transform.position, damageRadius, playerLayer);
        if (hit != null)
        {
            // handle player hit (apply damage or notify manager). Using SendMessage so there's no compile-time dependency on a specific method name.
            playerLifeManager.DamagePlayer();
            return;
        }
    }

    public void PlayerInProximity(GameObject player)
    {
        if (currentState != HawkState.Idle)
        {
            return;
        }

        StartDiveBomb();
    }

    public void PlayerOutOfProximity(GameObject player)
    {
        throw new System.NotImplementedException();
    }

    public override void ResetObject()
    {
        currentState = HawkState.Idle;
        transform.position = nest.position;
        flightSpeed = 0f;
        moveDirection = Vector2.zero;
        previousMoveDirection = Vector2.zero;
    }
}
