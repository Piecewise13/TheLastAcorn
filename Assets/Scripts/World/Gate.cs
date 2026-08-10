using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using UnityEngine;

public class Gate : MonoBehaviour
{
    private static PlayerMove playerMove;
    public event Action ResetCharges;

    private int numChargeCollected = 0;

    float groundCheckInterval = 0.1f;

    [SerializeField] List<GateCharge> gateCharges = new List<GateCharge>();

    [SerializeField] Transform gateTop;
    [SerializeField] float gateChargeSpeed = 5f;

    [SerializeField] private float radialDistance = 2f;
    [SerializeField] private float chargeTargetScale = 0.5f;
    [SerializeField] private float gateOpenDuration = 2f;

    [Header("Feedbacks")] [SerializeField] private MMF_Player activateFeedback;


    private void Start()
    {
        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>();
        }


        for (int i = 0; i < gateCharges.Count; i++)
        {

            float angle = 22.5f;

            int numSpotsOnHalf = gateCharges.Count / 2;

            float startingAngle = angle * (numSpotsOnHalf);

            Vector2 dirToSpot =
                Quaternion.Euler(0, 0, startingAngle - (angle * i)) * Vector2.up;
            Vector3 offset = dirToSpot * radialDistance;

            gateCharges[i].SetGatePosition(gateTop.position + offset);
            gateCharges[i].ActivateCharge();
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
        
        if (playerMove.GetPlayerState() == PlayerStateManager.PlayerState.Grounded)
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
        }
    }

    private void OpenGate()
    {
        OpenGateSequence(destroyCancellationToken).Forget();
    }

    private async UniTask OpenGateSequence(CancellationToken cancellationToken)
    {
        // Charges can be collected in any order, so wait on all of them rather than the last in the list
        await UniTask.WaitUntil(AllChargesFinishedMovement, cancellationToken: cancellationToken);

        // Move all charges towards the gate top
        List<UniTask> moveTasks = new List<UniTask>();

        foreach (GateCharge gateCharge in gateCharges)
        {
            moveTasks.Add(MoveChargeToGateTop(gateCharge.transform, cancellationToken));
        }

        // Wait for all charges to reach the gate top
        await UniTask.WhenAll(moveTasks);

        activateFeedback.PlayFeedbacks();

        // Delete the gate charges
        foreach (GateCharge gateCharge in gateCharges)
        {
            Destroy(gateCharge.gameObject);
        }
    }

    private bool AllChargesFinishedMovement()
    {
        foreach (GateCharge gateCharge in gateCharges)
        {
            if (gateCharge != null && !gateCharge.isFinishedMovement()) return false;
        }

        return true;
    }

    private async UniTask MoveChargeToGateTop(Transform chargeTransform, CancellationToken cancellationToken)
    {
        Vector3 startPosition = chargeTransform.position;
        Vector3 targetPosition = gateTop.position;
        float elapsed = 0f;

        while (elapsed < gateOpenDuration)
        {
            float t = elapsed / gateOpenDuration;
            chargeTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        chargeTransform.position = targetPosition;
    }
}
