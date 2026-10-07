using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Timeline marker that freezes playback until the player completes one tutorial step.
/// It shows nothing — put a TutorialPromptMarker earlier on the timeline for that.
/// If the step is already complete when this lands, playback carries straight on.
/// </summary>
[Serializable]
[DisplayName("Tutorial Wait")]
public class TutorialWaitMarker : Marker, INotification, INotificationOptionProvider
{
    [SerializeField] private TutorialManager.TutorialEvents step;

    [Tooltip("Fire even if playback jumps past this marker, e.g. after a scrub or a large frame spike.")]
    [SerializeField] private bool retroactive;

    [Tooltip("Fire only on the first pass. Leave on unless the timeline loops and should wait again.")]
    [SerializeField] private bool emitOnce = true;

    public TutorialManager.TutorialEvents Step => step;

    public PropertyName id => default;

    // Deliberately no TriggerInEditMode: scrubbing the timeline must not poke the tutorial.
    NotificationFlags INotificationOptionProvider.flags =>
        (retroactive ? NotificationFlags.Retroactive : default) |
        (emitOnce ? NotificationFlags.TriggerOnce : default);
}
