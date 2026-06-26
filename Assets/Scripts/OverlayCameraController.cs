using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using UnityEngine;

public class OverlayCameraController : MonoBehaviour
{
    public static OverlayCameraController Instance { get; private set; }

    [SerializeField] private MMF_Player whiteFadeEnter = null!;
    [SerializeField] private MMF_Player whiteFadeExit = null!;

    private Camera foregroundCamera;
    private Camera overlayCamera;
    private bool isOverlayEnabled;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        var rig = CameraRig.Instance;
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
        isOverlayEnabled = true;
        UpdateOverlayState();
        await whiteFadeEnter.PlayFeedbacksAsync(CancellationToken.None);
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
}
