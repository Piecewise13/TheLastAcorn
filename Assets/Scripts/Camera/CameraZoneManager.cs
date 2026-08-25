using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry for the camera zones in the loaded scene, and the home of the zone authoring tools.
///
/// Zones self-register, so a manager is not required for them to work — it exists so sequences can
/// resolve a zone by id, so spawns can resolve one by position, and so the editor has a single place
/// to author the whole set from. One list serves all three rather than an authoring list and a
/// runtime list that can drift apart.
/// </summary>
public class CameraZoneManager : MonoBehaviour
{
    public static CameraZoneManager Instance { get; private set; }

    private static readonly List<CameraZone> zones = new();

    [Tooltip("Aspect ratio the zone framing gizmos are drawn at. Authored framing fits vertically " +
             "at any aspect, but the horizontal fit follows this number.")]
    [SerializeField] private float targetAspect = 16f / 9f;

    public static IReadOnlyList<CameraZone> Zones => zones;

    public float TargetAspect => targetAspect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Register(CameraZone zone)
    {
        if (zone != null && !zones.Contains(zone)) zones.Add(zone);
    }

    public static void Unregister(CameraZone zone)
    {
        zones.Remove(zone);
    }

    /// <summary>Finds a zone by its id. Used by sequences that activate a zone from an event.</summary>
    public static CameraZone Find(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return null;

        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] != null && zones[i].ZoneId == zoneId) return zones[i];
        }

        return null;
    }

    /// <summary>
    /// The zone containing a world point, if any. Spawn and respawn resolve their framing this way
    /// because a teleport into an already-overlapping trigger does not re-fire OnTriggerEnter2D.
    /// </summary>
    public static CameraZone ResolveAt(Vector2 point)
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            CameraZone zone = zones[i];
            if (zone != null && zone.AllowsTrigger && zone.ContainsPoint(point)) return zone;
        }

        return null;
    }

    /// <summary>Frames whichever zone contains the point, with no transition. Call after a respawn.</summary>
    public static void ApplyAt(Vector2 point)
    {
        ResolveAt(point)?.ApplyImmediate();
    }

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
