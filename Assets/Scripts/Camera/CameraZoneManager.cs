using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The camera framing for one area: a bounds confiner plus the zones authored under it. Several areas
/// can live in a single scene (e.g. two caves stitched together); one is <see cref="Active"/> at a
/// time and everything reads the active area's confiner and zone set. Cross between areas through
/// <see cref="CameraDirector.SwitchArea(CameraZoneManager)"/>. Zones self-register, so a manager is
/// not required for them to work.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class CameraZoneManager : MonoBehaviour
{
    private static readonly List<CameraZoneManager> all = new();

    /// <summary>The area whose confiner and zones are currently in force, or null if none is loaded.</summary>
    public static CameraZoneManager Active { get; private set; }

    /// <summary>Raised when <see cref="Active"/> changes, carrying the new active area (may be null).</summary>
    public static event Action<CameraZoneManager> ActiveChanged;

    /// <summary>Every loaded area, in registration order.</summary>
    public static IReadOnlyList<CameraZoneManager> All => all;

    [Tooltip("Make this the active area as soon as it loads. Set it on the area the player starts in " +
             "and leave it off the others. With none set, the first area to load wins.")]
    [SerializeField] private bool activeByDefault;

    [Tooltip("This area's camera bounds. Defaults to the collider on this object.")]
    [SerializeField] private PolygonCollider2D confiner;

    [Tooltip("Aspect ratio the zone framing gizmos are drawn at. Authored framing fits vertically " +
             "at any aspect, but the horizontal fit follows this number.")]
    [SerializeField] private float targetAspect = 16f / 9f;

    // Zones owned by this area, self-registered in their OnEnable. Switching areas swaps this whole
    // set, so each area frames only its own rooms.
    private readonly List<CameraZone> zones = new();

    /// <summary>This area's camera bounds shape, read by <see cref="CameraDirector"/>.</summary>
    public PolygonCollider2D Confiner => confiner;

    public float TargetAspect => targetAspect;

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
        if (confiner == null) confiner = GetComponent<PolygonCollider2D>();

        // First area to load becomes active; an area flagged activeByDefault claims it outright.
        if (Active == null || activeByDefault) SetActive(this);
    }

    private void OnDisable()
    {
        all.Remove(this);

        // If the active area is unloading, fall back to any other loaded area rather than leave a
        // dangling Active pointing at a dead object.
        if (Active == this)
        {
            Active = all.Count > 0 ? all[0] : null;
            ActiveChanged?.Invoke(Active);
        }
    }

#if UNITY_EDITOR
    private void Reset() => confiner = GetComponent<PolygonCollider2D>();
#endif

    /// <summary>
    /// Makes <paramref name="manager"/> the active area: its confiner and zones take over. The
    /// outgoing area's held claims are released so it cannot keep pulling the camera after the switch.
    /// </summary>
    public static void SetActive(CameraZoneManager manager)
    {
        if (Active == manager) return;

        // A teleport does not fire OnTriggerExit2D, so the outgoing area's room claim would otherwise
        // linger in the director's stack and resurface whenever the new area has a framing gap.
        Active?.ReleaseZoneClaims();
        Active = manager;
        ActiveChanged?.Invoke(Active);
    }

    private void ReleaseZoneClaims()
    {
        for (int i = 0; i < zones.Count; i++) zones[i]?.ReleaseClaims();
    }

    /// <summary>Zones in the active area.</summary>
    public static IReadOnlyList<CameraZone> Zones =>
        Active != null ? Active.zones : (IReadOnlyList<CameraZone>)Array.Empty<CameraZone>();

    public static void Register(CameraZone zone)
    {
        if (zone == null) return;

        // A zone belongs to the area it sits under; an unparented zone falls back to the active area.
        CameraZoneManager owner = zone.GetComponentInParent<CameraZoneManager>(true) ?? Active;
        if (owner == null)
        {
            Debug.LogWarning($"[{nameof(CameraZoneManager)}] Zone '{zone.name}' has no parent area and no active area to join.", zone);
            return;
        }

        if (!owner.zones.Contains(zone)) owner.zones.Add(zone);
    }

    public static void Unregister(CameraZone zone)
    {
        if (zone == null) return;

        for (int i = 0; i < all.Count; i++) all[i].zones.Remove(zone);
    }

    /// <summary>Finds a zone by its id in the active area. Used by sequences that activate a zone from an event.</summary>
    public static CameraZone Find(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId) || Active == null) return null;

        List<CameraZone> list = Active.zones;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].ZoneId == zoneId) return list[i];
        }

        return null;
    }

    /// <summary>
    /// The active-area zone containing a world point, if any. Spawn and respawn resolve their framing
    /// this way because a teleport into an already-overlapping trigger does not re-fire OnTriggerEnter2D.
    /// </summary>
    public static CameraZone ResolveAt(Vector2 point)
    {
        if (Active == null) return null;

        List<CameraZone> list = Active.zones;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            CameraZone zone = list[i];
            if (zone != null && zone.AllowsTrigger && zone.ContainsPoint(point)) return zone;
        }

        return null;
    }

    /// <summary>Frames whichever active-area zone contains the point, with no transition. Call after a respawn.</summary>
    public static void ApplyAt(Vector2 point) => ResolveAt(point)?.ApplyImmediate();

#if UNITY_EDITOR
    /// <summary>
    /// Aspect ratio for drawing framing gizmos in edit mode, where no manager instance exists yet.
    /// Prefers a manager above the zone in the hierarchy, then any manager in the scene.
    /// </summary>
    public static float EditorTargetAspect(CameraZone zone)
    {
        const float Fallback = 16f / 9f;

        CameraZoneManager manager = zone != null ? zone.GetComponentInParent<CameraZoneManager>() : null;
        if (manager == null) manager = FindAnyObjectByType<CameraZoneManager>();

        return manager != null && manager.targetAspect > 0f ? manager.targetAspect : Fallback;
    }

    /// <summary>Zones under this manager, in hierarchy order. Edit mode safe.</summary>
    public CameraZone[] CollectZones() => GetComponentsInChildren<CameraZone>(true);

    private CameraClaim debugClaim;

    /// <summary>Play mode preview: claims the camera for a zone through the real arbiter, so the
    /// actual transition and easing are what you see rather than a static frame.</summary>
    public void DebugActivate(CameraZone zone)
    {
        if (!Application.isPlaying || zone == null) return;

        DebugRelease();
        debugClaim = zone.Claim(CameraPriority.EventZone);
    }

    public void DebugRelease()
    {
        debugClaim?.Release();
        debugClaim = null;
    }
#endif
}
