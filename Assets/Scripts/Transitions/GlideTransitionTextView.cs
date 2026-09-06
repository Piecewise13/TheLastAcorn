using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// The on-screen text for the glide-unlock transition ("Glide unlock", "Press A to start gliding"),
/// as a prefab-authored view pushed onto the <see cref="ViewManager"/> stack rather than built in
/// code. The stack already layers above the overlay white (same as <c>UnlockZoomView</c>), so the
/// text reads on the white.
///
/// This view only owns its own intro/outro feedbacks — <see cref="GlideUnlockTransition"/> decides
/// when it is pushed and popped. Author one prefab per line so the copy and its animation are set
/// visually in the inspector.
/// </summary>
public class GlideTransitionTextView : ViewBase
{
    [Tooltip("Plays when the view is shown (text pops in). Optional.")]
    [SerializeField] private MMF_Player introFeedback;

    [Tooltip("Plays before the view is hidden (text pops out). Optional.")]
    [SerializeField] private MMF_Player outroFeedback;

    public override async UniTask RunAsync(CancellationToken token = default)
    {
        if (introFeedback != null)
        {
            await introFeedback.PlayFeedbacksAsync(token);
        }
    }

    public override async UniTask Hide(CancellationToken token = default)
    {
        if (outroFeedback != null)
        {
            await outroFeedback.PlayFeedbacksAsync(token);
        }

        await base.Hide(token);
    }
}
