using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;

public class OverlayCameraController : SceneService<OverlayCameraController>
{
    [SerializeField] private Transform whiteBackground = null!;

    [SerializeField] private MMF_Player whiteFadeEnter = null!;
    [SerializeField] private MMF_Player whiteFadeExit = null!;

    private Camera foregroundCamera;
    private Camera overlayCamera;
    private bool isOverlayEnabled;

    public bool IsOverlayEnabled => isOverlayEnabled;

    protected override void OnEnable()
    {
        base.OnEnable();
        Camera.onPreCull += HandleCameraPreCull;
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
    }

    protected override void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        Camera.onPreCull -= HandleCameraPreCull;
        base.OnDisable();
    }

    void Start()
    {
        var rig = CameraRig.For(gameObject.scene);
        foregroundCamera = rig.Foreground;
        overlayCamera = rig.Overlay;

        overlayCamera.enabled = false;
        foregroundCamera.enabled = true;
    }

    private void LateUpdate()
    {
        MatchForegroundCamera();

        if (isOverlayEnabled)
        {
            CenterWhiteBackgroundOnCamera();
        }
    }

    private void OnDestroy()
    {
        if (whiteFadeEnter != null)
        {
            whiteFadeEnter.RestoreInitialValues();
        }
        isOverlayEnabled = false;
    }

    public async UniTask RequestPlayerOverlay()
    {
        MatchForegroundCamera();
        CenterWhiteBackgroundOnCamera();
        isOverlayEnabled = true;
        overlayCamera.enabled = isOverlayEnabled;
        await whiteFadeEnter.PlayFeedbacksAsync(CancellationToken.None);
        foregroundCamera.enabled = !isOverlayEnabled;
    }

    public async UniTask ReleasePlayerOverlay()
    {
        foregroundCamera.enabled = true;
        MatchForegroundCamera();
        await whiteFadeExit.PlayFeedbacksAsync(CancellationToken.None);
        
        isOverlayEnabled = false;
    }

    public void ForcePlayerOverlay()
    {
        MatchForegroundCamera();
        CenterWhiteBackgroundOnCamera();
        whiteFadeEnter.PlayFeedbacks();
        whiteFadeEnter.SkipToTheEnd();
        isOverlayEnabled = true;
        overlayCamera.enabled = true;
    }

    private void HandleCameraPreCull(Camera renderingCamera)
    {
        MatchBeforeOverlayRender(renderingCamera);
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        MatchBeforeOverlayRender(renderingCamera);
    }

    private void MatchBeforeOverlayRender(Camera renderingCamera)
    {
        if (!isOverlayEnabled || renderingCamera != overlayCamera) return;

        MatchForegroundCamera();
        CenterWhiteBackgroundOnCamera();
    }

    private void MatchForegroundCamera()
    {
        if (foregroundCamera == null || overlayCamera == null) return;

        Transform foregroundTransform = foregroundCamera.transform;
        Transform overlayTransform = overlayCamera.transform;
        overlayTransform.SetPositionAndRotation(foregroundTransform.position, foregroundTransform.rotation);
    }

    // The quad is a child of the overlay camera so it fills the view as the camera moves.
    // Writing world position onto the player baked a local offset from wherever the camera
    // happened to be that frame, which threw the quad off-screen once Cinemachine caught up.
    private void CenterWhiteBackgroundOnCamera()
    {
        Vector3 local = whiteBackground.localPosition;
        whiteBackground.localPosition = new Vector3(0f, 0f, local.z);
    }
    
    [Button("Debug: Request Overlay")]
    private void DebugRequestOverlay()
    {
        RequestPlayerOverlay().Forget();
    }

    [Button("Debug: Release Overlay")]
    private void DebugReleaseOverlay()
    {
        ReleasePlayerOverlay().Forget();
    }
}
