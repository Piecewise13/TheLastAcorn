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
[ExecuteAlways]
[DefaultExecutionOrder(100)]
public class CameraDirector : SceneService<CameraDirector>
{
    [Header("Defaults")]
    [Tooltip("Orthographic half-height used when no claim specifies a zoom.")]
    [SerializeField] private float defaultOrthographicSize = 10f;

    [Tooltip("Fallback transition when a claim does not specify its own.")]
    [SerializeField] private float defaultTransitionDuration = 0.6f;
    [SerializeField] private AnimationCurve defaultTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Follow target")]
    [Tooltip("The ghost the camera follows and the director moves. This component is its sole owner. " +
             "Left empty, a CameraGhost authored elsewhere in the scene is used.")]
    [SerializeField] private CameraGhost ghost;

    [Tooltip("Spawned at the player only when no ghost is authored anywhere. " +
             "Left manager-less it clamps to the active zone.")]
    [SerializeField] private CameraGhost ghostPrefab;

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

    /// <summary>
    /// The transform the virtual camera tracks: the scene's authored ghost (or a spawned fallback).
    /// The ghost follows the player when nothing claims the camera; the director overwrites its
    /// position while a claim is active.
    /// </summary>
    private Transform driven;

    // The position the director actually wrote last frame. The ghost's own LateUpdate runs first and
    // stomps its transform back onto the player, so we cannot read the camera's real position off
    // `driven` — this remembers it so a blend starts from where the camera truly was, not the stomp.
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

        // The camera tracks the ghost authored in the scene — the thing already following the player —
        // rather than a target we spawn on top of it. The director drives this transform while a claim
        // is active and leaves the ghost's own clamped follow alone otherwise.
        ghost = ResolveOrSpawnGhost();
        if (ghost == null)
        {
            Debug.LogError($"[{nameof(CameraDirector)}] No {nameof(CameraGhost)} authored and no {nameof(ghostPrefab)} to spawn. The camera will not follow.", this);
            enabled = false;
            return;
        }

        driven = ghost.transform;
        positionSource = driven;
        lastAppliedPosition = driven.position;
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
        // In edit mode there is no claim pipeline; just point the vcam at the ghost so moving it in the
        // scene previews the shot. Cinemachine drives the game view from the target in edit mode.
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
        CameraGhost previewGhost = FindAnyObjectByType<CameraGhost>();
        if (cam == null || previewGhost == null) return;

        if (cam.Target.TrackingTarget != previewGhost.transform)
        {
            cam.Target.TrackingTarget = previewGhost.transform;
        }
    }
#endif

    private void UpdatePosition()
    {
        CameraClaim claim = TopClaim(requireTarget: true);
        Transform desiredSource = claim != null ? claim.Target : BoundedTarget();
        if (desiredSource == null) return;

        if (desiredSource != positionSource)
        {
            BeginPositionBlend(claim, desiredSource);
        }

        Vector3 desired = desiredSource.position;
        desired.z = driven.position.z;

        // Steady state with no claim: the ghost's own LateUpdate already parked it on the player this
        // frame, so `desired` is that position and writing it back is a no-op — Cinemachine damping
        // does the smoothing. A blend interpolates from where the camera actually was (lastApplied,
        // not the ghost's stomped transform) toward the live destination.
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
    }

    // Re-anchors the camera when the active area changes: makes the bounded target current against the
    // new confiner (so a snap does not read the stale position), then parks on whatever zone now holds
    // the reframe point, or on the bounded follow if the point is in a gap.
    private void OnActiveAreaChanged(CameraZoneManager area)
    {
        if (rig == null || vcam == null) return; // event fired before Start; first-frame framing is Start's job

        Vector2 point = pendingReframe ?? ResolvePlayerPosition();
        pendingReframe = null;

        // Pull the new area's ghost onto the player and clamp it now, so the snap below reads its
        // current position rather than wherever it happened to sit.
        ActiveGhost()?.SnapToTarget();

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

    // Resolves the one ghost the camera follows: the reference authored on this director first, then
    // any CameraGhost placed in the scene. If none is authored, spawns the prefab at the player — left
    // manager-less, so CameraGhost clamps it to whatever zone is active.
    private CameraGhost ResolveOrSpawnGhost()
    {
        if (ghost != null) return ghost;

        CameraGhost found = FindAnyObjectByType<CameraGhost>();
        if (found != null) return found;

        if (ghostPrefab == null) return null;

        CameraGhost spawned = Instantiate(ghostPrefab, ResolvePlayerPosition(), Quaternion.identity);
        spawned.name = ghostPrefab.name;
        return spawned;
    }

    // The camera's follow target is the single ghost resolved at Start; there is no per-area target
    // to swap, so the bounded source is always that ghost.
    private Transform BoundedTarget() => driven;

    private CameraGhost ActiveGhost() => ghost;

    private static Vector2 ResolvePlayerPosition()
    {
        if (PlayerStateManager.Instance != null && PlayerStateManager.Instance.playerGameObject != null)
        {
            return PlayerStateManager.Instance.playerGameObject.transform.position;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? (Vector2)tagged.transform.position : Vector2.zero;
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
        Transform source = positionClaim != null ? positionClaim.Target : BoundedTarget();
        if (source != null)
        {
            Vector3 position = source.position;
            position.z = driven.position.z;
            driven.position = position;
            lastAppliedPosition = position;
            positionSource = source; // so the next LateUpdate doesn't blend away from the snap
        }

        CameraClaim claim = TopClaim(requireTarget: false);
        zoomSource = claim;
        zoomInitialised = true;
        ApplySize(claim?.OrthographicSize ?? defaultOrthographicSize);
    }
}
