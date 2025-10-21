using UnityEngine;

public class Hawk : MonoBehaviour, IProximityAlert
{
    private enum HawkState
    {
        Idle,
        Returning,
        GoingToDiveStart,
        Targeting,
        Attacking
    }

    [SerializeField] private HawkState currentState;

    [SerializeField] private Transform nest;

    [Header("Flight Settings")]
    [SerializeField] private float defaultFlightSpeed;
    [SerializeField] private float minFlightSpeed;
    [SerializeField] private float flightSpeed;
    [SerializeField] private float maxNestDistance;

    [Header("Targeting Settings")]
    [SerializeField] private float targetingDuration;
    private float targetingTimer;
    private bool hasTargetingPos = false;
    private Vector3 setTargetingPosition;
    [SerializeField] private float maxTargetShake;
    private Vector3 targetingBasePosition;


    [Header("Dive Settings")]
    [SerializeField] private float diveAttackSpeed;
    private Vector3 diveTarget;
    private GameObject player;
    private PlayerLifeManager playerLifeManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform.root.gameObject;
        playerLifeManager = player.GetComponent<PlayerLifeManager>();
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            case HawkState.Idle:
                flightSpeed = minFlightSpeed;
                break;
            case HawkState.Returning:
                flightSpeed = defaultFlightSpeed;
                ReturnToNest();
                break;
            case HawkState.Attacking:
                // Implement attacking behavior
                flightSpeed = diveAttackSpeed;
                AttackPlayer();
                break;
            case HawkState.GoingToDiveStart:
                flightSpeed = defaultFlightSpeed;
                MoveTowardsDiveStart();
                break;
            case HawkState.Targeting:
                TargetPlayer();
                break;

        }
    }

    private void FlyTowardsTarget(Vector3 target)
    {

        transform.position = Vector3.MoveTowards(transform.position, target, flightSpeed * Time.deltaTime);
    }

    private void ReturnToNest()
    {

        FlyTowardsTarget(nest.position);
        float distanceToNest = Vector3.Distance(transform.position, nest.position);
        if (distanceToNest <= 0.1f)
        {
            currentState = HawkState.Idle;
        }
    }

    private void AttackPlayer()
    {
        FlyTowardsTarget(diveTarget);

        float overlapRadius = 0.5f;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, overlapRadius);
        if (hit != null && (hit.gameObject == player || hit.CompareTag("Player")))
        {
            // handle player hit (apply damage or notify manager). Using SendMessage so there's no compile-time dependency on a specific method name.
            playerLifeManager.DamagePlayer();
            EndDiveAttack();
            return;
        }

        if (Vector2.Distance(transform.position, diveTarget) <= 0.1f)
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

        SetDiveStartLocation();
        currentState = HawkState.GoingToDiveStart;
    }

    private void TargetPlayer()
    {
        if (targetingTimer >= targetingDuration)
        {
            currentState = HawkState.Attacking;

            targetingTimer = 0f;
            flightSpeed = diveAttackSpeed;

            ResetTargetingShake();
            return;
        }
        diveTarget = player.transform.position;
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

    private void SetDiveStartLocation()
    {

        if (hasTargetingPos)
        {
            return;
        }

        setTargetingPosition = player.transform.position + new Vector3(Random.Range(10f, 10f), 20f, 0);
        hasTargetingPos = true;
        flightSpeed = minFlightSpeed;
    }

    private void MoveTowardsDiveStart()
    {
        // Implement going to dive start behavior
        FlyTowardsTarget(setTargetingPosition);
        if (Vector3.Distance(transform.position, setTargetingPosition) <= 0.1f)
        {
            currentState = HawkState.Targeting;
            targetingBasePosition = transform.position;
            hasTargetingPos = false;
        }
    }

    public void PlayerInProximity(GameObject player)
    {
        if (currentState != HawkState.Idle)
        {
            return;
        }

        currentState = HawkState.GoingToDiveStart;
        SetDiveStartLocation();

    }

    public void PlayerOutOfProximity(GameObject player)
    {
        throw new System.NotImplementedException();
    }
}
