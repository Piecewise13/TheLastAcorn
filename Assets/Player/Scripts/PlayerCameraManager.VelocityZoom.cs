using UnityEngine;
using UnityEngine.Serialization;

namespace Player
{
/// <summary>
/// The camera's default framing: the faster the squirrel travels horizontally, the further the
/// camera pulls back. This is the baseline the camera falls back to, not an effect that has to be
/// switched on — it claims at <see cref="CameraPriority.GlideZoom"/>, the lowest priority there is,
/// so any <see cref="CameraZone"/> a section authors overrides it just by existing. That is the
/// whole integration with the zone system; there is no arming, disarming or per-zone opt-out.
///
/// The ramp is normalized against the player's <em>current</em> top glide speed, so the curve keeps
/// exactly the same shape at every upgrade level. What the Gliding upgrade changes is where the
/// ramp lands: <see cref="maxVelocityZoom"/> climbs alongside max glide speed, so reaching the
/// widest framing is the reward for maxing the glide rather than something every level hands out at
/// its own cap.
/// </summary>
public partial class PlayerCameraManager
{
    [Header("Velocity Zoom")]
    [Tooltip("Horizontal speed at which the camera starts pulling back. Below this the zoom sits at " +
             "the minimum zoom.")]
    [SerializeField] private float velocitySpeedThreshold = 15f;

    [Tooltip("The player's normal framing: the size the camera sits at below the speed threshold, " +
             "and the floor the velocity ramp climbs from. 0 inherits the director's default size. " +
             "Raised by the Gliding upgrade via UpgradeEffectTarget.MinimumZoom.")]
    [SerializeField] private float minimumZoom;

    [Tooltip("Orthographic half-height reached at the player's current top glide speed. Raised by " +
             "the Gliding upgrade via UpgradeEffectTarget.MaxVelocityZoom.")]
    [SerializeField] private float maxVelocityZoom = 30f;

    [Tooltip("Shape of the ramp from threshold speed to top speed. Input and output are both " +
             "normalized, so this curve is upgrade-independent.")]
    [SerializeField] private AnimationCurve velocityZoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Exponential smoothing rate while pulling back as speed builds. Slow on purpose, so " +
             "a speed wobble does not pump the framing. Lower is slower.")]
    [FormerlySerializedAs("velocityZoomBlendRate")]
    [SerializeField] private float velocityZoomOutRate = 2f;

    [Tooltip("Exponential smoothing rate while tightening back to the minimum — after a landing, " +
             "most of all. Higher than the pull-back rate so the frame recovers promptly instead of " +
             "drifting in behind the player, but still eased rather than snapped.")]
    [SerializeField] private float velocityZoomInRate = 6f;

    // Which way the ramp is currently travelling. Tracked rather than recomputed from the camera's
    // applied size because the ramp's own target is what the rate should follow — the camera lags
    // behind it by design, so comparing against the camera would flip the rate mid-ease.
    private float lastVelocityZoomSize = -1f;
    private bool velocityZoomTightening;

    /// <summary>Deadzone on the direction test, so a speed jitter cannot flip the rate every frame.</summary>
    private const float VelocityZoomDirectionEpsilon = 0.01f;

    /// <summary>
    /// The rate handed to the claim, chosen by travel direction. Asymmetric on purpose: easing out
    /// as speed builds should be unhurried, while coming back in after a landing should be prompt.
    /// </summary>
    private float VelocityZoomBlendRate => velocityZoomTightening ? velocityZoomInRate : velocityZoomOutRate;

    private void Update()
    {
        SyncZoomUnlock();
        UpdateVelocityZoom();
    }

    private void UpdateVelocityZoom()
    {
        if (inputState == ZoomInputState.Disabled) return;
        if (playerMoveManager == null || PlayerStateManager.Instance == null) return;

        bool speedDriven;
        switch (PlayerStateManager.Instance.CurrentState)
        {
            case PlayerState.Glide:
            case PlayerState.Fall:
                speedDriven = true;
                break;

            // These states hand the player's body to something else. The owl holds its own
            // OwlRide claim at this same priority, and a priority tie resolves toward the newer
            // claim, so it wins while it is held and this one waits underneath.
            case PlayerState.STUNNED:
            case PlayerState.RidingOwl:
            case PlayerState.VineSwinging:
                return;

            // Grounded, climbing, locked: no speed contribution, but the claim is still held at the
            // minimum so that minimum is the player's normal framing everywhere and not just in the
            // air. Releasing it here would drop the camera back to the director's default size.
            default:
                speedDriven = false;
                break;
        }

        float t = 0f;
        if (speedDriven)
        {
            // Clamped because a gust lifts the horizontal cap above maxGlideSpeed, which would
            // otherwise push the ramp past its authored top size.
            float range = playerMoveManager.MaxGlideSpeed - velocitySpeedThreshold;
            t = range <= 0f
                ? 0f
                : Mathf.Clamp01((playerMoveManager.HorizontalSpeed - velocitySpeedThreshold) / range);
        }

        // The claim is held even at t = 0 rather than released below the threshold, so slowing down
        // eases back to the minimum zoom instead of parking on the last speed's framing.
        float size = Mathf.Lerp(MinimumZoom, maxVelocityZoom, Ease(velocityZoomCurve, t));

        if (lastVelocityZoomSize >= 0f)
        {
            if (size < lastVelocityZoomSize - VelocityZoomDirectionEpsilon) velocityZoomTightening = true;
            else if (size > lastVelocityZoomSize + VelocityZoomDirectionEpsilon) velocityZoomTightening = false;
        }

        lastVelocityZoomSize = size;
        ApplyZoom(size, ZoomEffect.VelocityZoom);
    }

    /// <summary>
    /// The player's normal zoom. An unset field falls back to the director's default so the ramp's
    /// floor matches the size the camera rests at when nothing is claiming the zoom — that is what
    /// makes engaging and releasing the glide claim invisible.
    /// </summary>
    private float MinimumZoom => minimumZoom > 0f ? minimumZoom : DefaultSize;

    /// <summary>
    /// The player's normal framing, held below the speed threshold and used as the floor the
    /// velocity ramp climbs from. Raised by the Gliding upgrade so the whole ramp — not just its top
    /// end — opens up as the glide improves. Values at or below 0 hand the floor back to the
    /// director's default.
    /// </summary>
    public void SetMinimumZoom(float amount)
    {
        minimumZoom = amount;
    }

    /// <summary>
    /// The widest the velocity zoom currently reaches. Raised by the Gliding upgrade so that the
    /// framing opens up in step with the speed that earns it.
    /// </summary>
    public void SetMaxVelocityZoom(float amount)
    {
        maxVelocityZoom = amount;
    }

    /// <summary>Tolerates a curve emptied in the Inspector by easing linearly; this runs every frame.</summary>
    private static float Ease(AnimationCurve curve, float t)
        => curve != null && curve.length > 0 ? curve.Evaluate(t) : t;
}
}
