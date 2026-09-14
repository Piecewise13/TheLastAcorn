using Player;
using UnityEngine;

/// <summary>
/// The look-ahead on default player follow: the camera target is pushed ahead of the player in the
/// direction they are travelling, so the frame shows what is coming rather than centring on where
/// they already are.
///
/// Three properties are deliberate:
///
/// <list type="bullet">
/// <item>It applies <b>only to default follow</b>. A <see cref="CameraZone"/> or a cinematic frames
/// its own shot, and nudging that off-centre by the player's speed would break authored framing —
/// so while any claim holds the position, the lead eases to zero instead of being applied.</item>
/// <item>It is applied <b>before</b> the confiner clamp, so the level bounds still have the last
/// word. A lead that would push past the edge of the level simply clamps back.</item>
/// <item>It is sized as a <b>fraction of the current view</b>, not a fixed distance. The velocity
/// zoom widens the frame as speed builds; a lead in world units would shrink on screen at exactly
/// the moment more reveal is wanted.</item>
/// </list>
/// </summary>
public partial class CameraDirector
{
    [Header("Look-ahead")]
    [Tooltip("Whether default follow pushes the camera ahead of the player. Off centres the player.")]
    [SerializeField] private bool lookAheadEnabled = true;

    [Tooltip("Horizontal lead at top speed, as a fraction of the view's half-width. 0.18 puts the " +
             "player a little under a fifth of the way back from centre at full glide.")]
    [SerializeField, Range(0f, 0.6f)] private float lookAheadFractionX = 0.18f;

    [Tooltip("Vertical lead at reference speed, as a fraction of the view's half-height. Kept small: " +
             "the velocity zoom is already opening the frame vertically on a fast descent.")]
    [SerializeField, Range(0f, 0.6f)] private float lookAheadFractionY = 0.08f;

    [Tooltip("Shape of the ramp from standstill to reference speed. Input and output are both " +
             "normalized, so this curve is independent of the player's upgrade level.")]
    [SerializeField] private AnimationCurve lookAheadCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Vertical speed at which the vertical lead reaches its maximum. Separate from the " +
             "horizontal reference because falling is not capped the way gliding is.")]
    [SerializeField] private float lookAheadVerticalReferenceSpeed = 25f;

    [Tooltip("Horizontal reference speed used when the player's move manager cannot be resolved. " +
             "Normally max glide speed wins, so the ramp scales with the Gliding upgrade.")]
    [SerializeField] private float lookAheadFallbackReferenceSpeed = 25f;

    [Tooltip("Seconds to ease the lead outward as speed builds. Slow on purpose, so a speed wobble " +
             "does not pump the frame.")]
    [SerializeField] private float lookAheadExtendTime = 0.35f;

    [Tooltip("Seconds to ease the lead back to centre after slowing or stopping. Faster than the " +
             "extend time so the player is not left staring at ground they are no longer heading for.")]
    [SerializeField] private float lookAheadReturnTime = 0.6f;

    [Tooltip("Speed below which the lead's direction stops updating. Without this, hovering in a " +
             "gust crosses zero every few frames and the frame swings across the player.")]
    [SerializeField] private float lookAheadSpeedDeadzone = 0.5f;

    private const float FallbackAspect = 16f / 9f;

    private Vector2 lookAhead;
    private Vector2 lookAheadSmoothVelocity;

    // The frame the ease was last advanced on. See the guard in UpdateLookAhead.
    private int lookAheadFrame = -1;

    // Latched travel direction per axis, not read straight off velocity — see the deadzone tooltip.
    private float lookAheadSignX = 1f;
    private float lookAheadSignY;

    private PlayerMoveManager cachedPlayerMove;

    /// <summary>The offset currently applied to default follow. Exposed for the tracking debug log.</summary>
    private Vector2 AppliedLookAhead => lookAhead;

    /// <summary>
    /// Advances the lead one frame and hands back the offset to apply. Call every frame, including
    /// the frames a claim owns the position — passing <c>false</c> there is what makes the lead ease
    /// to zero under an authored frame instead of freezing and reappearing on release.
    /// </summary>
    private Vector2 UpdateLookAhead(bool active)
    {
        // Advanced at most once per frame. The position it feeds is read from several places in a
        // single frame — LateUpdate, Start's first framing, every SnapNow — and advancing the ease
        // per read would make the lead's speed depend on how many times it was asked for.
        if (lookAheadFrame == Time.frameCount) return lookAhead;
        lookAheadFrame = Time.frameCount;

        if (!lookAheadEnabled)
        {
            lookAhead = Vector2.zero;
            lookAheadSmoothVelocity = Vector2.zero;
            return Vector2.zero;
        }

        Vector2 target = active ? ResolveLookAheadTarget() : Vector2.zero;

        // Extending and returning get separate times. Reaching outward as speed builds should be
        // unhurried, while recentring after a landing should be prompt — one shared time can only
        // ever satisfy one of them. Same asymmetry as the velocity zoom's two blend rates.
        float smoothTime = target.sqrMagnitude > lookAhead.sqrMagnitude
            ? lookAheadExtendTime
            : lookAheadReturnTime;

        lookAhead = Vector2.SmoothDamp(lookAhead, target, ref lookAheadSmoothVelocity, smoothTime,
                                       Mathf.Infinity, Time.deltaTime);
        return lookAhead;
    }

    /// <summary>
    /// Drops the lead to nothing with no travel. Snap paths call this: a teleport zeroes the player's
    /// velocity, so carrying the previous frame's lead across would show as a slide right after a
    /// snap that is meant to be instant.
    /// </summary>
    private void ResetLookAhead()
    {
        lookAhead = Vector2.zero;
        lookAheadSmoothVelocity = Vector2.zero;

        // Claims the frame as well, so the snap's own position read cannot immediately ease the lead
        // back out and reintroduce the offset the reset just removed.
        lookAheadFrame = Time.frameCount;
    }

    private Vector2 ResolveLookAheadTarget()
    {
        Vector2 velocity = ResolvePlayerVelocity();

        float halfHeight = appliedSize;
        float halfWidth = appliedSize * CurrentAspect;

        float x = LeadAxis(velocity.x, ref lookAheadSignX, HorizontalReferenceSpeed,
                           lookAheadFractionX * halfWidth);
        float y = LeadAxis(velocity.y, ref lookAheadSignY, lookAheadVerticalReferenceSpeed,
                           lookAheadFractionY * halfHeight);

        return new Vector2(x, y);
    }

    private float LeadAxis(float velocity, ref float sign, float referenceSpeed, float maxLead)
    {
        if (velocity > lookAheadSpeedDeadzone) sign = 1f;
        else if (velocity < -lookAheadSpeedDeadzone) sign = -1f;

        if (referenceSpeed <= 0f || maxLead <= 0f) return 0f;

        // Slowing below the deadzone leaves the sign latched but drives the ramp to zero, so the
        // lead collapses toward the player rather than flipping to the other side of them.
        float t = Mathf.Clamp01(Mathf.Abs(velocity) / referenceSpeed);
        return sign * maxLead * Ease(lookAheadCurve, t);
    }

    /// <summary>
    /// Normalized against the player's <em>current</em> top glide speed, so the ramp keeps the same
    /// shape at every Gliding upgrade level rather than saturating early once the cap rises.
    /// </summary>
    private float HorizontalReferenceSpeed
    {
        get
        {
            PlayerMoveManager move = PlayerMove;
            return move != null && move.MaxGlideSpeed > 0f
                ? move.MaxGlideSpeed
                : lookAheadFallbackReferenceSpeed;
        }
    }

    private Vector2 ResolvePlayerVelocity()
    {
        PlayerMoveManager move = PlayerMove;
        return move != null ? move.Velocity : Vector2.zero;
    }

    private PlayerMoveManager PlayerMove
    {
        get
        {
            if (cachedPlayerMove != null) return cachedPlayerMove;

            Transform player = ResolvePlayerTransform();
            cachedPlayerMove = player != null ? player.GetComponentInChildren<PlayerMoveManager>() : null;
            return cachedPlayerMove;
        }
    }

    /// <summary>
    /// Aspect from the camera actually rendering the world, so the horizontal lead is a true fraction
    /// of what the player can see rather than of an assumed 16:9.
    /// </summary>
    private float CurrentAspect
    {
        get
        {
            Camera foreground = rig != null ? rig.Foreground : null;
            return foreground != null && foreground.aspect > 0f ? foreground.aspect : FallbackAspect;
        }
    }
}
