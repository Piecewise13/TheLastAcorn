using System;
using System.Collections.Generic;
using Player;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Owns the tutorial prompt objects.
///
/// Division of labour:
///  - The timeline decides what is shown, what is hidden, and when playback waits.
///    TutorialPromptMarker shows/hides; TutorialWaitMarker pauses. They are independent:
///    a prompt can appear, the timeline keep running, and a later wait hold playback.
///    Only one prompt is up at a time: a Show marker replaces the current prompt.
///  - TutorialTrigger reports that the player satisfied a step. Completion never
///    shows or hides anything; it records the step and thaws playback if the timeline
///    was waiting on that step.
///  - Exception: a trigger flagged to show a prompt does so on arrival (ShowPromptFromTrigger).
///    Used where the player can get there before the timeline does, e.g. the scent trigger.
///    The timeline still owns the wait and the hide.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    // Unity saves these as numbers in the scene and timeline, so a value's number must never
    // change. Add new steps with a new, unused number; order in this list doesn't matter.
    // A step needs no prompt: pair a TutorialWaitMarker with a TutorialTrigger to make the
    // timeline wait for the player with nothing on screen (e.g. WaitForPlayer1).
    [Serializable] public enum TutorialEvents
    {
        ShowMovePrompt = 0,
        ShowClimbPrompt = 1,
        ShowJumpPrompt = 2,
        WaitForPlayer1 = 3,
        ShowSensesPrompt = 4,
        ReachScentTrail = 5,
        ReachBranchBreakAcorn = 6,
        CollectFirstAcorn = 7,
        ReachSensesTrigger = 8
    }

    [Serializable]
    private struct TutorialStep
    {
        public TutorialEvents step;
        public GameObject tutorialObjects;
    }

    [SerializeField] private List<TutorialStep> tutorialSteps;

    public PlayableDirector playableDirector;

    private PlayerMoveManager playerMoveManager;

    private readonly HashSet<TutorialEvents> completedSteps = new HashSet<TutorialEvents>();

    // The step whose marker froze playback. Only that step's completion thaws it.
    private TutorialEvents? haltingStep;

    // The prompt currently on screen. Showing another prompt replaces it.
    private TutorialEvents? shownStep;

    private bool bypassed;
    private bool movementEnabled;

    private void Awake()
    {
        // Claim the singleton in Awake: the director can fire its first marker before Start runs.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        bypassed = DebugSettings.Instance.BypassTutorial;
        if (bypassed)
        {
            Debug.Log("[TutorialManager] Bypassing tutorial due to debug settings.");
            return;
        }

        playerMoveManager = FindAnyObjectByType<PlayerMoveManager>();
        playerMoveManager.DisableMove();

        HideAllSteps();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Entry point for TutorialPromptMarker, via TutorialPromptReceiver.</summary>
    public void HandlePrompt(TutorialEvents step, TutorialPromptAction action)
    {
        if (bypassed)
        {
            return;
        }

        switch (action)
        {
            case TutorialPromptAction.Hide:
                // Unconditional: a completed step still needs its prompt cleared on cue.
                SetStepActive(step, false);
                if (shownStep == step)
                {
                    shownStep = null;
                }
                break;

            case TutorialPromptAction.Show:
                ShowPrompt(step);
                break;
        }
    }

    /// <summary>
    /// Called by TutorialTrigger. Records the step and releases playback if the timeline
    /// was waiting on it. Never changes what is on screen — that is the timeline's job.
    /// </summary>
    public void StepCompleted(TutorialEvents tutorialEvent)
    {
        // Add returns false if the step was already completed, so triggers are re-entry safe.
        if (bypassed || !completedSteps.Add(tutorialEvent))
        {
            return;
        }

        // Only the step that froze playback is allowed to thaw it.
        if (haltingStep == tutorialEvent)
        {
            haltingStep = null;
            SetPlaybackSpeed(1f);
        }
    }

    /// <summary>
    /// Called by a TutorialTrigger flagged to show a prompt. Same rules as a Show marker:
    /// replaces whatever is up, and does nothing if the step is already complete.
    /// </summary>
    public void ShowPromptFromTrigger(TutorialEvents step)
    {
        if (!bypassed)
        {
            ShowPrompt(step);
        }
    }

    public bool IsStepCompleted(TutorialEvents step) => completedSteps.Contains(step);

    /// <summary>
    /// True while the timeline has this step's prompt on screen. Prompt components use it to
    /// tell a real show apart from the OnEnable every object gets at scene load, before
    /// HideAllSteps runs.
    /// </summary>
    public bool IsPromptShown(TutorialEvents step) => shownStep == step;

    /// <summary>Entry point for TutorialWaitMarker, via TutorialPromptReceiver.</summary>
    public void WaitFor(TutorialEvents step)
    {
        if (!bypassed)
        {
            BeginWait(step);
        }
    }

    private void BeginWait(TutorialEvents step)
    {
        // The player may have beaten the step before the marker landed — don't deadlock on it.
        if (completedSteps.Contains(step))
        {
            return;
        }

        haltingStep = step;
        SetPlaybackSpeed(0f);
    }

    private void ShowPrompt(TutorialEvents step)
    {
        // A step the player already beat never prompts, however late the marker lands.
        if (completedSteps.Contains(step))
        {
            return;
        }

        // The player starts locked; the first prompt to appear hands control over.
        if (!movementEnabled)
        {
            movementEnabled = true;
            playerMoveManager.EnableMove();
        }

        // One prompt at a time: the new prompt replaces whatever is up.
        if (shownStep.HasValue && shownStep != step)
        {
            SetStepActive(shownStep.Value, false);
        }

        shownStep = step;
        SetStepActive(step, true);
    }

    private void SetStepActive(TutorialEvents step, bool active)
    {
        foreach (TutorialStep entry in tutorialSteps)
        {
            if (entry.step == step && entry.tutorialObjects != null)
            {
                entry.tutorialObjects.SetActive(active);
            }
        }
    }

    private void SetPlaybackSpeed(float speed)
    {
        if (playableDirector == null || !playableDirector.playableGraph.IsValid())
        {
            return;
        }

        playableDirector.playableGraph.GetRootPlayable(0).SetSpeed(speed);
    }

    private void HideAllSteps()
    {
        foreach (TutorialStep step in tutorialSteps)
        {
            if (step.tutorialObjects != null)
            {
                step.tutorialObjects.SetActive(false);
            }
        }
    }

#if UNITY_EDITOR
    [Header("Debug")]
    [Tooltip("Where Debug: Skip To Senses Beat puts the player — on the tree just before the scent trigger.")]
    [SerializeField] private Transform debugSensesBeatSpot;

    /// <summary>
    /// Play-mode shortcut for testing the senses beat: jumps the timeline to just before the
    /// mother's wait at the acorn, marks every earlier step done, and drops the player on the
    /// tree. The senses trigger past the acorn then shows the senses prompt.
    /// </summary>
    [NaughtyAttributes.Button("Debug: Skip To Senses Beat")]
    private void DebugSkipToSensesBeat()
    {
        if (!Application.isPlaying || bypassed)
        {
            Debug.LogWarning("[TutorialManager] Enter Play mode with the tutorial running (Bypass Tutorial off) first.", this);
            return;
        }

        if (debugSensesBeatSpot == null || !(playableDirector.playableAsset is UnityEngine.Timeline.TimelineAsset timeline))
        {
            Debug.LogWarning("[TutorialManager] Assign the debug senses spot and the playable director first.", this);
            return;
        }

        double beatTime = double.MaxValue;
        foreach (UnityEngine.Timeline.IMarker marker in timeline.markerTrack.GetMarkers())
        {
            if (marker is TutorialWaitMarker wait && wait.Step == TutorialEvents.CollectFirstAcorn)
            {
                beatTime = Math.Min(beatTime, wait.time);
            }
        }

        if (beatTime == double.MaxValue)
        {
            Debug.LogWarning("[TutorialManager] No Tutorial Wait marker for CollectFirstAcorn on the timeline.", this);
            return;
        }

        // Everything the timeline would have waited on before the beat counts as done, so no
        // skipped prompt reappears and nothing earlier can freeze playback.
        foreach (UnityEngine.Timeline.IMarker marker in timeline.markerTrack.GetMarkers())
        {
            if (marker is TutorialWaitMarker wait && wait.time < beatTime)
            {
                StepCompleted(wait.Step);
            }
        }

        haltingStep = null;
        HideAllSteps();
        shownStep = null;

        if (!movementEnabled)
        {
            movementEnabled = true;
            playerMoveManager.EnableMove();
        }

        playableDirector.time = Math.Max(0d, beatTime - 0.1d);
        playableDirector.Evaluate();
        SetPlaybackSpeed(1f);

        GameObject player = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.playerGameObject : null;
        if (player != null)
        {
            Vector3 spot = debugSensesBeatSpot.position;
            if (player.TryGetComponent(out Rigidbody2D body))
            {
                body.position = spot;
                body.linearVelocity = Vector2.zero;
            }
            player.transform.position = spot;
        }
    }
#endif
}
