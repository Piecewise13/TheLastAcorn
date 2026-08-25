using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

/// <summary>
/// The single owner of where the camera sits and how far it is zoomed. Everything that wants to
/// move or resize the camera requests a <see cref="CameraClaim"/> and holds it; the highest-priority
/// active claim wins, and releasing falls back to whatever sits underneath. An empty stack is the
/// default bounded follow, so "nothing is controlling the camera" needs no special handling.
///
/// This exists because four systems used to set the tracking target and zoom independently, which
/// let a player input steal the camera in the middle of a scripted beat and made it impossible for
/// one cave room to hand off to the next.
///
/// Runs after <see cref="CameraGhost"/>, which writes the bounded follow target in LateUpdate.
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraDirector : MonoBehaviour
{
    public static CameraDirector Instance { get; private set; }

    [Header("Defaults")]
    [Tooltip("Orthographic half-height used when no claim specifies a zoom.")]
    [SerializeField] private float defaultOrthographicSize = 10f;

    [Tooltip("Fallback transition when a claim does not specify its own.")]
    [SerializeField] private float defaultTransitionDuration = 0.6f;
    [SerializeField] private AnimationCurve defaultTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Background camera")]
    [Tooltip("Perspective FOV of the background camera at the reference orthographic size.")]
    [SerializeField] private float backgroundBaseFieldOfView = 80f;
    [Tooltip("How much the background FOV scales with zoom. 0 keeps it fixed.")]
    [SerializeField] private float backgroundFieldOfViewMultiplier = 0.3f;

    private readonly List<CameraClaim> claims = new();
    private long nextSequence;

    private CameraRig rig;
    private CinemachineCamera vcam;
    private CinemachineFollow follow;

    /// <summary>The transform the virtual camera always tracks. The director decides where it is.</summary>
    private Transform driven;

    private Vector3 defaultPositionDamping;
    private bool dampingSuppressed;

    private Transform positionSource;
    private bool positionBlending;
    private Vector3 positionBlendFrom;
    private float positionBlendElapsed;
    private float positionBlendDuration;
    private AnimationCurve positionBlendCurve;
    private CameraBlendMode positionBlendMode;
    private float positionBlendRate;

    private CameraClaim zoomSource;
    private bool zoomInitialised;
    private float appliedSize;
    private float zoomBlendFrom;
    private float zoomBlendElapsed;
    private float zoomBlendDuration;
    private AnimationCurve zoomBlendCurve;
    private CameraBlendMode zoomBlendMode;
    private float zoomBlendRate;

    public float DefaultOrthographicSize => defaultOrthographicSize;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        rig = CameraRig.Instance;
        if (rig == null)
        {
            Debug.LogError($"[{nameof(CameraDirector)}] No {nameof(CameraRig)} in the scene. The camera will not be driven.", this);
            enabled = false;
            return;
        }

        vcam = rig.Vcam;
        follow = vcam != null ? vcam.GetComponent<CinemachineFollow>() : null;
        if (follow != null)
        {
            defaultPositionDamping = follow.TrackerSettings.PositionDamping;
        }

        // The virtual camera tracks this one transform for the rest of its life. Claims change where
        // the director puts it rather than swapping the camera's target, so there is exactly one
        // writer and no ordering question between callers.
        driven = new GameObject("Camera Director Target").transform;
        driven.SetParent(transform, false);
        driven.position = rig.BoundedCameraTarget != null ? rig.BoundedCameraTarget.position : transform.position;

        positionSource = rig.BoundedCameraTarget;
        vcam.Target.TrackingTarget = driven;

        appliedSize = vcam.Lens.OrthographicSize;

        // With no claim yet, the blend fields are never touched by the claim-changed path, but the
        // default blend mode still routes through them every frame. They have to be valid from here.
        positionBlendMode = CameraBlendMode.Duration;
        positionBlendDuration = defaultTransitionDuration;
        positionBlendCurve = defaultTransitionCurve;
        zoomBlendMode = CameraBlendMode.Duration;
        zoomBlendDuration = defaultTransitionDuration;
        zoomBlendCurve = defaultTransitionCurve;
        zoomBlendFrom = appliedSize;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Takes a claim on the camera. Hold it for as long as you need control and release it when
    /// done — the camera returns to whatever was underneath on its own.
    /// </summary>
    public CameraClaim Request(CameraPriority priority)
    {
        var claim = new CameraClaim(this, priority, nextSequence++);
        claims.Add(claim);
        return claim;
    }

    internal void Release(CameraClaim claim)
    {
        claims.Remove(claim);
    }

    /// <summary>The claim currently deciding the camera's position, or null when it is the default follow.</summary>
    public CameraClaim ActivePositionClaim => TopClaim(requireTarget: true);

    /// <summary>The claim currently deciding the zoom, or null when it is the default size.</summary>
    public CameraClaim ActiveZoomClaim => TopClaim(requireTarget: false);

    /// <summary>
    /// Whether anything at or above the given priority is holding the camera. Callers use this to
    /// stand down rather than fight — the player's zoom input checks it so a cave room's authored
    /// framing is not overridden.
    /// </summary>
    public bool HasClaimAtOrAbove(CameraPriority priority)
    {
        for (int i = 0; i < claims.Count; i++)
        {
            if (claims[i].Priority >= priority) return true;
        }

        return false;
    }

    private CameraClaim TopClaim(bool requireTarget)
    {
        CameraClaim best = null;
        for (int i = 0; i < claims.Count; i++)
        {
            CameraClaim candidate = claims[i];
            bool usable = requireTarget ? candidate.Target != null : candidate.OrthographicSize.HasValue;
            if (!usable) continue;

            if (best == null
                || candidate.Priority > best.Priority
                || (candidate.Priority == best.Priority && candidate.Sequence > best.Sequence))
            {
                best = candidate;
            }
        }

        return best;
    }

    private void LateUpdate()
    {
        if (rig == null || vcam == null) return;

        UpdatePosition();
        UpdateZoom();
    }

    private void UpdatePosition()
    {
        CameraClaim claim = TopClaim(requireTarget: true);
        Transform desiredSource = claim != null ? claim.Target : rig.BoundedCameraTarget;
        if (desiredSource == null) return;

        if (desiredSource != positionSource)
        {
            BeginPositionBlend(claim, desiredSource);
        }

        Vector3 desired = desiredSource.position;
        desired.z = driven.position.z;

        if (positionBlending)
        {
            switch (positionBlendMode)
            {
                case CameraBlendMode.Instant:
                    driven.position = desired;
                    positionBlending = false;
                    break;

                case CameraBlendMode.Exponential:
                    driven.position = Vector3.Lerp(driven.position, desired, ExponentialStep(positionBlendRate));
                    if ((driven.position - desired).sqrMagnitude < 0.0001f) positionBlending = false;
                    break;

                default:
                    positionBlendElapsed += Time.deltaTime;
                    float t = positionBlendDuration <= 0f ? 1f : Mathf.Clamp01(positionBlendElapsed / positionBlendDuration);
                    // Lerping toward the live target rather than a snapshot, so a moving destination
                    // (the bounded follow target) still converges.
                    driven.position = Vector3.LerpUnclamped(positionBlendFrom, desired, Ease(positionBlendCurve, t));
                    if (t >= 1f) positionBlending = false;
                    break;
            }
        }
        else
        {
            driven.position = desired;
        }

        // Cinemachine's own damping has to stand down while the director owns the position,
        // otherwise the authored ease curve is smeared by a second layer of smoothing.
        SuppressDamping(claim != null || positionBlending);
    }

    private void BeginPositionBlend(CameraClaim claim, Transform desiredSource)
    {
        positionSource = desiredSource;
        positionBlendFrom = driven.position;
        positionBlendElapsed = 0f;
        positionBlending = true;

        positionBlendMode = claim != null ? claim.BlendMode : CameraBlendMode.Duration;
        positionBlendDuration = claim != null && claim.Duration > 0f ? claim.Duration : defaultTransitionDuration;
        positionBlendCurve = claim?.Curve ?? defaultTransitionCurve;
        positionBlendRate = claim != null ? claim.Rate : 0f;

        if (positionBlendMode == CameraBlendMode.Exponential && positionBlendRate <= 0f)
        {
            positionBlendMode = CameraBlendMode.Duration;
        }
    }

    private void UpdateZoom()
    {
        CameraClaim claim = TopClaim(requireTarget: false);
        float desired = claim?.OrthographicSize ?? defaultOrthographicSize;

        if (!zoomInitialised)
        {
            zoomInitialised = true;
            zoomSource = claim;
            appliedSize = desired;
            ApplySize(desired);
            return;
        }

        if (claim != zoomSource)
        {
            zoomSource = claim;
            zoomBlendFrom = appliedSize;
            zoomBlendElapsed = 0f;
            zoomBlendMode = claim != null ? claim.BlendMode : CameraBlendMode.Duration;
            zoomBlendDuration = claim != null && claim.Duration > 0f ? claim.Duration : defaultTransitionDuration;
            zoomBlendCurve = claim?.Curve ?? defaultTransitionCurve;
            zoomBlendRate = claim != null ? claim.Rate : 0f;

            if (zoomBlendMode == CameraBlendMode.Exponential && zoomBlendRate <= 0f)
            {
                zoomBlendMode = CameraBlendMode.Duration;
            }
        }

        float next;
        switch (zoomBlendMode)
        {
            case CameraBlendMode.Instant:
                next = desired;
                break;

            case CameraBlendMode.Exponential:
                next = Mathf.Lerp(appliedSize, desired, ExponentialStep(zoomBlendRate));
                if (Mathf.Abs(next - desired) < 0.01f) next = desired;
                break;

            default:
                zoomBlendElapsed += Time.deltaTime;
                float t = zoomBlendDuration <= 0f ? 1f : Mathf.Clamp01(zoomBlendElapsed / zoomBlendDuration);
                next = Mathf.LerpUnclamped(zoomBlendFrom, desired, Ease(zoomBlendCurve, t));
                break;
        }

        if (!Mathf.Approximately(next, appliedSize))
        {
            ApplySize(next);
        }
    }

    private void ApplySize(float size)
    {
        appliedSize = size;

        LensSettings lens = vcam.Lens;
        lens.OrthographicSize = size;
        vcam.Lens = lens;

        if (rig.Overlay != null)
        {
            rig.Overlay.orthographicSize = size;
        }

        if (rig.Background != null && defaultOrthographicSize > 0f)
        {
            float ratio = size / defaultOrthographicSize;
            float scale = 1f + (ratio - 1f) * backgroundFieldOfViewMultiplier;
            rig.Background.fieldOfView = backgroundBaseFieldOfView * scale;
        }
    }

    private void SuppressDamping(bool suppress)
    {
        if (follow == null || suppress == dampingSuppressed) return;

        dampingSuppressed = suppress;
        TrackerSettings settings = follow.TrackerSettings;
        settings.PositionDamping = suppress ? Vector3.zero : defaultPositionDamping;
        follow.TrackerSettings = settings;
    }

    private static float ExponentialStep(float rate) => 1f - Mathf.Exp(-rate * Time.deltaTime);

    /// <summary>
    /// Curve evaluation that tolerates a missing curve by easing linearly. A serialized curve can be
    /// emptied in the Inspector, and the blend state starts out unset, so this must never assume one
    /// exists — it runs every frame and a null here stops the camera dead.
    /// </summary>
    private static float Ease(AnimationCurve curve, float t)
        => curve != null && curve.length > 0 ? curve.Evaluate(t) : t;

    /// <summary>
    /// Drops the camera straight onto whatever is currently claiming it, with no travel. Used on
    /// spawn and respawn so the player is correctly framed on the first frame they see, and by the
    /// authoring harness so a preview does not slide in.
    /// </summary>
    public void SnapNow()
    {
        if (driven == null) return;

        positionBlending = false;
        CameraClaim positionClaim = TopClaim(requireTarget: true);
        Transform source = positionClaim != null ? positionClaim.Target : rig?.BoundedCameraTarget;
        if (source != null)
        {
            Vector3 position = source.position;
            position.z = driven.position.z;
            driven.position = position;
        }

        CameraClaim claim = TopClaim(requireTarget: false);
        zoomSource = claim;
        zoomInitialised = true;
        ApplySize(claim?.OrthographicSize ?? defaultOrthographicSize);
    }
}
