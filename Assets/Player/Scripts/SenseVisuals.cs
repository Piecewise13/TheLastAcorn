using UnityEngine;
using UnityEngine.Rendering;

namespace Player
{
/// <summary>
/// The look of sensing. While the zoom-out (senses) is held the scent trails glow brighter than
/// white, far enough past the scene's bloom threshold to bloom, and the SenseOverlay full-screen
/// pass grays the world and draws scent waves radiating from the player.
/// </summary>
public class SenseVisuals : MonoBehaviour
{
    [Tooltip("Trail colour multiplier at full sense. Has to push the trail past the scene's bloom " +
             "threshold (5.42 in Level 1) for it to glow.")]
    [SerializeField] private float sensedGlow = 6f;

    [Tooltip("Seconds to fade fully in or out.")]
    [SerializeField] private float fadeSeconds = 0.35f;

    private static readonly int SenseAmountId = Shader.PropertyToID("_SenseAmount");
    private static readonly int SenseCenterId = Shader.PropertyToID("_SenseCenter");

    private PlayerCameraManager playerCamera;
    private Transform waveOrigin;

    private bool sensing;
    private float amount;

    private void Start()
    {
        playerCamera = GetComponentInParent<PlayerCameraManager>();
        if (playerCamera != null)
        {
            playerCamera.OnZoomStarted += HandleZoomStarted;
            playerCamera.OnZoomEnded += HandleZoomEnded;
        }
        else
        {
            Debug.LogError("SenseVisuals: no PlayerCameraManager found in parents.", this);
        }

        PlayerSenses senses = GetComponent<PlayerSenses>();
        waveOrigin = senses != null && senses.TrailOrigin != null ? senses.TrailOrigin : transform;

        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;

        Apply();
    }

    private void OnDestroy()
    {
        if (playerCamera != null)
        {
            playerCamera.OnZoomStarted -= HandleZoomStarted;
            playerCamera.OnZoomEnded -= HandleZoomEnded;
        }

        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;

        // Shader globals outlive this player body too; don't leave the world grayed.
        Shader.SetGlobalFloat(SenseAmountId, 0f);

        // The trail manager outlives this player body; don't leave its trails glowing.
        if (ScentTrailManager.Instance != null)
        {
            ScentTrailManager.Instance.SetGlow(1f);
        }
    }

    private void HandleZoomStarted() => sensing = true;

    private void HandleZoomEnded() => sensing = false;

    private void Update()
    {
        float target = sensing ? 1f : 0f;
        if (Mathf.Approximately(amount, target)) return;

        amount = fadeSeconds > 0f ? Mathf.MoveTowards(amount, target, Time.deltaTime / fadeSeconds) : target;
        Apply();
    }

    // Right before each camera renders, so the wave centre matches where the camera actually ended
    // up this frame (it moves in LateUpdate) instead of lagging a frame behind.
    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (amount <= 0f || waveOrigin == null) return;
        Shader.SetGlobalVector(SenseCenterId, cam.WorldToViewportPoint(waveOrigin.position));
    }

    private void Apply()
    {
        // Eased once here so everything driven by it (the overlay's saturation and wave reveal,
        // the trail glow) moves on the same curve and lands at the same moment.
        float eased = Mathf.SmoothStep(0f, 1f, amount);

        Shader.SetGlobalFloat(SenseAmountId, eased);

        if (ScentTrailManager.Instance != null)
        {
            ScentTrailManager.Instance.SetGlow(Mathf.Lerp(1f, sensedGlow, eased));
        }
    }
}
}
