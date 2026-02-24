using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gate : MonoBehaviour
{



    private static PlayerMove playerMove;
    public event Action ResetCharges;

    private Animator animator;

    private int numChargeCollected = 0;

    float groundCheckInterval = 0.1f;

    [SerializeField] List<GateCharge> gateCharges = new List<GateCharge>();

    [SerializeField] Transform gateTop;
    [SerializeField] float gateChargeSpeed = 5f;

    [SerializeField] private float radialDistance = 2f;
    [SerializeField] private float chargeTargetScale = 0.5f;
    [SerializeField] private float gateOpenDuration = 2f;


    private void Start()
    {
        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>();
        }

        animator = GetComponent<Animator>();

        for (int i = 0; i < gateCharges.Count; i++)
        {
        
        float angle = 22.5f;

        int numSpotsOnHalf = gateCharges.Count / 2;

        float startingAngle = angle * (numSpotsOnHalf);

        Vector2 dirToSpot = 
        Quaternion.Euler(0, 0, startingAngle - (angle * i)) * Vector2.up;
        Vector3 offset = dirToSpot * radialDistance;

        gateCharges[i].SetGatePosition(gateTop.position + offset);

            if(i == 0){
                gateCharges[i].ActivateCharge();
                continue;
            }

            gateCharges[i].DeactivateCharge();
        }
    }

    void Update()
    {
        if(numChargeCollected == 0 && playerMove != null){
            return;
        }

        if(numChargeCollected >= gateCharges.Count){
            return;
        }
        
        if (playerMove.GetPlayerState() == PlayerMove.PlayerState.Grounded)
        {
           print("Player grounded, resetting gate charges." + ResetCharges.GetInvocationList().Length);
            numChargeCollected = 0;
            ResetCharges?.Invoke();
        }
    }

/// <summary>
/// Collects a gate charge and moves it to the gate.
/// <returns>  The target position where the gate charge will move to.</returns>
/// </summary>
    public void Collect()
    {

        numChargeCollected++;

        if (numChargeCollected >= gateCharges.Count)
        {
            OpenGate();
            return;
        }

        gateCharges[numChargeCollected].ActivateCharge();
    }

    private void OpenGate()
    {
        StartCoroutine(OpenGateSequence());
    }

    private IEnumerator OpenGateSequence()
    {
        // Wait for the last charge to finish its collection animation
        GateCharge lastCharge = gateCharges[gateCharges.Count - 1];
        while (!lastCharge.isFinishedMovement())
        {
            yield return null;
        }

        // Move all charges towards the gate top
        List<Coroutine> moveCoroutines = new List<Coroutine>();
        
        foreach (GateCharge gateCharge in gateCharges)
        {
            moveCoroutines.Add(StartCoroutine(MoveChargeToGateTop(gateCharge.transform)));
        }

        // Wait for all charges to reach the gate top
        foreach (Coroutine coroutine in moveCoroutines)
        {
            yield return coroutine;
        }

        // Trigger the gate animation
        animator.SetBool("isActive", true);
        
        // Delete the gate charges
        foreach (GateCharge gateCharge in gateCharges)
        {
            Destroy(gateCharge.gameObject);
        }
    }

    private IEnumerator MoveChargeToGateTop(Transform chargeTransform)
    {
        Vector3 startPosition = chargeTransform.position;
        Vector3 targetPosition = gateTop.position;
        float elapsed = 0f;

        while (elapsed < gateOpenDuration)
        {
            float t = elapsed / gateOpenDuration;
            chargeTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        chargeTransform.position = targetPosition;
    }
}
