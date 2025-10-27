using UnityEngine.InputSystem;
using UnityEngine;
using System.Collections.Generic;

public class Owl : MonoBehaviour, IProximityAlert
{

    private PlayerGameControls playerMovementMap;

    /// <summary>
    /// Input action for player movement.
    /// </summary>
    private InputAction owlAttachAction;


    private PlayerMove playerMovement;

    private PlayerCamera playerCamera;

    private Animator anim;

    [SerializeField] private Transform playerAttachPoint;
    [SerializeField] private List<Transform> flightPathPoints;
    private int currentFlightPointIndex = 1;
    private Vector2 flightOrigin;

    [SerializeField] private float flightSpeed = 5f;

    private float initialDistanceToPoint;
    private Vector3 initalFlightDirection;

    private bool isReturningToStart = false;

    [SerializeField] private float cameraZoomAmount = 5f;

    [SerializeField]private OwlState currentState = OwlState.Idle;



    private void Awake()
    {
        playerMovementMap = new PlayerGameControls();
        owlAttachAction = playerMovementMap.Gameplay.Interact;
        owlAttachAction.performed += AttachPlayerInput;

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        flightOrigin = transform.position;
        anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (currentState != OwlState.Flying)
        {
            return;
        }


        Transform flightDestination = flightPathPoints[currentFlightPointIndex];

        float distanceToNextPoint = Vector2.Distance(transform.position, flightDestination.position);



        // Interpolate the owl's forward direction towards the next flight point
        Vector2 directionToNextPoint = ((Vector2)flightDestination.position - (Vector2)transform.position).normalized;


        Vector3 moveDirection = Vector3.Lerp(initalFlightDirection, directionToNextPoint, Mathf.Clamp((initialDistanceToPoint - distanceToNextPoint) / initialDistanceToPoint, 0.05f, 1f));




        // Move the owl towards the flight destination
        transform.position = Vector2.MoveTowards(transform.position, flightDestination.position, Time.deltaTime * flightSpeed);

        if (!isReturningToStart) {
            playerMovement.transform.localPosition = playerAttachPoint.localPosition;
        }

        // Move the owl towards the current flight path point

        if (distanceToNextPoint < 0.1f)
        {
            currentFlightPointIndex = isReturningToStart ? currentFlightPointIndex - 1 : currentFlightPointIndex + 1;
            
            if (currentFlightPointIndex >= flightPathPoints.Count)
            {
                EndAttach();
                return;
            }

            if (currentFlightPointIndex < 0)
            {
                isReturningToStart = false;
                currentState = OwlState.Idle;

                transform.position = flightOrigin; // Return to the origin point
                currentFlightPointIndex = 1; // Reset to the first flight point after returning
                return;
                
            }

            flightDestination = flightPathPoints[currentFlightPointIndex];

        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {


        var root = other.transform.root;

        if (playerMovement == null || playerCamera == null)
        {
            playerMovement = root.GetComponent<PlayerMove>();
            playerCamera = root.GetComponentInChildren<PlayerCamera>();
        }
        owlAttachAction.Enable();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        owlAttachAction.Disable();

    }

    void AttachPlayerInput(InputAction.CallbackContext context)
    {
        if (playerMovement != null)
        {
            playerMovement.transform.SetParent(playerAttachPoint);
            playerMovement.transform.position = playerAttachPoint.position;

            playerMovement.AttachToOwl();
            currentState = OwlState.Flying;

            initialDistanceToPoint = Vector2.Distance(flightOrigin, flightPathPoints[currentFlightPointIndex].position);
            initalFlightDirection = transform.up;

            playerCamera.StartForceZoom(cameraZoomAmount, PlayerCamera.CameraState.GlideZoom);
        }
    }

    void EndAttach()
    {
        isReturningToStart = true;
        playerMovement.DetachFromOwl();
        currentFlightPointIndex = flightPathPoints.Count - 2; // Start returning to the last point
        //currentState = OwlState.Idle;
    }

    public void PlayerInProximity(GameObject player)
    {
        currentState = OwlState.Alert;
        anim.SetBool("isAlert", true);
    }

    public void PlayerOutOfProximity(GameObject player)
    {
        anim.SetBool("isAlert", false);
    }

    public void ReturnToStart()
    {
        
    }

    enum OwlState
    {
        Idle,
        Alert,
        Flying,
        Returning
    }
}


