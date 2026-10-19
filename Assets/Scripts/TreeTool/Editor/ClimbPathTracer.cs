using System.Collections.Generic;
using Player;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Builds a starting <see cref="ClimbPath"/> for a tree piece by walking up its climbable colliders, one slice at
/// a time, and laying a knot at the middle of the trunk with its width. Where the trunk forks, a second strand
/// starts at the fork. The result is meant as a first pass to edit by hand with the spline tools.
/// </summary>
public static class ClimbPathTracer
{
    private const string SycamoreFolder = "Assets/Visuals/World/Forest/Trees/Sycamore";

    /// <summary>Height between slices, in world units.</summary>
    private const float Step = 1f;

    /// <summary>Spans narrower than this are notches in the outline, not trunk.</summary>
    private const float MinWidth = 0.3f;

    /// <summary>Gaps narrower than this are treated as solid, matching the climb guide's slices.</summary>
    private const float MergeGap = 0.3f;

    /// <summary>No knot is narrower than this fraction of its strand's median width.</summary>
    private const float ThinWidthFraction = 0.6f;

    /// <summary>A knot is dropped when it sits this close to the line through its neighbours, in world units.</summary>
    private const float SimplifyTolerance = 0.15f;

    [MenuItem("Tools/The Last Acorn/Climb Paths/Trace Sycamore Prefabs")]
    private static void TraceSycamorePrefabs()
    {
        int mask = ClimbableMask();
        var traced = new List<string>();
        var skipped = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SycamoreFolder }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (TracePrefab(assetPath, mask, out int strands))
                traced.Add($"{name} ({strands} strand{(strands == 1 ? "" : "s")})");
            else
                skipped.Add(name);
        }

        Debug.Log($"[{nameof(ClimbPathTracer)}] Traced {traced.Count}: {string.Join(", ", traced)}\n" +
                  $"Skipped (no climbable collider): {string.Join(", ", skipped)}");
    }

    [MenuItem("CONTEXT/ClimbPath/Trace From Colliders")]
    private static void TraceFromContext(MenuCommand command)
    {
        var path = (ClimbPath)command.context;
        Undo.RecordObjects(new Object[] { path, path.Container }, "Trace Climb Path");
        int strands = Trace(path, ClimbableColliders(path.gameObject, ClimbableMask()));
        EditorUtility.SetDirty(path);
        EditorUtility.SetDirty(path.Container);
        Debug.Log($"[{nameof(ClimbPathTracer)}] Traced {strands} strand(s) on {path.name}.", path);
    }

    [MenuItem("Tools/The Last Acorn/Climb Paths/Flatten Sycamore Prefabs")]
    private static void FlattenSycamorePrefabs()
    {
        var flattened = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SycamoreFolder }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                ClimbPath path = root.GetComponent<ClimbPath>();
                if (path == null) continue;

                int knots = Flatten(path);
                if (knots == 0) continue;

                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                flattened.Add($"{System.IO.Path.GetFileNameWithoutExtension(assetPath)} ({knots})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"[{nameof(ClimbPathTracer)}] Flattened {flattened.Count} prefab(s), knots changed: {string.Join(", ", flattened)}");
    }

    [MenuItem("CONTEXT/ClimbPath/Flatten To 2D")]
    private static void FlattenFromContext(MenuCommand command)
    {
        var path = (ClimbPath)command.context;
        Undo.RecordObject(path.Container, "Flatten Climb Path");
        int knots = Flatten(path);
        EditorUtility.SetDirty(path.Container);
        Debug.Log($"[{nameof(ClimbPathTracer)}] Flattened {knots} knot(s) on {path.name}.", path);
    }

    /// <summary>
    /// Puts every knot of every strand on the piece's flat plane: Z of 0, tangents with no Z, no tilt out of the
    /// plane. The guide finds the nearest point in 3D, so knots dragged off in Z make it read the wrong spot.
    /// Returns how many knots changed.
    /// </summary>
    public static int Flatten(ClimbPath path)
    {
        int changed = 0;
        foreach (Spline spline in path.Container.Splines)
        {
            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];
                quaternion rotation = knot.Rotation;
                float3 tangentIn = math.mul(rotation, knot.TangentIn);
                float3 tangentOut = math.mul(rotation, knot.TangentOut);

                bool flat = math.abs(knot.Position.z) < 0.0001f
                    && math.abs(tangentIn.z) < 0.0001f
                    && math.abs(tangentOut.z) < 0.0001f;
                if (flat) continue;

                // Keep the turn within the plane and drop any tilt out of it, so the handles stay where they were in X and Y.
                tangentIn.z = 0f;
                tangentOut.z = 0f;
                knot.Position.z = 0f;
                knot.Rotation = quaternion.identity;
                knot.TangentIn = tangentIn;
                knot.TangentOut = tangentOut;

                TangentMode mode = spline.GetTangentMode(i);
                spline.SetKnot(i, knot);
                spline.SetTangentMode(i, mode);
                changed++;
            }
        }
        return changed;
    }

    /// <summary>Adds a traced climb path to a prefab asset that has climbable colliders, replacing any existing strands.</summary>
    public static bool TracePrefab(string assetPath, int mask, out int strands)
    {
        strands = 0;
        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            List<Collider2D> colliders = ClimbableColliders(root, mask);
            if (colliders.Count == 0) return false;

            if (root.GetComponent<SplineContainer>() == null) root.AddComponent<SplineContainer>();
            ClimbPath path = root.GetComponent<ClimbPath>() ?? root.AddComponent<ClimbPath>();

            strands = Trace(path, colliders);
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Replaces the path's strands with ones traced from the colliders. Returns the strand count.</summary>
    public static int Trace(ClimbPath path, List<Collider2D> colliders)
    {
        Transform transform = path.transform;
        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.0001f);

        var splines = new List<Spline>();
        path.Widths.Clear();

        foreach (Strand strand in TraceStrands(colliders))
        {
            strand.CorrectWidthsForLean();
            strand.RaiseThinWidths();
            strand.Simplify();
            if (strand.points.Count < 2) continue;

            var spline = new Spline();
            var widths = new ClimbPath.StrandWidths();
            for (int i = 0; i < strand.points.Count; i++)
            {
                Vector3 local = transform.InverseTransformPoint(strand.points[i]);
                local.z = 0f;
                spline.Add(new BezierKnot((float3)local), TangentMode.AutoSmooth);
                widths.knotWidths.Add(strand.widths[i] / scale);
            }

            splines.Add(spline);
            path.Widths.Add(widths);
        }

        path.Container.Splines = splines;
        return splines.Count;
    }

    private class Strand
    {
        public readonly List<Vector2> points = new List<Vector2>();
        public readonly List<float> widths = new List<float>();
        public bool open = true;

        public Vector2 Last => points[points.Count - 1];
        public float LastWidth => widths[widths.Count - 1];

        public void Add(Vector2 point, float width)
        {
            points.Add(point);
            widths.Add(width);
        }

        /// <summary>True when the span overlaps the strand's last span, so it carries on from it.</summary>
        public bool Continues(ClimbSpan span) => span.left <= Last.x + LastWidth * 0.5f && span.right >= Last.x - LastWidth * 0.5f;

        /// <summary>Slices are horizontal, so a leaning strand reads wider than it is. Narrow each width by the lean.</summary>
        public void CorrectWidthsForLean()
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 below = points[Mathf.Max(i - 1, 0)];
                Vector2 above = points[Mathf.Min(i + 1, points.Count - 1)];
                Vector2 direction = (above - below).normalized;
                if (direction != Vector2.zero)
                    widths[i] *= Mathf.Max(Mathf.Abs(direction.y), 0.3f);
            }
        }

        /// <summary>
        /// Pieces taper where they overlap the next piece, so end slices read far thinner than the trunk.
        /// Raises any width below ThinWidthFraction of the strand's median.
        /// </summary>
        public void RaiseThinWidths()
        {
            var sorted = new List<float>(widths);
            sorted.Sort();
            float floor = sorted[sorted.Count / 2] * ThinWidthFraction;
            for (int i = 0; i < widths.Count; i++)
                widths[i] = Mathf.Max(widths[i], floor);
        }

        /// <summary>Drops knots that sit on the line through their neighbours, so there are fewer to edit.</summary>
        public void Simplify()
        {
            for (int i = points.Count - 2; i >= 1; i--)
            {
                Vector2 a = points[i - 1], b = points[i + 1];
                float along = Mathf.Clamp01(Vector2.Dot(points[i] - a, b - a) / Mathf.Max((b - a).sqrMagnitude, 0.0001f));
                float straightWidth = Mathf.Lerp(widths[i - 1], widths[i + 1], along);
                if (Vector2.Distance(points[i], Vector2.Lerp(a, b, along)) < SimplifyTolerance
                    && Mathf.Abs(widths[i] - straightWidth) < SimplifyTolerance)
                {
                    points.RemoveAt(i);
                    widths.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Walks up the colliders a slice at a time. Each span carries on the strand it overlaps; when one strand
    /// overlaps two spans the trunk has forked, and the second span starts a new strand from the fork.
    /// </summary>
    private static List<Strand> TraceStrands(List<Collider2D> colliders)
    {
        Bounds bounds = ColliderBounds(colliders[0]);
        foreach (var col in colliders) bounds.Encapsulate(ColliderBounds(col));

        var strands = new List<Strand>();
        var spans = new List<ClimbSpan>();
        var claimed = new List<bool>();

        for (float y = bounds.min.y + Step * 0.5f; y < bounds.max.y; y += Step)
        {
            ClimbSlices.Slice(colliders, y, spans, MergeGap);
            spans.RemoveAll(span => span.right - span.left < MinWidth);

            claimed.Clear();
            for (int i = 0; i < spans.Count; i++) claimed.Add(false);

            int existing = strands.Count;
            for (int s = 0; s < existing; s++)
            {
                Strand strand = strands[s];
                if (!strand.open) continue;

                int nearest = -1;
                float nearestGap = float.MaxValue;
                for (int i = 0; i < spans.Count; i++)
                {
                    if (claimed[i] || !strand.Continues(spans[i])) continue;
                    float gap = Mathf.Abs(spans[i].Center - strand.Last.x);
                    if (gap < nearestGap)
                    {
                        nearestGap = gap;
                        nearest = i;
                    }
                }

                if (nearest < 0)
                {
                    strand.open = false;
                    continue;
                }

                Vector2 fork = strand.Last;
                float forkWidth = strand.LastWidth;
                claimed[nearest] = true;
                strand.Add(new Vector2(spans[nearest].Center, y), spans[nearest].right - spans[nearest].left);

                // Any other span this strand reaches is another arm of a fork.
                for (int i = 0; i < spans.Count; i++)
                {
                    if (claimed[i] || spans[i].left > fork.x + forkWidth * 0.5f || spans[i].right < fork.x - forkWidth * 0.5f) continue;
                    claimed[i] = true;
                    var arm = new Strand();
                    arm.Add(fork, forkWidth);
                    arm.Add(new Vector2(spans[i].Center, y), spans[i].right - spans[i].left);
                    strands.Add(arm);
                }
            }

            // Spans nothing carries on start strands of their own.
            for (int i = 0; i < spans.Count; i++)
            {
                if (claimed[i]) continue;
                var strand = new Strand();
                strand.Add(new Vector2(spans[i].Center, y), spans[i].right - spans[i].left);
                strands.Add(strand);
            }
        }

        return strands;
    }

    /// <summary>
    /// A polygon's bounds from its own points. A prefab opened for editing is not in the physics scene, so
    /// Collider2D.bounds can come back empty there.
    /// </summary>
    private static Bounds ColliderBounds(Collider2D col)
    {
        if (!(col is PolygonCollider2D polygon) || polygon.GetTotalPointCount() == 0) return col.bounds;

        Bounds bounds = new Bounds(polygon.transform.TransformPoint(polygon.points[0] + polygon.offset), Vector3.zero);
        for (int p = 0; p < polygon.pathCount; p++)
            foreach (Vector2 point in polygon.GetPath(p))
                bounds.Encapsulate(polygon.transform.TransformPoint(point + polygon.offset));
        return bounds;
    }

    private static List<Collider2D> ClimbableColliders(GameObject root, int mask)
    {
        var colliders = new List<Collider2D>();
        foreach (var col in root.GetComponentsInChildren<Collider2D>(true))
            if ((mask & (1 << col.gameObject.layer)) != 0) colliders.Add(col);
        return colliders;
    }

    /// <summary>The player's climbable layers, so tracing matches what the climb can actually grab.</summary>
    private static int ClimbableMask()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab");
        PlayerMoveManager move = player != null ? player.GetComponentInChildren<PlayerMoveManager>(true) : null;
        if (move != null)
        {
            SerializedProperty layers = new SerializedObject(move).FindProperty("climbableLayer");
            if (layers != null) return layers.intValue;
        }
        return LayerMask.GetMask("Climbable", "Slippery");
    }
}
