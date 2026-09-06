using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MoreMountains.Feedbacks;
using UnityEngine;

public class OverlayCameraController : SceneService<OverlayCameraController>
{
    [SerializeField] private Transform whiteBackground = null!;

    [SerializeField] private MMF_Player whiteFadeEnter = null!;
    [SerializeField] private MMF_Player whiteFadeExit = null!;

    private Camera foregroundCamera;
    private Camera overlayCamera;
    private bool isOverlayEnabled;

    // Instance, not static: each scene has its own overlay controller, so a shared cache would let
    // one scene's controller hand back another scene's (destroyed) player.
    private PlayerMoveManager playerMovement = null!;
    
    public bool IsOverlayEnabled => isOverlayEnabled;

    void Start()
    {
        var rig = CameraRig.For(gameObject.scene);
        foregroundCamera = rig.Foreground;
        overlayCamera = rig.Overlay;
        UpdateOverlayState();
    }

    private void OnDestroy()
    {
        if (whiteFadeEnter != null)
        {
            whiteFadeEnter.RestoreInitialValues();
        }
        isOverlayEnabled = false;
        UpdateOverlayState();
    }

    public async UniTask RequestPlayerOverlay()
    {
        playerMovement = ResolvePlayer();
        whiteBackground.position = playerMovement.transform.position;
        isOverlayEnabled = true;
        UpdateOverlayState();
        await whiteFadeEnter.PlayFeedbacksAsync(CancellationToken.None);
    }

    public void ForcePlayerOverlay()
    {
        playerMovement = ResolvePlayer();
        whiteBackground.position = playerMovement.transform.position;
        
        
        whiteFadeEnter.PlayFeedbacks();
        whiteFadeEnter.SkipToTheEnd();
        isOverlayEnabled = true;
        UpdateOverlayState();
    }

    public async UniTask ReleasePlayerOverlay()
    {
        foregroundCamera.enabled = true;
        whiteFadeExit.PlayFeedbacks();
        while (whiteFadeExit.IsPlaying)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
        
        isOverlayEnabled = false;
        UpdateOverlayState();
    }

    private void UpdateOverlayState()
    {
        if (overlayCamera != null)
        {
            overlayCamera.enabled = isOverlayEnabled;
        }

        if (foregroundCamera != null)
        {
            foregroundCamera.enabled = !isOverlayEnabled;
        }
    }
    
    [CanBeNull]
    private PlayerMoveManager ResolvePlayer()
    {
        if (PlayerStateManager.Instance == null)
        {
            Debug.LogError("[Overlay Camera] couldn't get player reference");
            return null;
        }

        if (playerMovement != null)
        {
            return playerMovement;
        }
        
        return PlayerStateManager.Instance.playerGameObject.GetComponent<PlayerMoveManager>();
    }
}
