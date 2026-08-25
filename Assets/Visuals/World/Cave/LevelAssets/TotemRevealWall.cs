using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// Drives the cave wall reveal that follows the glide unlock. This type holds no timing of its own:
/// the Reveal clip owns the schedule. It animates the rock transforms to open the path, keys
/// <see cref="RockWallRumble"/>'s intensity to shape the shaking, and fires AnimationEvents into the
/// methods below.
///
/// AnimationEvents only reach components on the same GameObject as the Animator, so anything the
/// clip needs to talk to has to be reachable from here.
/// </summary>
[RequireComponent(typeof(Animator))]
public class TotemRevealWall : MonoBehaviour
{
    [SerializeField] private RockWallRumble rumble;

    [SerializeField] private MMF_Player revealFeedbacks = null!;

    public event Action RevealCompleted;

    private Animator animator;
    private UniTaskCompletionSource revealCompletion;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Starts the reveal and completes once the clip reaches its OnRevealComplete event, so the
    /// caller can hold control until the path is open.
    /// </summary>
    public async UniTask BeginRevealAsync(CancellationToken cancellationToken)
    {
        
        await revealFeedbacks.PlayFeedbacksAsync(cancellationToken);
    }
    

    public void OnRevealComplete()
    {
        revealCompletion?.TrySetResult();
        revealCompletion = null;
        RevealCompleted?.Invoke();
    }

    private void OnDestroy()
    {
        revealCompletion?.TrySetCanceled();
        revealCompletion = null;
    }

    [Button("Begin Reveal (Debug)")]
    private void DebugBeginReveal()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter play mode to test the reveal.", this);
            return;
        }

        BeginRevealAsync(CancellationToken.None);
    }

    [Button("Clear Rumble (Debug)")]
    private void DebugClearRumble()
    {
        rumble?.ResetRocks();
    }
}
