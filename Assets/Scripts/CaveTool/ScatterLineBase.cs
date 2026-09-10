using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One prop the generator wants placed. Produced by <see cref="ScatterLineBase.BuildPlacements"/>
/// and consumed by the editor tool, which is the only thing allowed to instantiate.
/// </summary>
public struct PropPlacement
{
    public PropEntry entry;
    public Vector3 worldPosition;
    public Quaternion worldRotation;
    public Vector3 localScale;
    public int sortingOrder;

    /// <summary>Material forced onto the prop, or null to keep the prefab's own.</summary>
    public Material materialOverride;

    /// <summary>
    /// Parallax factor baked at generation time (the layered variant sets one per layer). NaN means
    /// "derive from Z at runtime", which is how the graded variant works.
    /// </summary>
    public float parallaxFactor;
}

/// <summary>
/// A prop the tool has already placed, plus the pose it was placed at. The stored pose is what makes
/// regeneration non-destructive: if a prop's current pose still matches, the author never touched it
/// and it is safe to rebuild; if it has drifted, they moved it by hand and it is left alone.
/// </summary>
[System.Serializable]
public class GeneratedProp
{
    public GameObject instance;
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 localScale;
    public int sortingOrder;

    /// <summary>Baked parallax factor for the layered variant; NaN when the factor is Z-derived.</summary>
    public float parallaxFactor;
}

/// <summary>
/// A drawn line that scatters props along it. The polyline is the path; the tool walks it and spawns
/// overlapping props facing away from it, so a cliff, cave wall, tree-top ridge or bush run is one
/// drawn shape rather than dozens of hand-placed sprites.
///
/// This base owns everything both variants share: the shape and its normals, the placement walk, the
/// non-destructive generated list, and the runtime parallax that anchors each prop and slides it past
/// the camera by its factor. What differs — how props draw their set, material and depth — is left to
/// the concrete variants: <see cref="ScatterLine"/> (one graded pool) and LayeredScatterLine
/// (discrete layers). Creating and destroying props is the editor tool's job (see the generator), so
/// nothing here instantiates in a build.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public abstract class ScatterLineBase : MonoBehaviour
{
    /// <summary>Runaway guard. A line this dense is a mis-set overlap, not a real request.</summary>
    public const int MaxProps = 400;

    /// <summary>How props are laid out: strung along the path, or scattered inside it as a polygon.</summary>
    public enum PlacementMode { Line, Fill }

    [Header("Library")]
    [SerializeField] protected PropLibrary library;

    [Header("Shape")]
    [Tooltip("Line points in this object's local space. Edit them with the handles in the Scene view.")]
    [SerializeField] protected List<Vector2> points = new List<Vector2>();

    [Tooltip("Props face left of the drawn direction. Tick this when they end up facing into the " +
             "surface instead of into open air. The Scene view arrows show which way is out.")]
    [SerializeField] protected bool flipNormal;

    [Header("Placement")]
    [Tooltip("How much of the previous prop each new prop covers. 0.6 means each prop hides 60% of " +
             "the one before it, so the line reads as solid rather than beads on a string.")]
    [Range(0f, 0.95f)] [SerializeField] protected float overlap = 0.6f;

    [Tooltip("Random +/- added to the overlap per prop, so the spacing does not read as a pattern.")]
    [Range(0f, 0.4f)] [SerializeField] protected float overlapJitter = 0.08f;

    [Tooltip("Slides every prop along its outward normal. Positive lifts them out of the surface, " +
             "negative sinks them in. Drag the cone handle in the Scene view to set it by eye.")]
    [SerializeField] protected float normalOffset;

    [Tooltip("Random +/- around the normal offset, per prop. 0 sits every prop exactly on the line, " +
             "which reads as too even; a small value roughens the silhouette.")]
    [Min(0f)] [SerializeField] protected float normalOffsetJitter;

    [Tooltip("Line walks props along the drawn path (cliffs, cave walls, tree-top ridges). Fill " +
             "treats the path as a closed polygon and scatters props inside it (bushes, nature " +
             "behind the player).")]
    [SerializeField] protected PlacementMode placementMode = PlacementMode.Line;

    [Tooltip("Fill mode only: grid spacing in world units. Smaller packs more props in.")]
    [Min(0.05f)] [SerializeField] protected float fillSpacing = 2f;

    [Tooltip("Fill mode only: how far each prop wanders from its grid cell, 0..1 of the spacing. " +
             "0 is a rigid grid; higher reads as natural scatter.")]
    [Range(0f, 1f)] [SerializeField] protected float fillJitter = 0.7f;

    [Header("Orientation")]
    [Tooltip("Rotate each prop so its top faces out of the surface. Off leaves every prop upright, " +
             "which only looks right on a flat floor.")]
    [SerializeField] protected bool alignToNormal = true;

    [Tooltip("Random +/- degrees on top of the surface rotation.")]
    [Range(0f, 45f)] [SerializeField] protected float rotationJitter = 6f;

    [Tooltip("Randomly mirrors props left-to-right, so some read as flipped and others as rotated.")]
    [SerializeField] protected bool mirrorVariety = true;

    [Header("Sorting")]
    [Tooltip("The order in layer every prop on this line spawns with. All props share this one " +
             "value; raise it to draw the whole line in front of another, lower it to push it back.")]
    [SerializeField] protected int sortingOrder = 3;

    [Tooltip("Ties render order to the normal offset. Props that sit further out along the normal " +
             "(more offset, away from the path) draw further back; props nearer the path draw in " +
             "front. 0 keeps every prop on the shared sorting order above.")]
    [Min(0f)] [SerializeField] protected float offsetDepthSorting;

    [Header("Randomisation")]
    [Tooltip("Same seed and same shape always produce the same props. Every new line starts on its " +
             "own random seed; re-roll in the inspector for a different arrangement.")]
    [SerializeField] protected int seed;

    [Header("Parallax")]
    [Tooltip("How much of the parallax applies vertically. 1 drifts up and down as much as sideways; " +
             "0 keeps every prop level, which reads best on a scene that scrolls mostly horizontally.")]
    [Range(0f, 1f)] [SerializeField] protected float verticalInfluence = 1f;

    [Tooltip("Editor only: rebuild the props automatically when this line's settings or shape change " +
             "and when you enter play mode, so you never have to press Regenerate. Hand-moved props " +
             "are still kept.")]
    [SerializeField] protected bool autoRegenerate = true;

    [SerializeField, HideInInspector] protected List<GeneratedProp> generated = new List<GeneratedProp>();

    public PropLibrary Library => library;

    /// <summary>Whether props string along the path (Line) or scatter inside it (Fill).</summary>
    public PlacementMode Mode => placementMode;

    /// <summary>The transform generated props are parented under: always this line.</summary>
    public Transform PropParent => transform;

    /// <summary>Live list, mutated directly by the scene view handles.</summary>
    public List<Vector2> Points => points;

    /// <summary>How far along the outward normal every prop sits. Same for the whole line.</summary>
    public float NormalOffset
    {
        get => normalOffset;
        set => normalOffset = value;
    }

    public float NormalOffsetJitter => Mathf.Max(0f, normalOffsetJitter);

    public List<GeneratedProp> Generated => generated;

    public int Seed
    {
        get => seed;
        set => seed = value;
    }

    /// <summary>Editor-only flag read by the auto-regenerate hooks. See ScatterLineAutoRegenerate.</summary>
    public bool AutoRegenerate => autoRegenerate;

    /// <summary>
    /// A fresh arrangement seed. Never 0, so a serialised 0 still reads as "this line has not been
    /// seeded yet" and the tool can fill it in.
    /// </summary>
    public static int NewSeed()
    {
        return UnityEngine.Random.Range(1, int.MaxValue);
    }

    public bool HasSeed => seed != 0;

    /// <summary>
    /// Unity message, editor only: a line added to a scene seeds itself, so dropping two of them on
    /// the same shape does not produce the same props twice.
    /// </summary>
    protected virtual void Reset()
    {
        seed = NewSeed();
    }

    // -------------------------------------------------------------------------
    // Placement API — the variants fill these in
    // -------------------------------------------------------------------------

    /// <summary>
    /// Walks the line and returns where every prop should go. Pure: calling it twice with the same
    /// shape and seed gives the same answer, which is what lets regeneration be predictable.
    /// </summary>
    public abstract List<PropPlacement> BuildPlacements();

    /// <summary>
    /// Whether this line has enough set up to place anything. On false, <paramref name="message"/>
    /// says what is missing, which the generator surfaces as a warning.
    /// </summary>
    public abstract bool ValidateForGenerate(out string message);

    /// <summary>
    /// Appends props for one set, in whichever placement mode the line is set to. Both variants call
    /// this and stay mode-agnostic — Line walks the path, Fill scatters inside the closed shape.
    /// </summary>
    protected void AppendScatter(List<PropPlacement> placements, int setIndex, Material material,
                                 float zJitter, float bakedFactor, int baseSorting, System.Random rng)
    {
        if (placementMode == PlacementMode.Fill)
            AppendFill(placements, setIndex, material, zJitter, bakedFactor, baseSorting, rng);
        else
            AppendWalk(placements, setIndex, material, zJitter, bakedFactor, baseSorting, rng);
    }

    /// <summary>
    /// Shared placement walk: steps along the line and appends one prop per step, drawn from
    /// <paramref name="setIndex"/> of the library, wearing <paramref name="material"/>, jittered in Z
    /// by <paramref name="zJitter"/>. <paramref name="bakedFactor"/> is stored on each placement
    /// (NaN to leave the factor Z-derived). Both variants build on this — the graded one calls it
    /// once, the layered one once per layer.
    /// </summary>
    protected void AppendWalk(List<PropPlacement> placements, int setIndex, Material material,
                              float zJitter, float bakedFactor, int baseSorting, System.Random rng)
    {
        if (library == null || points.Count < 2)
            return;

        float totalLength = TotalLength();
        if (totalLength <= 1e-4f)
            return;

        float cursor = 0f;
        while (cursor <= totalLength && placements.Count < MaxProps)
        {
            PropEntry entry = library.PickProp(setIndex, rng);
            if (entry == null)
                break;

            float propScale = library.RollScale(rng);

            Evaluate(cursor, out Vector3 position, out Vector3 normal);

            float offset = normalOffset + Range(rng, NormalOffsetJitter);
            position += normal * offset;

            // Depth from offset: a prop sitting further out along the normal than its neighbours reads
            // as poking further back, so subtract from the shared order. Ranked by deviation from the
            // base offset so the base still shifts the whole line together rather than reordering it.
            int sorting = baseSorting - Mathf.RoundToInt((offset - normalOffset) * offsetDepthSorting);

            // normal already carries the flipNormal sign (see OutwardNormal), so both branches pick up
            // the flip for free: aligned props point up the normal, perpendicular props point along
            // the tangent, and flipping the normal spins either 180 to the other side.
            float normalAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
            float angle = alignToNormal ? normalAngle - 90f : normalAngle + 180f;
            angle += Range(rng, rotationJitter);

            // Mirroring left-to-right gives both looks from one sprite: rotated to the normal it reads
            // as a full turn, mirrored it reads as a vertical flip. One roll covers both.
            float scaleX = mirrorVariety && rng.Next(2) == 0 ? -propScale : propScale;

            // Perpendicular props come out mirrored top-to-bottom versus the art's up, so reflect Y to
            // stand them upright. Aligned props keep their normal Y.
            float scaleY = alignToNormal ? propScale : -propScale;

            // Z jitter spreads the band's depth; the graded variant interpolates factor across it.
            float z = Range(rng, zJitter);

            placements.Add(new PropPlacement
            {
                entry = entry,
                worldPosition = new Vector3(position.x, position.y, z),
                worldRotation = Quaternion.Euler(0f, 0f, angle),
                localScale = new Vector3(scaleX, scaleY, propScale),
                sortingOrder = sorting,
                materialOverride = material,
                parallaxFactor = bakedFactor
            });

            float width = SpriteWidth(entry.prefab) * propScale;
            float step = width * (1f - Mathf.Clamp(overlap + Range(rng, overlapJitter), 0f, 0.97f));

            // Never advance by nothing, or the walk would never reach the end of the line.
            cursor += Mathf.Max(step, 0.05f);
        }
    }

    /// <summary>
    /// Scatters props inside the shape treated as a closed polygon: a jittered grid clipped to the
    /// polygon interior. Freestanding props (bushes, nature behind the player), so there is no normal
    /// to align or offset to — each prop stands upright with a little rotation and mirror variety.
    /// Needs at least three points to enclose an area.
    /// </summary>
    protected void AppendFill(List<PropPlacement> placements, int setIndex, Material material,
                              float zJitter, float bakedFactor, int baseSorting, System.Random rng)
    {
        if (library == null || points.Count < 3)
            return;

        // Work in world XY so the grid matches where the line was drawn, whatever the transform.
        var polygon = new Vector2[points.Count];
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 w = GetWorldPoint(i);
            polygon[i] = new Vector2(w.x, w.y);
            if (w.x < minX) minX = w.x;
            if (w.y < minY) minY = w.y;
            if (w.x > maxX) maxX = w.x;
            if (w.y > maxY) maxY = w.y;
        }

        float spacing = Mathf.Max(0.05f, fillSpacing);
        float wander = fillJitter * spacing * 0.5f;

        for (float y = minY; y <= maxY; y += spacing)
        {
            for (float x = minX; x <= maxX; x += spacing)
            {
                if (placements.Count >= MaxProps)
                    return;

                var cell = new Vector2(x + Range(rng, wander), y + Range(rng, wander));
                if (!PointInPolygon(cell, polygon))
                    continue;

                PropEntry entry = library.PickProp(setIndex, rng);
                if (entry == null)
                    return;

                float propScale = library.RollScale(rng);
                float angle = Range(rng, rotationJitter);
                float scaleX = mirrorVariety && rng.Next(2) == 0 ? -propScale : propScale;
                float z = Range(rng, zJitter);

                placements.Add(new PropPlacement
                {
                    entry = entry,
                    worldPosition = new Vector3(cell.x, cell.y, z),
                    worldRotation = Quaternion.Euler(0f, 0f, angle),
                    localScale = new Vector3(scaleX, propScale, propScale),
                    sortingOrder = baseSorting,
                    materialOverride = material,
                    parallaxFactor = bakedFactor
                });
            }
        }
    }

    /// <summary>Even-odd point-in-polygon test in the XY plane.</summary>
    private static bool PointInPolygon(Vector2 p, Vector2[] polygon)
    {
        bool inside = false;
        int n = polygon.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            if ((a.y > p.y) != (b.y > p.y))
            {
                float t = (p.y - a.y) / (b.y - a.y);
                if (p.x < a.x + t * (b.x - a.x))
                    inside = !inside;
            }
        }
        return inside;
    }

    // -------------------------------------------------------------------------
    // Runtime parallax
    // -------------------------------------------------------------------------

    /// <summary>One placed prop, with the spot it belongs and the factor its depth maps to.</summary>
    protected struct PropParallax
    {
        public Transform transform;
        public Vector2 anchor;
        public float factor;
    }

    protected PropParallax[] runtimeProps;

    private void Start()
    {
        BuildRuntimeProps();
    }

    // Register with the central driver only in play mode — the editor must not spawn the driver or
    // move props while authoring. OnEnable runs before Start, so runtimeProps may be null on the first
    // driver tick; ApplyParallax guards that.
    private void OnEnable()
    {
        if (Application.isPlaying)
            ScatterLineParallaxDriver.Register(this);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            ScatterLineParallaxDriver.Unregister(this);
    }

    /// <summary>
    /// Caches every placed prop with its anchor and parallax factor. How the factor is chosen differs
    /// by variant — graded across the line's Z span, or read straight off each prop's baked layer
    /// factor — so the concrete class fills <see cref="runtimeProps"/>. Built once from the generated
    /// list, which persists into play.
    /// </summary>
    protected abstract void BuildRuntimeProps();

    /// <summary>
    /// Places every prop relative to its anchor from <paramref name="cameraPos"/>, called by
    /// <see cref="ScatterLineParallaxDriver"/> in LateUpdate so Cinemachine has already moved the
    /// camera. Anchored and absolute: a prop sits on its authored spot when the camera is on it and
    /// slides off by its factor as the camera moves away, so nothing accumulates or drifts. Negative
    /// factors overshoot and read as in front; positive track the camera and read as far back.
    /// </summary>
    public void ApplyParallax(Vector3 cameraPos)
    {
        if (runtimeProps == null)
            return;

        for (int i = 0; i < runtimeProps.Length; i++)
        {
            Transform t = runtimeProps[i].transform;
            if (t == null)
                continue;

            float factor = runtimeProps[i].factor;
            Vector2 anchor = runtimeProps[i].anchor;

            float x = anchor.x + (cameraPos.x - anchor.x) * factor;
            float y = anchor.y + (cameraPos.y - anchor.y) * factor * verticalInfluence;
            t.position = new Vector3(x, y, t.position.z);
        }
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

    /// <summary>Symmetric random in [-amount, amount].</summary>
    protected static float Range(System.Random rng, float amount)
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
        // Faint line so unselected lines are still findable. The selected line gets the full handle
        // overlay from the editor instead.
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
