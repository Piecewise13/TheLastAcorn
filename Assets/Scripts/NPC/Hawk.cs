using UnityEngine;

public class Hawk : MonoBehaviour, IProximityAlert
{
    //DEBUG ONLY
    private enum BehaviorType
    {
        Chase,
        Dive,
        ChaseDelayed
    }
    private enum HawkState
    {
        Idle,
        Returning,
        Attacking
    }

    [Header("Debug Only")]
    [SerializeField] private BehaviorType behaviorType;

    [SerializeField] private HawkState currentState;

    [SerializeField] private Transform nest;

    [Header("Flight Settings")]
    [SerializeField] private float maxFlightSpeed;
    [SerializeField] private float minFlightSpeed;

    [SerializeField] private float acceleration;
    [SerializeField] private float flightSpeed;
    [SerializeField] private float maxNestDistance;
    [SerializeField] private float diveSpeed;

    private GameObject flightTarget;

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
                ReturnToNest();
                break;
            case HawkState.Attacking:
                flightSpeed = Mathf.Clamp(flightSpeed + Time.deltaTime * acceleration, minFlightSpeed, maxFlightSpeed);
                // Implement attacking behavior
                AttackPlayer();
                break;
        }
    }

    private void FlyTowardsTarget(GameObject target)
    {
        transform.position = Vector3.MoveTowards(transform.position, target.transform.position, flightSpeed * Time.deltaTime);
    }

    private void ReturnToNest()
    {

        FlyTowardsTarget(nest.gameObject);
        float distanceToNest = Vector3.Distance(transform.position, nest.position);
        if (distanceToNest <= 0.1f)
        {
            currentState = HawkState.Idle;
        }
    }

    private void AttackPlayer()
    {
        FlyTowardsTarget(player);
        if (Vector3.Distance(transform.position, player.transform.position) <= 0.1f)
        {
            playerLifeManager.DamagePlayer();
            currentState = HawkState.Returning;
            return;
        }

        if(Vector3.Distance(transform.position, nest.position) > maxNestDistance)
        {
            currentState = HawkState.Returning;
            return;
        }
    }

    public void PlayerInProximity(GameObject player)
    {
        flightTarget = player;
        currentState = HawkState.Attacking;

    }

    public void PlayerOutOfProximity(GameObject player)
    {
        throw new System.NotImplementedException();
    }
}
