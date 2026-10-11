using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OverlayCameraController : SceneService<OverlayCameraController>
{
    [SerializeField] private Transform whiteBackground = null!;

    [SerializeField] private MMF_Player whiteFadeEnter = null!;
    [SerializeField] private MMF_Player whiteFadeExit = null!;

    [Header("Dim")]
    [Tooltip("Backdrop colour for the dim. Alpha is how dark the world gets behind the player; keep it light.")]
    [SerializeField] private Color dimColor = new Color(0.02f, 0.02f, 0.06f, 0.3f);
    [SerializeField] private float dimFadeDuration = 0.35f;

    private SpriteRenderer backgroundRenderer;
    private bool foregroundPostProcessing;
    private bool overlayPostProcessing;
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
        backgroundRenderer = whiteBackground.GetComponent<SpriteRenderer>();

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

    /// <summary>
    /// Like <see cref="RequestPlayerOverlay"/>, but the world stays visible behind a dark, translucent
    /// backdrop instead of being replaced by white. The player still draws above it on the overlay camera,
    /// so nothing in the world can cover the player or effects on the player's layer (the upgrade glow).
    /// </summary>
    public async UniTask RequestPlayerDim()
    {
        MatchForegroundCamera();
        CenterWhiteBackgroundOnCamera();
        backgroundRenderer.color = new Color(dimColor.r, dimColor.g, dimColor.b, 0f);
        isOverlayEnabled = true;
        overlayCamera.enabled = true;
        MovePostProcessingToOverlay();

        // The foreground camera stays on, unlike the white overlay: the dim needs the world drawn underneath it.
        await FadeBackgroundAlpha(dimColor.a, destroyCancellationToken);
    }

    public async UniTask ReleasePlayerDim()
    {
        await FadeBackgroundAlpha(0f, destroyCancellationToken);

        isOverlayEnabled = false;
        overlayCamera.enabled = false;
        RestorePostProcessing();

        // The white fade feedbacks only animate alpha, so hand the backdrop back white.
        backgroundRenderer.color = new Color(1f, 1f, 1f, 0f);
    }

    private async UniTask FadeBackgroundAlpha(float targetAlpha, CancellationToken token)
    {
        Color color = backgroundRenderer.color;
        float startAlpha = color.a;
        float elapsed = 0f;

        while (elapsed < dimFadeDuration)
        {
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / dimFadeDuration);
            backgroundRenderer.color = color;
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        color.a = targetAlpha;
        backgroundRenderer.color = color;
    }

    // The overlay camera is last in the stack and normally skips post-processing, so anything it draws
    // (the player and the upgrade glow) misses bloom. While dimmed, post runs on
    // the overlay camera instead of the foreground one: still once per frame, but after the glow is drawn,
    // so the glow blooms.
    private void MovePostProcessingToOverlay()
    {
        var foregroundData = foregroundCamera.GetUniversalAdditionalCameraData();
        var overlayData = overlayCamera.GetUniversalAdditionalCameraData();
        foregroundPostProcessing = foregroundData.renderPostProcessing;
        overlayPostProcessing = overlayData.renderPostProcessing;

        overlayData.renderPostProcessing = foregroundPostProcessing || overlayPostProcessing;
        foregroundData.renderPostProcessing = false;
    }

    private void RestorePostProcessing()
    {
        foregroundCamera.GetUniversalAdditionalCameraData().renderPostProcessing = foregroundPostProcessing;
        overlayCamera.GetUniversalAdditionalCameraData().renderPostProcessing = overlayPostProcessing;
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
