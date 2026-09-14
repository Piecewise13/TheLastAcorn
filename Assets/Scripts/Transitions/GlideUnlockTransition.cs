using System.Threading;
using Player;
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
    [Header("Trigger")]
    [Tooltip("When true, this runs automatically the moment the Glide ability is unlocked.")]
    [SerializeField]
    private bool runOnGlideUnlock = true;

    [Header("Camera")]
    [Tooltip("Orthographic size to zoom in to on the player before the white. Leave <= 0 to skip.")]
    [SerializeField]
    private float zoomInOrthoSize = 3f;

    [Header("Text Views")]
    [Tooltip("View prefab shown on the white in the SOURCE scene (\"Glide unlock\").")]
    [SerializeField]
    private ViewBase unlockTextView;

    [SerializeField] private Transform teleportPosition = null!;
    [SerializeField] private CameraZoneManager puzzleZone = null!;

    [Header("Debug")]
    [Tooltip("When true, the player is dropped straight onto the teleport point as the scene starts, " +
             "with Glide already unlocked. Skips the unlock cinematic entirely — for iterating on the " +
             "glide section without playing up to it.")]
    [SerializeField]
    private bool spawnAtTeleportPoint;


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

    private void Start()
    {
        if (!spawnAtTeleportPoint) return;

        SpawnAtTeleportPoint(destroyCancellationToken).Forget();
    }

    /// <summary>
    /// Scene-start counterpart to <see cref="DebugTeleportDirectlyToFall"/>: the player and the
    /// camera director come online over the scene's first frames, so wait for them rather than
    /// racing them, then drop the player in.
    /// </summary>
    private async UniTaskVoid SpawnAtTeleportPoint(CancellationToken token)
    {
        if (teleportPosition == null)
        {
            Debug.LogWarning("[GlideUnlockTransition] spawnAtTeleportPoint is on but no teleport position is assigned.",
                this);
            return;
        }

        await UniTask.WaitUntil(
            () => PlayerStateManager.Instance != null
                  && PlayerStateManager.Instance.playerGameObject != null
                  && CameraDirector.Current != null,
            cancellationToken: token);

        DebugTeleportDirectlyToFall();
    }

    private void HandleAbilityUnlocked(PlayerAbilityManager.Abilities ability)
    {
        if (ability == PlayerAbilityManager.Abilities.Glide)
        {
            Begin();
        }
    }

    [Button("Begin Glide Unlock Transition (Debug)")]
    public void BypassPuzzle()
    {
        PlayerAbilityManager.Instance.UnlockAbility(PlayerAbilityManager.Abilities.Glide);
        Begin();
    }

    public void Begin()
    {
        if (hasRun) return;
        hasRun = true;

        Debug.Log("[GlideUnlockTransition] Begin: starting sequence, reparenting to root + DontDestroyOnLoad.");

        // Nothing else drives this, so it has to carry itself across the load.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        Run(destroyCancellationToken).Forget();
    }

    [Button("Debug: Teleport Directly To Fall")]
    public void DebugTeleportDirectlyToFall()
    {
        PlayerMoveManager player = ResolvePlayer();
        if (player == null || teleportPosition == null)
        {
            Debug.LogWarning($"[GlideUnlockTransition] Teleport aborted: player={player}, teleportPosition={teleportPosition}.", this);
            return;
        }

        hasRun = true;
        if (PlayerAbilityManager.Instance != null
            && !PlayerAbilityManager.Instance.IsAbilityUnlocked(PlayerAbilityManager.Abilities.Glide))
        {
            PlayerAbilityManager.Instance.UnlockAbility(PlayerAbilityManager.Abilities.Glide);
        }

        CameraRig.Current?.ResetTrackingTarget();
        player.transform.position = teleportPosition.position;
        CameraDirector.Current?.SwitchArea(puzzleZone, teleportPosition.position);

        // Land on an unlocked, falling player whatever state the scene left us in. ChangeState is a
        // no-op while Locked, so the unlock has to come first; after it the second call is harmless.
        if (PlayerStateManager.Instance.CurrentState == PlayerState.Locked)
        {
            PlayerStateManager.Instance.UnlockPlayer();
        }

        PlayerStateManager.Instance.ChangeState(PlayerState.Fall);
        player.EnableMove();

        Debug.Log($"[GlideUnlockTransition] Teleported player to {teleportPosition.position}, state={PlayerStateManager.Instance.CurrentState}.", this);
    }

    private async UniTask Run(CancellationToken token)
    {
        CameraClaim preservedZoom = null;
        
        PlayerMoveManager player = ResolvePlayer();

        try
        {
            CameraRig.Current.SetTrackingTarget(player.transform);
            PlayerStateManager.Instance.LockPlayer();
            await OverlayCameraController.Current.RequestPlayerOverlay();
            // 8. "Glide unlock" — pushed onto the (now persistent) view stack, above the white.

            ViewManager.Instance.ClearViewsInstant();
            ViewBase unlockView = await PushGlideUnlockView(unlockTextView);

            preservedZoom = CameraDirector.Current?.HoldCurrentZoom(CameraPriority.Cinematic);
            player.transform.position = teleportPosition.position;
            CameraDirector.Current?.SwitchArea(puzzleZone, teleportPosition.position);

            // 9. Hand control back so the player can fall and start gliding.
            PlayerStateManager.Instance.UnlockPlayer();

            await UniTask.WaitForSeconds(0.5f);

            await OverlayCameraController.Current.ReleasePlayerOverlay();
            await PopTextView(unlockView, token);

            await WaitForGlide(token);
        }
        finally
        {
            preservedZoom?.Release();
            Destroy(gameObject);
        }
    }

    private static async UniTask<ViewBase> PushGlideUnlockView(ViewBase viewPrefab)
    {
        if (viewPrefab == null || ViewManager.Instance == null) return null;

        return await ViewManager.Instance.PushView(viewPrefab);
    }
    
    private static async UniTask PopTextView(ViewBase view, CancellationToken token)
    {
        if (view == null || ViewManager.Instance == null) return;

        await ViewManager.Instance.PopViewAsync(token);
    }
    
    private static async UniTask WaitForGlide(CancellationToken token)
    {
        if (PlayerStateManager.Instance == null) return;

        var completion = new UniTaskCompletionSource();

        void OnChanged(PlayerState from, PlayerState to)
        {
            if (to == PlayerState.Glide)
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
