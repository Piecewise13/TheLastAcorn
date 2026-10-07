using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TutorialPromptAction
{
    /// <summary>Show the prompt. The timeline keeps running.</summary>
    Show,

    /// <summary>Hide the prompt. Always applies, even for a step already completed.</summary>
    Hide
}

/// <summary>
/// Timeline marker that shows or hides one tutorial prompt. It never pauses playback —
/// put a TutorialWaitMarker later on the timeline for that. The timeline is the sole
/// authority on what is shown and when; TutorialTrigger only reports completion.
/// </summary>
[Serializable]
[DisplayName("Tutorial Prompt")]
public class TutorialPromptMarker : Marker, INotification, INotificationOptionProvider
{
    [SerializeField] private TutorialManager.TutorialEvents step;
    [SerializeField] private TutorialPromptAction action = TutorialPromptAction.Show;

    [Tooltip("Fire even if playback jumps past this marker, e.g. after a scrub or a large frame spike.")]
    [SerializeField] private bool retroactive;

    [Tooltip("Fire only on the first pass. Leave on unless the timeline loops and should re-prompt.")]
    [SerializeField] private bool emitOnce = true;

    public TutorialManager.TutorialEvents Step => step;
    public TutorialPromptAction Action => action;

    public PropertyName id => default;

    // Deliberately no TriggerInEditMode: scrubbing the timeline must not poke the tutorial.
    NotificationFlags INotificationOptionProvider.flags =>
        (retroactive ? NotificationFlags.Retroactive : default) |
        (emitOnce ? NotificationFlags.TriggerOnce : default);
}
