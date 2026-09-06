using System.Collections.Generic;
using Unity.VisualScripting;
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
[DefaultExecutionOrder(1000)]
public class CaveBoundary : MonoBehaviour
{
    /// <summary>Runaway guard. A boundary this dense is a mis-set overlap, not a real request.</summary>
    public const int MaxRocks = 400;

    [Header("Library")]
    [SerializeField] private CaveRockLibrary library;
    [SerializeField] private int rockSetIndex;
    [SerializeField] private int depthTierIndex;

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
    [Tooltip("The order in layer every rock on this boundary spawns with. All rocks share this one " +
             "value; raise it to draw the whole boundary in front of another, lower it to push it back.")]
    [SerializeField] private int sortingOrder = 3;

    [Header("Randomisation")]
    [Tooltip("Same seed and same shape always produce the same rocks. Every new boundary starts on " +
             "its own random seed, so two boundaries never lay the same rocks; re-roll in the " +
             "inspector for a different arrangement.")]
    [SerializeField] private int seed;

    [Header("Parallax")]
    [Tooltip("The set of depth bands this boundary can read at. Shared asset — drop the same one on " +
             "every boundary and pick a band per boundary. Leave empty and the rocks hold still.")]
    [SerializeField] private CaveParallaxProfile parallaxProfile;

    [Tooltip("Which band in the profile this boundary uses — its Z spread and near/far factors.")]
    [SerializeField] private int parallaxTierIndex;

    [Tooltip("How much of the parallax applies vertically. 1 drifts up and down as much as sideways; " +
             "0 keeps every rock level, which reads best on a cave that scrolls mostly horizontally.")]
    [Range(0f, 1f)] [SerializeField] private float verticalInfluence = 1f;

    [Tooltip("Editor only: rebuild the rocks automatically when this boundary's settings or shape " +
             "change and when you enter play mode, so you never have to press Regenerate. Hand-moved " +
             "rocks are still kept.")]
    [SerializeField] private bool autoRegenerate = true;

    [SerializeField, HideInInspector] private List<CaveGeneratedRock> generated = new List<CaveGeneratedRock>();

    public CaveRockLibrary Library => library;
    public int RockSetIndex => rockSetIndex;
    public int DepthTierIndex => depthTierIndex;

    /// <summary>The transform generated rocks are parented under: always this boundary.</summary>
    public Transform RockParent => transform;

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

    /// <summary>The parallax profile this boundary reads its depth band from.</summary>
    public CaveParallaxProfile ParallaxProfile => parallaxProfile;

    /// <summary>The selected depth band — its Z spread and near/far factors — or null if unset.</summary>
    public CaveParallaxTier ParallaxTier => parallaxProfile != null ? parallaxProfile.GetTier(parallaxTierIndex) : null;

    /// <summary>Editor-only flag read by the auto-regenerate hooks. See CaveBoundaryAutoRegenerate.</summary>
    public bool AutoRegenerate => autoRegenerate;

    /// <summary>
    /// A fresh arrangement seed. Never 0, so a serialised 0 still reads as "this boundary has not
    /// been seeded yet" and the tool can fill it in.
    /// </summary>
    public static int NewSeed()
    {
        return UnityEngine.Random.Range(1, int.MaxValue);
    }

    public bool HasSeed => seed != 0;

    /// <summary>
    /// Unity message, editor only: a boundary added to a scene seeds itself, so dropping two of them
    /// on the same shape does not produce the same rocks twice.
    /// </summary>
    private void Reset()
    {
        seed = NewSeed();
    }

    // -------------------------------------------------------------------------
    // Runtime parallax
    // -------------------------------------------------------------------------

    /// <summary>One placed rock, with the spot it belongs and the factor its depth maps to.</summary>
    private struct RockParallax
    {
        public Transform transform;
        public Vector2 anchor;
        public float factor;
    }

    private RockParallax[] runtimeRocks;
    private Camera parallaxCamera;

    private void Start()
    {
        BuildRuntimeRocks();
    }

    /// <summary>
    /// Caches every placed rock with the spot it belongs and the parallax factor its depth earns.
    /// The factor is spread across the boundary's own Z span using the selected
    /// <see cref="CaveParallaxTier"/>: the nearest rock (minimum Z) takes the tier's min factor and
    /// slides fastest, the furthest (maximum Z) takes its max factor and barely moves, with everything
    /// between interpolated. A missing profile/tier leaves every factor at 0, so the rocks hold still
    /// rather than throwing. Built once from the generated list, which persists into play.
    /// </summary>
    private void BuildRuntimeRocks()
    {
        var transforms = new List<Transform>(generated.Count);
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        foreach (CaveGeneratedRock record in generated)
        {
            if (record == null || record.instance == null)
                continue;

            Transform t = record.instance.transform;
            float z = t.position.z;
            transforms.Add(t);

            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }

        CaveParallaxTier parallaxTier = ParallaxTier;
        float minFactor = parallaxTier != null ? parallaxTier.minParallaxFactor : 0f;
        float maxFactor = parallaxTier != null ? parallaxTier.maxParallaxFactor : 0f;

        // A boundary with no Z spread (jitter off) has no near/far to interpolate, so put every rock
        // halfway between the two factors rather than dividing by zero.
        bool flat = maxZ - minZ < 1e-4f;

        var built = new RockParallax[transforms.Count];
        for (int i = 0; i < transforms.Count; i++)
        {
            Vector3 world = transforms[i].position;
            float t = flat ? 0.5f : Mathf.InverseLerp(minZ, maxZ, world.z);

            built[i] = new RockParallax
            {
                transform = transforms[i],
                anchor = new Vector2(world.x, world.y),
                factor = Mathf.Lerp(minFactor, maxFactor, t)
            };
        }

        runtimeRocks = built;
    }

    /// <summary>
    /// Places every rock relative to its anchor from the camera, in LateUpdate so Cinemachine has
    /// already moved the camera. Anchored and absolute: a rock sits on its authored spot when the
    /// camera is on it and slides off by its factor as the camera moves away, so nothing accumulates
    /// or drifts. Negative factors overshoot and read as in front; positive track the camera and
    /// read as far back.
    /// </summary>
    private void LateUpdate()
    {
        Camera view = ParallaxCamera();
        if (view == null || runtimeRocks == null)
            return;

        Vector3 camera = view.transform.position;
        for (int i = 0; i < runtimeRocks.Length; i++)
        {
            Transform t = runtimeRocks[i].transform;
            if (t == null)
                continue;

            float factor = runtimeRocks[i].factor;
            Vector2 anchor = runtimeRocks[i].anchor;

            float x = anchor.x + (camera.x - anchor.x) * factor;
            float y = anchor.y + (camera.y - anchor.y) * factor * verticalInfluence;
            t.position = new Vector3(x, y, t.position.z);
        }
    }

    /// <summary>
    /// The foreground camera, resolved late. The rig can arrive after the level it parallaxes — an
    /// additive load, or a scene opened on its own — so this keeps asking rather than giving up.
    /// </summary>
    private Camera ParallaxCamera()
    {
        if (parallaxCamera != null)
            return parallaxCamera;

        CameraRig rig = CameraRig.Current;
        parallaxCamera = rig != null ? rig.Foreground : null;
        return parallaxCamera;
    }

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
        CaveParallaxTier parallaxTier = ParallaxTier;
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

            // Depth drives the parallax: each rock gets a Z jittered from the selected parallax tier,
            // and the runtime spreads factor across that Z span. Rolled from the seeded stream so a
            // rebuild of an unchanged boundary reproduces the same depths.
            float jitter = parallaxTier != null ? parallaxTier.ZJitter : 0f;
            float z = Range(rng, jitter);

            placements.Add(new CaveRockPlacement
            {
                entry = entry,
                worldPosition = new Vector3(position.x, position.y, z),
                worldRotation = Quaternion.Euler(0f, 0f, angle),
                localScale = new Vector3(scaleX, rockScale, rockScale),
                sortingOrder = sortingOrder
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
