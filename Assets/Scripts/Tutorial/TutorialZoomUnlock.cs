using Player;
using UnityEngine;

/// <summary>
/// Unlocks the zoom (senses) ability the moment its tutorial prompt appears, freezes the player
/// until they zoom, and completes the prompt's step the first time they do. Put it on the prompt
/// object; whatever turns the prompt on (a timeline Show marker or a TutorialTrigger flagged to
/// show it) is the trigger, and a Wait marker on the same step holds the timeline until the
/// player has tried it.
/// </summary>
public class TutorialZoomUnlock : MonoBehaviour
{
    [Tooltip("The prompt step this object belongs to. Only a real show of that prompt unlocks zoom.")]
    [SerializeField] private TutorialManager.TutorialEvents step = TutorialManager.TutorialEvents.ShowSensesPrompt;

    private PlayerCameraManager playerCamera;

    private void OnEnable()
    {
        // Every object is enabled once at scene load, before the tutorial hides its prompts.
        // Only unlock when the timeline has actually shown this prompt.
        TutorialManager tutorial = TutorialManager.Instance;
        if (tutorial == null || !tutorial.IsPromptShown(step))
        {
            return;
        }

        GameObject player = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.playerGameObject : null;
        playerCamera = player != null ? player.GetComponentInChildren<PlayerCameraManager>() : null;
        if (playerCamera == null)
        {
            Debug.LogWarning("[TutorialZoomUnlock] No player camera found; zoom stays locked.", this);
            return;
        }

        PlayerAbilityManager abilities = PlayerAbilityManager.Instance;
        if (abilities != null && !abilities.IsAbilityUnlocked(PlayerAbilityManager.Abilities.Zoom))
        {
            abilities.UnlockAbility(PlayerAbilityManager.Abilities.Zoom);
        }

        playerCamera.EnableZoom();
        playerCamera.OnZoomStarted += HandleZoomStarted;

        // Hold the player still until they try the sense. No matching EnableMove here: letting go
        // of zoom already re-enables movement (PlayerCameraManager.HandleZoomInput).
        if (player.TryGetComponent(out PlayerMoveManager playerMoveManager))
        {
            playerMoveManager.DisableMove();
        }
    }

    private void OnDisable()
    {
        if (playerCamera != null)
        {
            playerCamera.OnZoomStarted -= HandleZoomStarted;
            playerCamera = null;
        }
    }

    private void HandleZoomStarted()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.StepCompleted(step);
        }
    }
}
