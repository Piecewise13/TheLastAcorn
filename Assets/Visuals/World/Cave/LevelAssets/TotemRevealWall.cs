using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

    [Tooltip("Animator trigger that moves the wall from Idle into Reveal")]
    [SerializeField] private string revealTrigger = "Reveal";

    public event Action RevealCompleted;

    private Animator animator;
    private UniTaskCompletionSource revealCompletion;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Starts the reveal. The rumble is started here rather than from an AnimationEvent because
    /// events keyed at frame 0 do not reliably fire on a clip's first play.
    /// </summary>
    public void BeginReveal()
    {
        if (rumble == null)
        {
            Debug.LogWarning($"[{nameof(TotemRevealWall)}] No {nameof(RockWallRumble)} assigned, the rocks will not shake.", this);
        }

        rumble?.StartRumble();
        animator.SetTrigger(revealTrigger);
    }

    /// <summary>
    /// Starts the reveal and completes once the clip reaches its OnRevealComplete event, so the
    /// caller can hold control until the path is open.
    /// </summary>
    public UniTask BeginRevealAsync(CancellationToken cancellationToken)
    {
        if (revealCompletion == null)
        {
            revealCompletion = new UniTaskCompletionSource();
            BeginReveal();
        }

        return revealCompletion.Task.AttachExternalCancellation(cancellationToken);
    }

    // Called from AnimationEvents on the Reveal clip. Keeping the rumble's start and stop on the
    // timeline is what lets it overlap the animated movement for as long as the clip wants.
    public void StartRumble()
    {
        rumble?.StartRumble();
    }

    public void StopRumble()
    {
        rumble?.StopRumble();
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

        BeginReveal();
    }

    [Button("Clear Rumble (Debug)")]
    private void DebugClearRumble()
    {
        rumble?.ResetRocks();
    }
}
