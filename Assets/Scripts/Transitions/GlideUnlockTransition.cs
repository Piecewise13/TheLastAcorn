using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the glide-unlock cinematic that crosses a scene boundary: freeze the player, frame the
/// camera on them, fade to white with the player still visible, show the "Glide unlock" text, load
/// the next cave under the white, drop the player into a fall in that new scene, reveal the fall,
/// then hand control back for the "Press A to start gliding" beat.
///
/// The "player visible on white" look is the existing <see cref="OverlayCameraController"/> overlay
/// fade, reused on both scenes — its overlay camera composites the player above the white, which a
/// screen-space cover cannot. The persistent <see cref="ScreenFader"/> only bridges the raw
/// single-mode swap instant. Text is authored as a pushed <see cref="GlideTransitionTextView"/>;
/// because the <see cref="ViewManager"/> is persistent, a view pushed here survives the load. This
/// object must outlive the load too, so <see cref="Begin"/> reparents to root and marks itself
/// <see cref="Object.DontDestroyOnLoad"/>. The falling half is handed to a <see cref="GlideFallEntry"/>
/// placed in the destination scene.
/// </summary>
public class GlideUnlockTransition : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("The cave scene to fall into. Must be in Build Settings.")]
    [SerializeField] private string nextCaveSceneName;

    [Header("Trigger")]
    [Tooltip("When true, this runs automatically the moment the Glide ability is unlocked.")]
    [SerializeField] private bool runOnGlideUnlock = true;

    [Header("Camera")]
    [Tooltip("Orthographic size to zoom in to on the player before the white. Leave <= 0 to skip.")]
    [SerializeField] private float zoomInOrthoSize = 3f;

    [Header("Text Views")]
    [Tooltip("View prefab shown on the white in the SOURCE scene (\"Glide unlock\").")]
    [SerializeField] private ViewBase unlockTextView;

    [SerializeField] private Transform teleportPosition = null!;
    [SerializeField] private CameraZoneManager puzzleZone = null!;


    private bool hasRun;

    private void OnEnable()
    {
        if (runOnGlideUnlock && PlayerAbilityManager.Instance != null)
        {
            PlayerAbilityManager.Instance.OnAbilityUnlocked += HandleAbilityUnlocked;
        }
    }

    private void OnDisable()
    {
        if (PlayerAbilityManager.Instance != null)
        {
            PlayerAbilityManager.Instance.OnAbilityUnlocked -= HandleAbilityUnlocked;
        }
    }

    private void HandleAbilityUnlocked(PlayerAbilityManager.Abilities ability)
    {
        if (ability == PlayerAbilityManager.Abilities.Glide)
        {
            Begin();
        }
    }

    [Button("Begin Glide Unlock Transition (Debug)")]
    public void Begin()
    {
        if (hasRun) return;
        hasRun = true;

        // Nothing else drives this, so it has to carry itself across the load.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        Run(destroyCancellationToken).Forget();
    }

    private async UniTask Run(CancellationToken token)
    {
        if (string.IsNullOrEmpty(nextCaveSceneName))
        {
            Debug.LogError($"[{nameof(GlideUnlockTransition)}] No next cave scene name set.", this);
            return;
        }

        // 2. Freeze the player and zoom the overlay camera in on them, fading to white with the
        //    player still composited on top (source scene). This is the whole "camera zooms in with
        //    white background behind player" beat — the zoom lives in the whiteFadeEnter feedback.
        PlayerMoveManager player = ResolvePlayer();
        PlayerStateManager.Instance.ChangeState(PlayerStateManager.PlayerState.Locked);
        
        player?.DisableMove();
        await OverlayCameraController.Current.RequestPlayerOverlay();

        // 3. Snap the persistent white bridge on. It is white-on-white over the source overlay, so
        //    it is invisible, and it survives the single-mode load that tears the source overlay
        //    (and its camera) down. Without it the destination world flashes for a frame mid-swap.
        ScreenFader.Instance.ShowCoverInstant();

        // 5. Drop the destination scene's own player into the fall. This positions it, sets the Fall
        //    state, and retargets the camera to it — all still hidden behind the bridge.
        GlideFallEntry entry = FindFirstObjectByType<GlideFallEntry>();
        PlayerMoveManager fallingPlayer = null;
        if (entry != null)
        {
            entry.BeginFall();
            fallingPlayer = entry.Player;
        }
        else
        {
            Debug.LogWarning($"[{nameof(GlideUnlockTransition)}] No {nameof(GlideFallEntry)} found in '{nextCaveSceneName}'. The player will not be positioned.", this);
        }

        // 7. Drop the bridge. The destination overlay white is already up, so the handoff is
        //    white-on-white and invisible.
        ScreenFader.Instance.HideCoverInstant();
        
        player.transform.position = teleportPosition.position;
        player.
        CameraDirector.Current.SwitchArea(puzzleZone, player.transform.position);

        // 8. "Glide unlock" — pushed onto the (now persistent) view stack, above the white.
        ViewBase unlockView = await PushGlideUnlockView(unlockTextView);
        
        // 9. Hand control back so the player can fall and start gliding.
        fallingPlayer?.EnableMove();
        await WaitForGlide(token);

        // 10. Reveal the cave around the fall, then drop the "Glide unlock" text.
        await ExitPlayerOverlay(token);
        PopTextView(unlockView);
        Destroy(gameObject);
    }
    
    private static async UniTask<ViewBase> PushGlideUnlockView(ViewBase viewPrefab)
    {
        if (viewPrefab == null || ViewManager.Instance == null) return null;

        return await ViewManager.Instance.PushView(viewPrefab);
    }
    
    private static void PopTextView(ViewBase view)
    {
        if (view == null || ViewManager.Instance == null) return;

        ViewManager.Instance.PopView();
    }
    
    #region White Fade Management

    /// <summary>Reveals the world again, fading the overlay white out (or the flat cover as fallback).</summary>
    private static async UniTask ExitPlayerOverlay(CancellationToken token)
    {
        if (OverlayCameraController.Current != null)
        {
            await OverlayCameraController.Current.ReleasePlayerOverlay();
        }
    }
    
    #endregion

    private static async UniTask WaitForGlide(CancellationToken token)
    {
        if (PlayerStateManager.Instance == null) return;

        var completion = new UniTaskCompletionSource();

        void OnChanged(PlayerStateManager.PlayerState from, PlayerStateManager.PlayerState to)
        {
            if (to == PlayerStateManager.PlayerState.Glide)
            {
                completion.TrySetResult();
            }
        }

        PlayerStateManager.Instance.OnStateChanged += OnChanged;
        try
        {
            await completion.Task.AttachExternalCancellation(token);
        }
        finally
        {
            PlayerStateManager.Instance.OnStateChanged -= OnChanged;
        }
    }
    
    private static PlayerMoveManager ResolvePlayer()
    {
        if (PlayerStateManager.Instance == null)
        {
            Debug.LogError("[Overlay Camera] couldn't get player reference");
            return null;
        }
        
        return PlayerStateManager.Instance.playerGameObject.GetComponent<PlayerMoveManager>();
    }
}
