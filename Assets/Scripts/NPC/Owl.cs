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

    [SerializeField] private Transform playerAttachPoint;
    [SerializeField] private List<Transform> flightPathPoints;
    private int currentFlightPointIndex = 0;
    private Vector2 flightOrigin;

    [SerializeField] private float flightSpeed = 5f;

    [SerializeField] private float cameraZoomAmount = 5f;

    private OwlState currentState = OwlState.Idle;



    private void Awake()
    {
        playerMovementMap = new PlayerGameControls();
        owlAttachAction = playerMovementMap.Gameplay.OwlAttach;
        owlAttachAction.performed += AttachPlayerInput;
        
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        flightOrigin = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (currentState != OwlState.Flying)
        {
            return;
        }

        if (currentFlightPointIndex >= flightPathPoints.Count)
        {
            EndAttach();
            return;
        }

        Transform flightDestination = flightPathPoints[currentFlightPointIndex];

        // Move the owl towards the current flight path point
        if (Vector2.Distance(transform.position, flightDestination.position) < 0.1f)
        {
            currentFlightPointIndex++;
            if (currentFlightPointIndex >= flightPathPoints.Count)
            {
                EndAttach();
                return;
            }
            flightDestination = flightPathPoints[currentFlightPointIndex];
        }

        // Move the owl towards the flight destination
        transform.position = Vector2.MoveTowards(transform.position, flightDestination.position, Time.deltaTime * flightSpeed);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        print("in trigger");
        var root = other.transform.root;
        playerMovement = root.GetComponent<PlayerMove>();
        playerCamera = root.GetComponentInChildren<PlayerCamera>();
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
            playerCamera.StartForceZoom(cameraZoomAmount);
        }
    }

    void EndAttach()
    {
        playerMovement.DetachFromOwl();
        currentState = OwlState.Idle;
    }





    public void PlayerInProximity(GameObject player)
    {
        throw new System.NotImplementedException();
    }

    public void PlayerOutOfProximity(GameObject player)
    {
        throw new System.NotImplementedException();
    }

    enum OwlState {
        Idle,
        Alert,
        Flying,
        Returning
    }
}


