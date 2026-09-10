using System.Collections.Generic;
using Player;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// Spawns scent trails when the player zooms out. On zoom start it does a single
/// physics overlap check around the player to find acorns in range, and instantiates
/// one <see cref="ScentTrail"/> prefab per acorn. On zoom end every trail is destroyed.
/// </summary>
public class ScentTrailManager : MonoBehaviour
{
    [SerializeField] private ScentTrail trailPrefab;

    [Tooltip("The player's Graphics transform the trails start from. Falls back to the root transform if unset.")]
    [SerializeField] private Transform graphicsOrigin;

    [Tooltip("How much larger the detection radius is than the zoomed-out camera view. " +
             "1 = exactly the view extent, 1.2 = 20% bigger than the view.")]
    [SerializeField] private float rangeMultiplier = 1.2f;

    [Tooltip("Fallback detection radius used if the zoomed-out camera size can't be determined.")]
    [SerializeField] private float fallbackDetectionRange = 15f;

    [Tooltip("Layer mask for acorns (the Acorn layer).")]
    [SerializeField] private LayerMask acornMask;

    [Tooltip("Optional parent for spawned trails. One is created automatically if left empty.")]
    [SerializeField] private Transform trailContainer;

    private PlayerCameraManager playerCamera;

    // Trails spawned for the current zoom. Recreated each zoom.
    // NOTE (future optimization): these trails are instantiated on zoom start and destroyed
    // on zoom end. If this causes GC pressure, switch to pooling/reusing ScentTrail instances
    // instead of Instantiate/Destroy.
    private readonly List<ScentTrail> activeTrails = new List<ScentTrail>();

    private void Start()
    {
        playerCamera = GetComponentInParent<PlayerCameraManager>();

        if (graphicsOrigin == null)
        {
            graphicsOrigin = transform.root;
        }

        if (trailContainer == null)
        {
            trailContainer = new GameObject("Scent Trails").transform;
            trailContainer.SetParent(transform, false);
        }

        if (playerCamera != null)
        {
            playerCamera.OnZoomStarted += SpawnTrails;
            playerCamera.OnZoomEnded += ClearTrails;
        }
        else
        {
            Debug.LogError("ScentTrailManager: no PlayerCameraManager found in parents.");
        }
    }

    private void SpawnTrails()
    {
        ClearTrails();

        if (trailPrefab == null || graphicsOrigin == null)
        {
            return;
        }

        float detectionRange = GetDetectionRange();
        Collider2D[] hits = Physics2D.OverlapCircleAll(graphicsOrigin.position, detectionRange, acornMask);

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<Acorn>(out var acorn)) continue;
            if (!acorn.gameObject.activeInHierarchy) continue;

            ScentTrail trail = Instantiate(trailPrefab, trailContainer);
            trail.transform.position = acorn.transform.position;
            
            trail.SetEndpoints(graphicsOrigin, acorn);
            activeTrails.Add(trail);
        }
    }

    /// <summary>
    /// Detection radius derived from the zoomed-out camera view so it is slightly larger
    /// than what the player can see when zoomed out. Uses the zoomed-out orthographic size
    /// (the view half-height) and the camera aspect to get the view half-diagonal, then
    /// scales it by <see cref="rangeMultiplier"/>.
    /// </summary>
    private float GetDetectionRange()
    {
        if (playerCamera == null)
        {
            return fallbackDetectionRange;
        }

        // Orthographic size = half-height of the view in world units.
        float halfHeight = playerCamera.GetZoomOutAmount();
        if (halfHeight <= 0f)
        {
            return fallbackDetectionRange;
        }

        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float halfWidth = halfHeight * aspect;

        // Half-diagonal so the circle covers the corners of the rectangular view.
        float viewRadius = Mathf.Sqrt(halfHeight * halfHeight + halfWidth * halfWidth);
        return viewRadius * rangeMultiplier;
    }

    private void ClearTrails()
    {
        foreach (var trail in activeTrails)
        {
            if (trail != null)
            {
                Destroy(trail.gameObject);
            }
        }

        activeTrails.Clear();
    }

    private void OnDestroy()
    {
        if (playerCamera != null)
        {
            playerCamera.OnZoomStarted -= SpawnTrails;
            playerCamera.OnZoomEnded -= ClearTrails;
        }

        ClearTrails();
    }

    [Button("Spawn Trails (Debug)")]
    private void DebugSpawnTrails()
    {
        // Reuses the normal spawn path, which clears any existing trails first so
        // repeated button presses (or a press mid-zoom) don't double up.
        SpawnTrails();
    }

    [Button("Clear Trails (Debug)")]
    private void DebugClearTrails()
    {
        ClearTrails();
    }
}
