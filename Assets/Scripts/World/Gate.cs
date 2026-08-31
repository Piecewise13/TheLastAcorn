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

    /// <summary>Fires once the gate has fully opened and its charges are spent.</summary>
    public event Action GateOpened;

    private int numChargeCollected = 0;

    float groundCheckInterval = 0.1f;

    [SerializeField] List<GateCharge> gateCharges = new List<GateCharge>();

    [SerializeField] Transform gateTop;
    [SerializeField] float gateChargeSpeed = 5f;

    [SerializeField] private float radialDistance = 2f;
    [SerializeField] private float chargeTargetScale = 0.5f;
    [SerializeField] private float gateOpenDuration = 2f;

    [Header("Feedbacks")] [SerializeField] private MMF_Player activateFeedback;

    [Header("Initial State")]
    [Tooltip("Marks this gate as one the player has already opened. Its charges are removed, the " +
             "activation feedbacks run on load so the eyes and fireflies are already going, and it " +
             "cannot be opened a second time. Purely presentational — no ability is unlocked and " +
             "GateOpened does not fire.")]
    [SerializeField] private bool startActivated;

    private bool isActivated;


    private void Start()
    {
        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>();
        }

        if (startActivated)
        {
            ActivateImmediately();
            return;
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

    /// <summary>
    /// Drops the gate straight into its opened state for a scene where the player has already used it.
    /// </summary>
    /// <remarks>
    /// The feedbacks are played rather than skipped. <see cref="MMF_Particles"/> has no
    /// skip-to-the-end behaviour and <c>SkipToTheEnd</c> finishes by calling <c>StopFeedbacks</c>, so
    /// skipping would leave the fireflies stopped instead of running. Playing them means the eyes and
    /// fireflies ramp up over the feedback's own duration, which is hidden by the scene fading in.
    /// </remarks>
    private void ActivateImmediately()
    {
        isActivated = true;

        // Held rather than zeroed so Update's "already finished" guard still reads as complete after
        // the charges are gone.
        numChargeCollected = gateCharges.Count;

        foreach (GateCharge gateCharge in gateCharges)
        {
            if (gateCharge != null) Destroy(gateCharge.gameObject);
        }

        if (activateFeedback == null)
        {
            Debug.LogWarning($"[{nameof(Gate)}] {nameof(startActivated)} is set but no activate feedback is assigned, so the gate will look closed.", this);
            return;
        }

        activateFeedback.PlayFeedbacks();
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
        if (isActivated) return;

        numChargeCollected++;

        if (numChargeCollected >= gateCharges.Count)
        {
            OpenGate();
        }
    }

    private void OpenGate()
    {
        // Latched before the sequence starts, so a late Collect cannot kick off a second unlock.
        isActivated = true;

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

        await PlayerAbilityManager.Instance.UnlockAbility(PlayerAbilityManager.Abilities.Glide);
        GateOpened?.Invoke();
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
