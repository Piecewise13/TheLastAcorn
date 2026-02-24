using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class GateCharge : MonoBehaviour
{

    private enum State
    {
        Idle,
        Collected,
        Return,
        Finished,
        Activated
    }

    [SerializeField]private State currentState = State.Idle;
    
    [SerializeField] private Gate gate;

    private Vector3 originalPosition;

    SpriteRenderer spriteRenderer;

    [SerializeField] private float collectSpeed = 10f;
    [SerializeField] private float returnSpeed = 15f;

    [SerializeField] private float maxScale = 1f;
    [SerializeField] private float minScale = 0.2f;

    private Vector2 gatePosition;

    [SerializeField] private Color activatedColor;

    [SerializeField] private Color idleColor;

    [SerializeField] private Color collectedColor;

    [SerializeField] private Color resetColor;

    [SerializeField] private float resetDuration = 1f;

    [SerializeField]private bool shouldActivateOnReturn = false;


    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        activatedColor = spriteRenderer.color;

        originalPosition = transform.position;

        gate.ResetCharges += ResetCharge;
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Idle:
                // Do nothing
                break;
            case State.Collected:
                MoveTowardsTarget(gatePosition);
                break;
            case State.Return:
                MoveTowardsTarget(originalPosition);
                break;
            case State.Activated:
                // Do nothing, wait for player to collect
                break;
        }
    }

    private void MoveTowardsTarget(Vector3 targetPosition)
    {
        if (Vector2.Distance(transform.position, targetPosition) < 0.01f)
        {
            if (currentState == State.Collected)
            {
                currentState = State.Finished;
                return;
            }
            else if (currentState == State.Return)
            {
                if (shouldActivateOnReturn)
                {
                    ActivateCharge();
                    return;
                }

                DeactivateCharge();
                return;
            }
        }

        bool isCollected = currentState == State.Collected;

        float scale = Mathf.Lerp(minScale, maxScale, Vector2.Distance(transform.position, gatePosition) / Vector2.Distance(gatePosition, originalPosition));


        float step = (isCollected ? collectSpeed : returnSpeed) * Time.deltaTime;
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, step);
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (currentState != State.Activated)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void Collect(){
        spriteRenderer.color = collectedColor;
        currentState = State.Collected;
        gate.Collect();
    }

    public void SetGatePosition(Vector2 position)
    {
        gatePosition = position;
    }

    public void ActivateCharge()
    {
        currentState = State.Activated;
        spriteRenderer.color = activatedColor;
    }

    public void DeactivateCharge()
    {
        currentState = State.Idle;
        spriteRenderer.color = idleColor;
    }

    public void ResetCharge()
    {
        print("Resetting gate charge.");
        spriteRenderer.color = resetColor;
        currentState = State.Return;
    }

    public bool isFinishedMovement()
    {
        return  currentState == State.Finished;
    }


}
