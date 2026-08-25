using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One rock the generator wants placed. Produced by <see cref="CaveBoundary.BuildPlacements"/>
/// and consumed by the editor tool, which is the only thing allowed to instantiate.
/// </summary>
public struct CaveRockPlacement
{
    public CaveRockEntry entry;
    public Vector3 worldPosition;
    public Quaternion worldRotation;
    public Vector3 localScale;
    public int sortingOrder;
}

/// <summary>
/// A rock the tool has already placed, plus the pose it was placed at. The stored pose is what
/// makes regeneration non-destructive: if a rock's current pose still matches, the author never
/// touched it and it is safe to rebuild; if it has drifted, they moved it by hand and it is left
/// alone.
/// </summary>
[System.Serializable]
public class CaveGeneratedRock
{
    public GameObject instance;
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 localScale;
    public int sortingOrder;
}

/// <summary>
/// A drawn cave surface. The polyline is the rock line; the tool walks it and spawns overlapping
/// rocks facing away from it, so a ceiling, floor or wall is one drawn shape rather than thirty
/// hand-placed sprites.
///
/// This component only holds the shape and works out where rocks go. Creating and destroying them
/// is the editor tool's job (see CaveBoundaryEditor), so nothing here runs in a build.
/// </summary>
[DisallowMultipleComponent]
public class CaveBoundary : MonoBehaviour
{
    /// <summary>Runaway guard. A boundary this dense is a mis-set overlap, not a real request.</summary>
    public const int MaxRocks = 400;

    [Header("Library")]
    [SerializeField] private CaveRockLibrary library;
    [SerializeField] private int rockSetIndex;
    [SerializeField] private int depthTierIndex;

    [Header("Output")]
    [Tooltip("Where generated rocks are parented. Leave empty to parent them under this boundary, " +
             "which makes the boundary its own parallax layer root. Point it at an existing group " +
             "(Frontground Objects/Top and friends) to fold into a layer that already exists.")]
    [SerializeField] private Transform rockParent;

    [Header("Shape")]
    [Tooltip("Boundary points in this object's local space. Edit them with the handles in the Scene view.")]
    [SerializeField] private List<Vector2> points = new List<Vector2>();

    [Tooltip("Rocks face left of the drawn direction. Tick this when they end up facing into the " +
             "rock instead of into open air. The Scene view arrows show which way is out.")]
    [SerializeField] private bool flipNormal;

    [Header("Placement")]
    [Tooltip("How much of the previous rock each new rock covers. 0.6 means each rock hides 60% of " +
             "the one before it, so the line reads as solid stone rather than beads on a string.")]
    [Range(0f, 0.95f)] [SerializeField] private float overlap = 0.6f;

    [Tooltip("Random +/- added to the overlap per rock, so the spacing does not read as a pattern.")]
    [Range(0f, 0.4f)] [SerializeField] private float overlapJitter = 0.08f;

    [Tooltip("Slides every rock along its outward normal. Positive lifts them out of the surface, " +
             "negative sinks them in. Drag the cone handle in the Scene view to set it by eye.")]
    [SerializeField] private float normalOffset;

    [Tooltip("Random +/- around the normal offset, per rock. 0 sits every rock exactly on the line, " +
             "which reads as too even; a small value roughens the silhouette.")]
    [Min(0f)] [SerializeField] private float normalOffsetJitter;

    [Header("Orientation")]
    [Tooltip("Rotate each rock so its top faces out of the surface. Off leaves every rock upright, " +
             "which only looks right on a flat floor.")]
    [SerializeField] private bool alignToNormal = true;

    [Tooltip("Random +/- degrees on top of the surface rotation.")]
    [Range(0f, 45f)] [SerializeField] private float rotationJitter = 6f;

    [Tooltip("Randomly mirrors rocks left-to-right. On a ceiling this is what makes some rocks read " +
             "as flipped vertically and others as rotated a full turn, matching how the Top group " +
             "was hand-authored.")]
    [SerializeField] private bool mirrorVariety = true;

    [Header("Sorting")]
    [Tooltip("Added to the tier's sorting order for each rock along the chain, so later rocks draw " +
             "in front of earlier ones. 0 gives every rock the tier's order.")]
    [SerializeField] private int sortingStep;

    [Header("Randomisation")]
    [Tooltip("Same seed and same shape always produce the same rocks. Re-roll in the inspector for " +
             "a different arrangement.")]
    [SerializeField] private int seed = 1;

    [SerializeField, HideInInspector] private List<CaveGeneratedRock> generated = new List<CaveGeneratedRock>();

    public CaveRockLibrary Library => library;
    public int RockSetIndex => rockSetIndex;
    public int DepthTierIndex => depthTierIndex;

    /// <summary>The transform generated rocks are parented under; this boundary when unset.</summary>
    public Transform RockParent => rockParent != null ? rockParent : transform;

    /// <summary>Live list, mutated directly by the scene view handles.</summary>
    public List<Vector2> Points => points;

    /// <summary>How far along the outward normal every rock sits. Same for the whole boundary.</summary>
    public float NormalOffset
    {
        get => normalOffset;
        set => normalOffset = value;
    }

    public float NormalOffsetJitter => Mathf.Max(0f, normalOffsetJitter);

    public List<CaveGeneratedRock> Generated => generated;

    public int Seed
    {
        get => seed;
        set => seed = value;
    }

    public CaveDepthTier Tier => library != null ? library.GetDepthTier(depthTierIndex) : null;

    // -------------------------------------------------------------------------
    // Shape queries
    // -------------------------------------------------------------------------

    public Vector3 GetWorldPoint(int index)
    {
        return transform.TransformPoint(points[index]);
    }

    public void SetWorldPoint(int index, Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        points[index] = new Vector2(local.x, local.y);
    }

    public float TotalLength()
    {
        float length = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            length += Vector3.Distance(GetWorldPoint(i - 1), GetWorldPoint(i));
        }
        return length;
    }

    /// <summary>
    /// Position and outward normal at <paramref name="distance"/> along the polyline. The normal is
    /// the left of the drawn direction, negated when Flip Normal is set.
    /// </summary>
    public void Evaluate(float distance, out Vector3 position, out Vector3 normal)
    {
        position = points.Count > 0 ? GetWorldPoint(0) : transform.position;
        normal = Vector3.up;

        if (points.Count < 2)
            return;

        float remaining = Mathf.Max(0f, distance);
        for (int i = 1; i < points.Count; i++)
        {
            Vector3 a = GetWorldPoint(i - 1);
            Vector3 b = GetWorldPoint(i);
            Vector3 segment = b - a;
            float segmentLength = segment.magnitude;

            if (segmentLength <= 1e-5f)
                continue;

            Vector3 direction = segment / segmentLength;

            // Last segment absorbs any leftover so a rounding overshoot still lands on the line.
            if (remaining <= segmentLength || i == points.Count - 1)
            {
                position = a + direction * Mathf.Min(remaining, segmentLength);
                normal = OutwardNormal(direction);
                return;
            }

            remaining -= segmentLength;
        }
    }

    private Vector3 OutwardNormal(Vector3 direction)
    {
        Vector3 left = new Vector3(-direction.y, direction.x, 0f);
        return flipNormal ? -left : left;
    }

    // -------------------------------------------------------------------------
    // Placement
    // -------------------------------------------------------------------------

    /// <summary>
    /// Walks the boundary and returns where every rock should go. Pure: calling it twice with the
    /// same shape and seed gives the same answer, which is what lets regeneration be predictable.
    /// </summary>
    public List<CaveRockPlacement> BuildPlacements()
    {
        var placements = new List<CaveRockPlacement>();

        CaveDepthTier tier = Tier;
        if (library == null || tier == null || points.Count < 2)
            return placements;

        float totalLength = TotalLength();
        if (totalLength <= 1e-4f)
            return placements;

        var rng = new System.Random(seed);
        float cursor = 0f;
        int index = 0;

        while (cursor <= totalLength && index < MaxRocks)
        {
            CaveRockEntry entry = library.PickRock(rockSetIndex, rng);
            if (entry == null)
                break;

            float rockScale = library.RollScale(rng);

            Evaluate(cursor, out Vector3 position, out Vector3 normal);

            float offset = normalOffset + Range(rng, NormalOffsetJitter);
            position += normal * offset;

            float angle = alignToNormal ? Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f : 0f;
            angle += Range(rng, rotationJitter);

            // Mirroring the sprite left-to-right is what produces both of the ceiling looks in the
            // existing art: rotated to the normal it reads as a full turn, mirrored it reads as a
            // vertical flip. One roll covers both, and gives floors their variety too.
            float scaleX = mirrorVariety && rng.Next(2) == 0 ? -rockScale : rockScale;

            placements.Add(new CaveRockPlacement
            {
                entry = entry,
                worldPosition = new Vector3(position.x, position.y, 0f),
                worldRotation = Quaternion.Euler(0f, 0f, angle),
                localScale = new Vector3(scaleX, rockScale, rockScale),
                sortingOrder = tier.sortingOrder + sortingStep * index
            });

            float width = SpriteWidth(entry.prefab) * rockScale;
            float step = width * (1f - Mathf.Clamp(overlap + Range(rng, overlapJitter), 0f, 0.97f));

            // Never advance by nothing, or the walk would never reach the end of the line.
            cursor += Mathf.Max(step, 0.05f);
            index++;
        }

        return placements;
    }

    /// <summary>Symmetric random in [-amount, amount].</summary>
    private static float Range(System.Random rng, float amount)
    {
        if (amount <= 0f)
            return 0f;
        return (float)(rng.NextDouble() * 2.0 - 1.0) * amount;
    }

    /// <summary>Unscaled sprite width in world units, used to turn an overlap fraction into a step.</summary>
    public static float SpriteWidth(GameObject prefab)
    {
        if (prefab == null)
            return 1f;

        SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null)
            return 1f;

        float width = renderer.sprite.bounds.size.x;
        return width > 1e-4f ? width : 1f;
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Faint line so unselected boundaries are still findable. The selected boundary gets the
        // full handle overlay from CaveBoundaryEditor instead.
        if (points.Count < 2 || UnityEditor.Selection.activeGameObject == gameObject)
            return;

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
        for (int i = 1; i < points.Count; i++)
        {
            Gizmos.DrawLine(GetWorldPoint(i - 1), GetWorldPoint(i));
        }
    }
#endif
}
