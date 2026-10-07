using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// Reports that the player satisfied one tutorial step. By default it does nothing else —
/// the timeline owns presentation; this only says "done". Put it on a trigger collider the
/// player must reach or pass through.
///
/// Optionally it also shows a prompt the moment the player arrives. Use that when the prompt
/// must not depend on where the timeline is (the scent trigger: the player can beat the mother
/// to the acorn, so a timeline Show marker could land too late or not at all).
///
/// For a wait with no prompt (the mother pausing until the player catches up), keep this
/// object outside every prompt in TutorialManager's list — a hidden prompt disables its
/// children, so a trigger inside one could never fire.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private TutorialManager.TutorialEvents eventID;

    [Tooltip("Disable this trigger once it has reported completion.")]
    [SerializeField] private bool disableOnComplete = true;

    [Tooltip("Also show a prompt when the player arrives, instead of waiting for a timeline Show marker.")]
    [SerializeField] private bool showsPrompt;

    [ShowIf(nameof(showsPrompt))]
    [SerializeField] private TutorialManager.TutorialEvents promptToShow = TutorialManager.TutorialEvents.ShowSensesPrompt;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
        {
            return;
        }

        if (TutorialManager.Instance == null)
        {
            Debug.LogWarning($"[TutorialTrigger] No TutorialManager for step '{eventID}'.", this);
            return;
        }

        TutorialManager.Instance.StepCompleted(eventID);

        if (showsPrompt)
        {
            TutorialManager.Instance.ShowPromptFromTrigger(promptToShow);
        }

        if (disableOnComplete)
        {
            gameObject.SetActive(false);
        }
    }
}
