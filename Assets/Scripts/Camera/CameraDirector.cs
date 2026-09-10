using System.Collections.Generic;
using Player;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

/// <summary>
/// Owns camera position and zoom through prioritized claims. The vcam tracks one
/// director-driven target; claims move it directly, while default follow clamps the player to the
/// active camera area.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(100)]
public class CameraDirector : SceneService<CameraDirector>
{
    private const string DefaultTrackingTargetName = "CameraTarget";
    private const float CameraTrackingDebugInterval = 0.25f;

    [Header("Defaults")]
    [Tooltip("Orthographic half-height used when no claim specifies a zoom.")]
    [SerializeField] private float defaultOrthographicSize = 10f;

    [Tooltip("Fallback transition when a claim does not specify its own.")]
    [SerializeField] private float defaultTransitionDuration = 0.6f;
    [SerializeField] private AnimationCurve defaultTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Tracking target")]
    [Tooltip("Transform the Cinemachine camera follows. CameraDirector is the only runtime writer.")]
    [SerializeField] private Transform trackingTarget;

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

    /// <summary>The transform the virtual camera tracks. CameraDirector is its runtime owner.</summary>
    private Transform driven;

    // The position the director actually wrote last frame. Blends start from here rather than from a
    // target that may have been moved by editor tooling or scene authoring.
    private Vector3 lastAppliedPosition;

    // A reframe point handed in by SwitchArea, consumed the next time the active area changes. Null
    // means "reframe on the player" — the usual case, since the player has just been teleported.
    private Vector2? pendingReframe;

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
    private float nextCameraTrackingDebugLogTime;

    public float DefaultOrthographicSize => defaultOrthographicSize;

    protected override void OnEnable()
    {
        base.OnEnable(); // SceneService registration
        // The active area owns which confiner and zones are live; the director owns where the camera
        // sits. When the area changes it re-anchors, so the switch is driven from here.
        CameraZoneManager.ActiveChanged += OnActiveAreaChanged;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        CameraZoneManager.ActiveChanged -= OnActiveAreaChanged;
    }

    private void Start()
    {
        // Edit-mode preview (see LateUpdate) needs no runtime setup: no driven target, no claims.
        if (!Application.isPlaying) return;

        rig = CameraRig.For(gameObject.scene);
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

        driven = ResolveTrackingTarget();
        if (driven == null)
        {
            Debug.LogError($"[{nameof(CameraDirector)}] No tracking target assigned. Add a child named " +
                           $"'{DefaultTrackingTargetName}' or assign {nameof(trackingTarget)}.", this);
            enabled = false;
            return;
        }

        positionSource = null;
        lastAppliedPosition = ResolveDefaultPosition();
        driven.position = lastAppliedPosition;
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

    public CameraClaim HoldCurrentZoom(CameraPriority priority)
    {
        return Request(priority)
            .WithOrthographicSize(appliedSize)
            .WithInstantBlend();
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
#if UNITY_EDITOR
        // In edit mode there is no claim pipeline; just keep the vcam pointed at the authored target.
        if (!Application.isPlaying)
        {
            EditorPreviewFollow();
            return;
        }
#endif

        if (rig == null || vcam == null) return;

        UpdatePosition();
        UpdateZoom();
    }

#if UNITY_EDITOR
    private void EditorPreviewFollow()
    {
        // Registries are play-mode only, so resolve by search in edit mode.
        CameraRig editorRig = CameraRig.For(gameObject.scene);
        if (editorRig == null) editorRig = FindAnyObjectByType<CameraRig>();

        CinemachineCamera cam = editorRig != null ? editorRig.Vcam : null;
        Transform previewTarget = ResolveTrackingTarget();
        if (cam == null || previewTarget == null) return;

        if (cam.Target.TrackingTarget != previewTarget)
        {
            cam.Target.TrackingTarget = previewTarget;
        }
    }
#endif

    private void UpdatePosition()
    {
        CameraClaim claim = TopClaim(requireTarget: true);
        Transform desiredSource = claim != null ? claim.Target : null;

        if (desiredSource != positionSource)
        {
            BeginPositionBlend(claim, desiredSource);
        }

        Vector3 desired = claim != null ? claim.Target.position : ResolveDefaultPosition();
        desired.z = driven.position.z;

        // With no claim, the tracking target follows the player clamped to the active area and
        // Cinemachine damping supplies the movement. Claims bypass the confiner.
        Vector3 next;
        if (positionBlending)
        {
            switch (positionBlendMode)
            {
                case CameraBlendMode.Instant:
                    next = desired;
                    positionBlending = false;
                    break;

                case CameraBlendMode.Exponential:
                    next = Vector3.Lerp(lastAppliedPosition, desired, ExponentialStep(positionBlendRate));
                    if ((next - desired).sqrMagnitude < 0.0001f) positionBlending = false;
                    break;

                default:
                    positionBlendElapsed += Time.deltaTime;
                    float t = positionBlendDuration <= 0f ? 1f : Mathf.Clamp01(positionBlendElapsed / positionBlendDuration);
                    // Lerping toward the live target rather than a snapshot, so a moving destination
                    // (the bounded follow target) still converges.
                    next = Vector3.LerpUnclamped(positionBlendFrom, desired, Ease(positionBlendCurve, t));
                    if (t >= 1f) positionBlending = false;
                    break;
            }
        }
        else
        {
            next = desired;
        }

        driven.position = next;
        lastAppliedPosition = next;

        // Cinemachine's own damping has to stand down while the director owns the position,
        // otherwise the authored ease curve is smeared by a second layer of smoothing.
        SuppressDamping(claim != null || positionBlending);
        LogCameraTrackingDebug(claim, desired, next);
    }

    private void LogCameraTrackingDebug(CameraClaim claim, Vector3 desired, Vector3 applied)
    {
        if (PlayerStateManager.Instance == null || PlayerStateManager.Instance.CurrentState != PlayerState.Glide) return;
        if (Time.time < nextCameraTrackingDebugLogTime) return;

        nextCameraTrackingDebugLogTime = Time.time + CameraTrackingDebugInterval;

        Transform player = ResolvePlayerTransform();
        Vector3 playerPosition = player != null ? player.position : Vector3.zero;
        string claimInfo = claim != null
            ? $"{claim.Priority}:{(claim.Target != null ? claim.Target.name : "null")}"
            : "DefaultFollow";
        string areaName = CameraZoneManager.Active != null ? CameraZoneManager.Active.name : "none";
        bool hasConfiner = CameraZoneManager.Active != null && CameraZoneManager.Active.Confiner != null;
        float targetToPlayerX = player != null ? applied.x - playerPosition.x : 0f;

        Debug.Log($"[CameraTrackDebug] t={Time.time:F2} state={PlayerStateManager.Instance.CurrentState} " +
                  $"claim={claimInfo} trackingTarget={(driven != null ? driven.name : "null")} " +
                  $"targetPos={applied} desired={desired} playerPos={playerPosition} " +
                  $"targetMinusPlayerX={targetToPlayerX:F3} activeArea={areaName} hasConfiner={hasConfiner} " +
                  $"blending={positionBlending} dampingSuppressed={dampingSuppressed}", this);
    }

    private void BeginPositionBlend(CameraClaim claim, Transform desiredSource)
    {
        positionSource = desiredSource;
        positionBlendFrom = lastAppliedPosition;
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
    /// Switches the active camera area to <paramref name="manager"/>. The director re-anchors on the
    /// change (see <see cref="OnActiveAreaChanged"/>), re-framing on whatever zone now holds the
    /// player. Call after teleporting the player between areas that share one scene.
    /// </summary>
    public void SwitchArea(CameraZoneManager manager)
    {
        if (manager != null) CameraZoneManager.SetActive(manager);
    }

    /// <summary>
    /// Switches the active camera area and re-frames on <paramref name="reframeAt"/> rather than the
    /// player — for when the framing point differs from the player's landing spot.
    /// </summary>
    public void SwitchArea(CameraZoneManager manager, Vector2 reframeAt)
    {
        if (manager == null) return;

        pendingReframe = reframeAt;
        CameraZoneManager.SetActive(manager);

        // SetActive is a no-op when the area is already active, so ActiveChanged never fires and the
        // reframe is stranded — a teleport within the current area would then let Cinemachine lerp to
        // the player instead of snapping. Consume it here in that case so the switch is always instant.
        if (pendingReframe.HasValue)
        {
            pendingReframe = null;
            ReanchorTo(reframeAt);
        }
    }

    // Re-anchors the camera when the active area changes, then parks on whatever zone now holds the
    // reframe point, or on bounded player follow if the point is in a gap.
    private void OnActiveAreaChanged(CameraZoneManager area)
    {
        if (rig == null || vcam == null) return; // event fired before Start; first-frame framing is Start's job

        Vector2 point = pendingReframe ?? ResolvePlayerPosition();
        pendingReframe = null;
        ReanchorTo(point);
    }

    // Snaps the camera onto a freshly teleported point with no travel, using a zone frame if one
    // contains the point and bounded player follow otherwise.
    private void ReanchorTo(Vector2 point)
    {
        if (rig == null || vcam == null) return;

        CameraZone zone = CameraZoneManager.ResolveAt(point);
        if (zone != null)
        {
            zone.ApplyImmediate();
        }
        else
        {
            SnapNow();
        }
    }

    private Transform ResolveTrackingTarget()
    {
        if (trackingTarget != null) return trackingTarget;

        return transform.Find(DefaultTrackingTargetName);
    }

    private Vector3 ResolveDefaultPosition()
    {
        Transform player = ResolvePlayerTransform();
        Vector3 position = player != null ? player.position : driven.position;
        PolygonCollider2D confiner = CameraZoneManager.Active != null ? CameraZoneManager.Active.Confiner : null;
        if (confiner != null)
        {
            Vector2 clamped = confiner.ClosestPoint(position);
            position = new Vector3(clamped.x, clamped.y, position.z);
        }

        position.z = driven.position.z;
        return position;
    }

    private static Vector2 ResolvePlayerPosition()
    {
        Transform player = ResolvePlayerTransform();
        if (player != null) return player.position;

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? (Vector2)tagged.transform.position : Vector2.zero;
    }

    private static Transform ResolvePlayerTransform()
    {
        if (PlayerStateManager.Instance != null && PlayerStateManager.Instance.playerGameObject != null)
        {
            return PlayerStateManager.Instance.playerGameObject.transform;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }

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
        Transform source = positionClaim != null ? positionClaim.Target : null;
        Vector3 position = source != null ? source.position : ResolveDefaultPosition();
        position.z = driven.position.z;
        driven.position = position;
        lastAppliedPosition = position;
        positionSource = source; // so the next LateUpdate doesn't blend away from the snap

        CameraClaim claim = TopClaim(requireTarget: false);
        zoomSource = claim;
        zoomInitialised = true;
        float targetSize = claim?.OrthographicSize ?? defaultOrthographicSize;
        if (!Mathf.Approximately(targetSize, appliedSize))
        {
            ApplySize(targetSize);
        }
    }
}
