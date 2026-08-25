using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// A framed region of the world. While a zone owns the camera, the camera parks on the zone's
/// transform at the zone's authored orthographic size and does not move — the cave is meant to read
/// as a series of framed rooms rather than a scrolling side-view.
///
/// A zone is deliberately just an anchor plus a size. Because the framing is static it needs no
/// bounding polygon, so <see cref="CameraGhost"/> and its confining shape stay out of the way while
/// a zone is active and keep doing their job for the overworld.
///
/// Overlapping zones need no arbitration rules: a zone's claim is held for as long as the player is
/// inside it, so entering room B while still in room A puts B on top, and walking back into A
/// returns to A because its claim was underneath the whole time. Rooms should therefore be authored
/// to overlap at doorways — leaving a gap reverts to overworld follow for those frames.
/// </summary>
[DisallowMultipleComponent]
public class CameraZone : MonoBehaviour
{
    public enum ActivationMode
    {
        /// <summary>Claimed while the player is inside the trigger.</summary>
        Trigger,
        /// <summary>Claimed only when a sequence asks for it. A wall the player walks past must not fire.</summary>
        Event,
        Both
    }

    [Tooltip("Identifier a sequence can use to look this zone up. Leave blank for trigger-only zones.")]
    [SerializeField] private string zoneId;

    [SerializeField] private ActivationMode activation = ActivationMode.Trigger;

    [Tooltip("Orthographic half-height, in world units. This is the height the room is framed at; " +
             "the width follows from the aspect ratio.")]
    [SerializeField] private float orthographicSize = 10f;

    [Header("Transition")]
    [Tooltip("Seconds to move and resize into this zone. 0 uses the director's default.")]
    [SerializeField] private float transitionDuration;
    [Tooltip("Easing for the move in. Leave empty to use the director's default.")]
    [SerializeField] private AnimationCurve transitionCurve;

    private Collider2D[] triggers;
    private CameraClaim roomClaim;
    private CameraClaim eventClaim;

    public string ZoneId => string.IsNullOrEmpty(zoneId) ? name : zoneId;
    public ActivationMode Activation => activation;
    public float OrthographicSize => orthographicSize;

    public bool AllowsTrigger => activation == ActivationMode.Trigger || activation == ActivationMode.Both;
    public bool AllowsEvent => activation == ActivationMode.Event || activation == ActivationMode.Both;

    private void Awake()
    {
        triggers = GetComponentsInChildren<Collider2D>();
    }

    private void OnEnable()
    {
        CameraZoneManager.Register(this);
    }

    private void OnDisable()
    {
        CameraZoneManager.Unregister(this);
        ReleaseRoomClaim();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!AllowsTrigger) return;
        if (!other.transform.root.CompareTag("Player")) return;

        AcquireRoomClaim(CameraBlendMode.Duration);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!AllowsTrigger) return;
        if (!other.transform.root.CompareTag("Player")) return;

        ReleaseRoomClaim();
    }

    /// <summary>
    /// Applies this zone with no transition. Used on spawn and respawn, where the player should
    /// already be framed on the first frame they see.
    /// </summary>
    public void ApplyImmediate()
    {
        AcquireRoomClaim(CameraBlendMode.Instant);
        CameraDirector.Instance?.SnapNow();
    }

    private void AcquireRoomClaim(CameraBlendMode mode)
    {
        CameraDirector director = CameraDirector.Instance;
        if (director == null) return;

        if (roomClaim == null || roomClaim.Released)
        {
            roomClaim = director.Request(CameraPriority.RoomZone);
        }

        roomClaim.SetTarget(transform);
        roomClaim.SetOrthographicSize(orthographicSize);

        if (mode == CameraBlendMode.Instant)
        {
            roomClaim.WithInstantBlend();
        }
        else
        {
            roomClaim.WithDurationBlend(transitionDuration, HasCurve ? transitionCurve : null);
        }
    }

    private void ReleaseRoomClaim()
    {
        roomClaim?.Release();
        roomClaim = null;
    }

    private bool HasCurve => transitionCurve != null && transitionCurve.length > 0;

    /// <summary>
    /// Claims the camera for this zone at event priority. Dispose the result to hand the camera back.
    /// Prefer <see cref="HoldWhile"/>, which cannot leak the claim.
    /// </summary>
    public CameraClaim Claim(CameraPriority priority = CameraPriority.EventZone)
    {
        CameraDirector director = CameraDirector.Instance;
        if (director == null) return null;

        return director.Request(priority)
            .WithTarget(transform)
            .WithOrthographicSize(orthographicSize)
            .WithDurationBlend(transitionDuration, HasCurve ? transitionCurve : null);
    }

    /// <summary>
    /// Holds the camera on this zone for the duration of <paramref name="body"/>. The claim is
    /// released even if the body throws or is cancelled, so a death mid-sequence cannot strand the
    /// camera away from the player.
    /// </summary>
    public async UniTask HoldWhile(Func<CancellationToken, UniTask> body, CancellationToken cancellationToken)
    {
        CameraClaim claim = Claim();
        try
        {
            await body(cancellationToken);
        }
        finally
        {
            claim?.Release();
        }
    }

    /// <summary>Inspector and AnimationEvent entry point. <see cref="HoldWhile"/> is the safer path.</summary>
    public void Activate()
    {
        if (eventClaim != null) return;
        eventClaim = Claim();
    }

    /// <summary>Inspector and AnimationEvent entry point.</summary>
    public void Deactivate()
    {
        eventClaim?.Release();
        eventClaim = null;
    }

    /// <summary>
    /// Whether a world point falls inside this zone. Spawn resolves its zone this way rather than
    /// through triggers: teleporting the player into a collider they already overlap does not
    /// re-fire OnTriggerEnter2D, so a trigger-driven respawn would silently fail to reframe.
    /// </summary>
    public bool ContainsPoint(Vector2 point)
    {
        Collider2D[] colliders = triggers ?? GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].OverlapPoint(point)) return true;
        }

        return false;
    }

    public bool HasTriggerCollider
    {
        get
        {
            Collider2D[] colliders = triggers ?? GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger) return true;
            }

            return false;
        }
    }

    /// <summary>The region this zone frames, at the given aspect ratio.</summary>
    public Bounds GetFramedBounds(float aspect)
    {
        float halfHeight = orthographicSize;
        float halfWidth = orthographicSize * aspect;
        return new Bounds(transform.position, new Vector3(halfWidth * 2f, halfHeight * 2f, 0f));
    }

#if UNITY_EDITOR
    public void SetOrthographicSizeFromEditor(float size)
    {
        orthographicSize = Mathf.Max(0.01f, size);
    }

    private void OnDrawGizmos()
    {
        DrawFrameGizmo(new Color(0.35f, 0.75f, 1f, 0.35f));
    }

    private void OnDrawGizmosSelected()
    {
        DrawFrameGizmo(new Color(0.35f, 0.85f, 1f, 0.9f));
    }

    private void DrawFrameGizmo(Color color)
    {
        Bounds bounds = GetFramedBounds(CameraZoneManager.EditorTargetAspect(this));
        Gizmos.color = color;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
#endif
}
