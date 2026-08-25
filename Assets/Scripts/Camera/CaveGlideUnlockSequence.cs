using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// The beat after the glide unlock: the camera leaves the player to frame the cave wall, the wall
/// rumbles apart, and the camera comes back.
///
/// This owns the ordering and nothing else. The camera system stays a service that knows nothing
/// about totems, and <see cref="TotemRevealWall"/> stays responsible only for the reveal itself —
/// neither had a caller before this, so the wiring is what was missing rather than the pieces.
/// </summary>
public class CaveGlideUnlockSequence : MonoBehaviour
{
    [Tooltip("The zone that frames the wall. Set its activation to Event so walking past does nothing.")]
    [SerializeField] private CameraZone wallZone;

    [SerializeField] private TotemRevealWall wall;

    [Tooltip("Left empty, the player is found by tag when the sequence runs.")]
    [SerializeField] private PlayerMove playerMove;

    [Header("Triggers")]
    [Tooltip("Runs when this gate finishes opening. Leave empty if the glide unlock is the trigger.")]
    [SerializeField] private Gate gate;

    [Tooltip("Runs when the Glide ability is unlocked.")]
    [SerializeField] private bool runOnGlideUnlock = true;

    [Tooltip("Beat before the camera leaves the player, so the unlock has a moment to land.")]
    [SerializeField] private float delayBeforeReveal = 0.35f;

    [Tooltip("Beat after the reveal finishes, before the camera returns.")]
    [SerializeField] private float holdAfterReveal = 0.5f;

    [SerializeField] private CameraDirector cameraDirector = null;

    private bool hasRun;

    private void OnEnable()
    {
        if (runOnGlideUnlock && PlayerAbilityManager.Instance != null)
        {
            PlayerAbilityManager.Instance.OnAbilityUnlocked += HandleAbilityUnlocked;
        }

        if (gate != null)
        {
            gate.GateOpened += Begin;
        }
    }

    private void OnDisable()
    {
        if (PlayerAbilityManager.Instance != null)
        {
            PlayerAbilityManager.Instance.OnAbilityUnlocked -= HandleAbilityUnlocked;
        }

        if (gate != null)
        {
            gate.GateOpened -= Begin;
        }
    }

    private void HandleAbilityUnlocked(PlayerAbilityManager.Abilities ability)
    {
        if (ability != PlayerAbilityManager.Abilities.Glide) return;

        Begin();
    }

    [Button("Begin Reveal Sequence (Debug)")]
    public void Begin()
    {
        if (hasRun) return;
        hasRun = true;

        Run(destroyCancellationToken).Forget();
    }

    private async UniTask Run(CancellationToken cancellationToken)
    {
        if (wallZone == null || wall == null)
        {
            Debug.LogWarning($"[{nameof(CaveGlideUnlockSequence)}] Needs both a wall zone and a {nameof(TotemRevealWall)}.", this);
            return;
        }

        PlayerMove player = ResolvePlayer();

        // The player is frozen for the whole beat. That is not only presentation: a frozen player
        // cannot walk into another camera zone mid-sequence, so nothing can compete for the camera.
        player?.DisableMove();

        try
        {
            await UniTask.WaitForSeconds(delayBeforeReveal, cancellationToken: cancellationToken);

            await wallZone.HoldWhile(async token =>
            {
                
                await wall.BeginRevealAsync(token);
                await UniTask.WaitForSeconds(holdAfterReveal, cancellationToken: token);
                
                
            }, cancellationToken);
        }
        finally
        {
            player?.EnableMove();
        }
    }

    private PlayerMove ResolvePlayer()
    {
        if (playerMove != null) return playerMove;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerMove = player != null ? player.GetComponentInChildren<PlayerMove>() : null;

        if (playerMove == null)
        {
            Debug.LogWarning($"[{nameof(CaveGlideUnlockSequence)}] No PlayerMove found, the player will not be frozen.", this);
        }

        return playerMove;
    }
}
