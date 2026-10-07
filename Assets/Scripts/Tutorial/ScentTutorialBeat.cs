using System.Threading;
using Cysharp.Threading.Tasks;
using Player;
using UnityEngine;

/// <summary>
/// The scent trail for the "use your senses" moment. Put it on an always-active object — not
/// on a prompt — because prompts are toggled on and off and would restart the trail.
///
/// When the player hits the scent trigger, a trail flows in from the off-screen acorn and
/// stops part-way to the player (halfway by default). It never changes after that: zooming out
/// simply lets the player see all of it and the acorn it leads to. It fades away once the acorn
/// is collected. Reaching the acorn (its own TutorialTrigger) completes CollectFirstAcorn, which
/// lets the timeline show the senses prompt; this beat only completes it itself when the acorn is
/// already gone.
/// </summary>
public class ScentTutorialBeat : MonoBehaviour
{
    [Tooltip("The acorn the scent trail leads to. It should sit just off-screen at normal zoom.")]
    [SerializeField] private Acorn acorn;

    [Tooltip("How far from the acorn toward the player the trail reaches. 0.5 = halfway.")]
    [SerializeField, Range(0f, 1f)] private float reach = 0.5f;

    private ScentTrail trail;

    private void Start()
    {
        RunBeat(destroyCancellationToken).Forget();
    }

    private void OnDestroy()
    {
        if (trail != null)
        {
            Destroy(trail.gameObject);
        }
    }

    private async UniTask RunBeat(CancellationToken cancellationToken)
    {
        // The player body registers itself after load, so it may not exist on our first frame.
        await UniTask.WaitUntil(
            () => PlayerStateManager.Instance != null && PlayerStateManager.Instance.playerGameObject != null,
            cancellationToken: cancellationToken);

        if (DebugSettings.Instance != null && DebugSettings.Instance.BypassTutorial)
        {
            return;
        }

        // An acorn collected on an earlier run is inactive and can never be reached. Complete the
        // step now so the timeline isn't left waiting on it forever.
        if (acorn == null || !acorn.gameObject.activeInHierarchy)
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.StepCompleted(TutorialManager.TutorialEvents.CollectFirstAcorn);
            }
            return;
        }

        PlayerSenses senses = PlayerStateManager.Instance.playerGameObject.GetComponentInChildren<PlayerSenses>();
        ScentTrailManager scentTrails = ScentTrailManager.Instance;
        if (senses == null || scentTrails == null)
        {
            Debug.LogWarning("[ScentTutorialBeat] Needs PlayerSenses on the player and a ScentTrailManager in the session.", this);
            return;
        }

        // Nothing shows until the player walks into the scent trigger (ScentPromptTrigger).
        await UniTask.WaitUntil(
            () => acorn == null || !acorn.gameObject.activeInHierarchy ||
                  (TutorialManager.Instance != null &&
                   TutorialManager.Instance.IsStepCompleted(TutorialManager.TutorialEvents.ReachScentTrail)),
            cancellationToken: cancellationToken);
        if (acorn == null || !acorn.gameObject.activeInHierarchy)
        {
            return;
        }

        // Start with zero length at the acorn and ease out to the target reach, so the trail
        // flows in from off-screen rather than popping on. The manager skips this acorn while
        // the trail exists, so zooming out shows this same trail rather than adding a second.
        trail = scentTrails.CreateTrail(acorn, senses.TrailOrigin, 1f, 0f);
        trail.SetReach(reach);
    }
}
