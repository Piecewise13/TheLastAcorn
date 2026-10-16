using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// The centreline of a climbable tree piece: one spline per strand, drawn bottom to top, with the width of the
/// trunk at each knot. A trunk has one strand; a split has one running up its main arm and one starting at the
/// fork for the other arm. The climb guide follows these when its source is set to Splines, and hops between
/// pieces where one piece's strand starts on another's.
/// <para>
/// Strands are traced from the piece's colliders by Tools > The Last Acorn > Climb Paths, then meant to be
/// edited by hand with the spline tools. Widths are in the piece's local units.
/// </para>
/// </summary>
[RequireComponent(typeof(SplineContainer))]
[DisallowMultipleComponent]
public class ClimbPath : MonoBehaviour
{
    [Serializable]
    public class StrandWidths
    {
        [Tooltip("Width of the trunk at each knot of the matching spline, bottom to top, in local units.")]
        public List<float> knotWidths = new List<float>();
    }

    [Tooltip("One entry per spline in the Spline Container, in the same order.")]
    [SerializeField] private List<StrandWidths> strandWidths = new List<StrandWidths>();

    private static readonly List<ClimbPath> enabledPaths = new List<ClimbPath>();

    /// <summary>Every enabled climb path in the loaded scenes.</summary>
    public static IReadOnlyList<ClimbPath> All => enabledPaths;

    private SplineContainer container;
    private Bounds? worldBounds;

    public SplineContainer Container => container != null ? container : container = GetComponent<SplineContainer>();
    public int StrandCount => Container.Splines.Count;
    public List<StrandWidths> Widths => strandWidths;

    private float Scale => Mathf.Abs(transform.lossyScale.x);

    private void OnEnable() => enabledPaths.Add(this);
    private void OnDisable() => enabledPaths.Remove(this);
    private void OnValidate() => worldBounds = null;

    /// <summary>World bounds of every strand, grown by the widest knot. Cached, as tree pieces do not move.</summary>
    public Bounds WorldBounds
    {
        get
        {
            if (worldBounds.HasValue) return worldBounds.Value;

            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            float widest = 0f;
            for (int i = 0; i < StrandCount; i++)
            {
                if (Container.Splines[i].Count == 0) continue;
                bounds.Encapsulate(Container.Splines[i].GetBounds(transform.localToWorldMatrix));
                if (i < strandWidths.Count)
                    foreach (float width in strandWidths[i].knotWidths)
                        widest = Mathf.Max(widest, width);
            }
            bounds.Expand(widest * Scale);
            worldBounds = bounds;
            return bounds;
        }
    }

    /// <summary>True when the strand exists and has at least two knots to climb along.</summary>
    public bool IsClimbable(int strand) => strand >= 0 && strand < StrandCount && Container.Splines[strand].Count >= 2;

    /// <summary>Width of the trunk at t along the strand, in world units, interpolated between knots.</summary>
    public float WidthAt(int strand, float t)
    {
        List<float> widths = strand < strandWidths.Count ? strandWidths[strand].knotWidths : null;
        if (widths == null || widths.Count == 0) return Scale;

        float knot = Container.Splines[strand].ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Knot);
        int below = Mathf.Clamp(Mathf.FloorToInt(knot), 0, widths.Count - 1);
        int above = Mathf.Min(below + 1, widths.Count - 1);
        return Mathf.Lerp(widths[below], widths[above], knot - below) * Scale;
    }

    /// <summary>Nearest point on the strand to a world point. Returns the world distance to it.</summary>
    public float Nearest(int strand, Vector2 point, out Vector2 nearest, out float t)
    {
        float3 local = transform.InverseTransformPoint(point);
        SplineUtility.GetNearestPoint(Container.Splines[strand], local, out float3 onSpline, out t, 4, 2);
        nearest = transform.TransformPoint(onSpline);
        return Vector2.Distance(point, nearest);
    }

    public Vector2 PointAt(int strand, float t) => (Vector3)Container.EvaluatePosition(strand, t);

    /// <summary>World distance along the strand from its start to t.</summary>
    public float DistanceAt(int strand, float t) =>
        Container.Splines[strand].ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Distance) * Scale;

    /// <summary>Direction of the strand at t, pointing up the strand.</summary>
    public Vector2 TangentAt(int strand, float t) => ((Vector2)(Vector3)Container.EvaluateTangent(strand, t)).normalized;

    /// <summary>The point a world distance further up the strand from t, stopping at its end.</summary>
    public Vector2 PointAhead(int strand, float t, float distance, out float aheadT)
    {
        float3 local = Container.Splines[strand].GetPointAtLinearDistance(t, distance / Mathf.Max(Scale, 0.0001f), out aheadT);
        return transform.TransformPoint(local);
    }

    /// <summary>Draws each strand's two edges and a bar at every knot, so widths can be checked against the art.</summary>
    private void OnDrawGizmos()
    {
        const int Samples = 24;
        Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.7f);

        for (int strand = 0; strand < StrandCount; strand++)
        {
            if (!IsClimbable(strand)) continue;

            Vector2 previousLeft = default, previousRight = default;
            for (int i = 0; i <= Samples; i++)
            {
                float t = i / (float)Samples;
                EdgesAt(strand, t, out Vector2 left, out Vector2 right);
                if (i > 0)
                {
                    Gizmos.DrawLine(previousLeft, left);
                    Gizmos.DrawLine(previousRight, right);
                }
                previousLeft = left;
                previousRight = right;
            }

            Spline spline = Container.Splines[strand];
            for (int knot = 0; knot < spline.Count; knot++)
            {
                float t = spline.ConvertIndexUnit(knot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
                EdgesAt(strand, t, out Vector2 left, out Vector2 right);
                Gizmos.DrawLine(left, right);
            }
        }
    }

    private void EdgesAt(int strand, float t, out Vector2 left, out Vector2 right)
    {
        Vector2 center = PointAt(strand, t);
        Vector2 tangent = TangentAt(strand, t);
        Vector2 across = new Vector2(-tangent.y, tangent.x) * (WidthAt(strand, t) * 0.5f);
        left = center + across;
        right = center - across;
    }
}
