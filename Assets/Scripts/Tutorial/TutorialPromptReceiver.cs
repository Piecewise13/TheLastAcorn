using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Forwards TutorialPromptMarker and TutorialWaitMarker notifications to TutorialManager.
/// Must live on the PlayableDirector's own GameObject — Timeline only delivers
/// notifications to receivers there.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class TutorialPromptReceiver : MonoBehaviour, INotificationReceiver
{
    public void OnNotify(Playable origin, INotification notification, object context)
    {
        switch (notification)
        {
            case TutorialPromptMarker prompt:
                if (HasManager(prompt.Step))
                {
                    TutorialManager.Instance.HandlePrompt(prompt.Step, prompt.Action);
                }
                break;

            case TutorialWaitMarker wait:
                if (HasManager(wait.Step))
                {
                    TutorialManager.Instance.WaitFor(wait.Step);
                }
                break;
        }
    }

    private bool HasManager(TutorialManager.TutorialEvents step)
    {
        if (TutorialManager.Instance != null)
        {
            return true;
        }

        Debug.LogWarning($"[TutorialPromptReceiver] No TutorialManager for marker '{step}'.", this);
        return false;
    }
}
