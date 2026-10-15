using System.Collections.Generic;
using UnityEngine;

namespace Player
{
/// <summary>
/// A solid stretch of climbable surface along one horizontal line, in world x.
/// </summary>
public struct ClimbSpan
{
    public float left;
    public float right;

    public float Center => (left + right) * 0.5f;
    public float HalfWidth => (right - left) * 0.5f;

    public bool Contains(float x) => x >= left && x <= right;

    /// <summary>How far x is outside the span, 0 when inside.</summary>
    public float DistanceTo(float x) => x < left ? left - x : x > right ? x - right : 0f;

    /// <summary>Clamps x to the span, held margin inside each side (the centre when the span is narrower).</summary>
    public float Clamp(float x, float margin)
    {
        float min = left + margin;
        float max = right - margin;
        return min < max ? Mathf.Clamp(x, min, max) : Center;
    }
}

/// <summary>
/// Cuts climbable colliders along a horizontal line and returns the solid spans, so the climb can tell
/// where the middle of a trunk is at any height. Polygon colliders are cut along their real outline, which
/// is what lets a split read as two arms; any other collider falls back to its bounds.
/// </summary>
public static class ClimbSlices
{
    private static readonly List<Vector2> path = new List<Vector2>();
    private static readonly List<float> crossings = new List<float>();

    /// <summary>
    /// Fills <paramref name="spans"/> with the solid spans of every collider at height y, sorted left to right.
    /// Spans closer than <paramref name="mergeGap"/> are joined, so seams between pieces and small notches in
    /// an outline do not split the trunk into slivers.
    /// </summary>
    public static void Slice(List<Collider2D> colliders, float y, List<ClimbSpan> spans, float mergeGap)
    {
        spans.Clear();
        foreach (var col in colliders)
        {
            if (col is PolygonCollider2D polygon)
                SlicePolygon(polygon, y, spans);
            else if (y >= col.bounds.min.y && y <= col.bounds.max.y)
                spans.Add(new ClimbSpan { left = col.bounds.min.x, right = col.bounds.max.x });
        }

        Merge(spans, mergeGap);
    }

    private static void SlicePolygon(PolygonCollider2D polygon, float y, List<ClimbSpan> spans)
    {
        Transform t = polygon.transform;
        for (int p = 0; p < polygon.pathCount; p++)
        {
            polygon.GetPath(p, path);
            crossings.Clear();

            Vector2 previous = t.TransformPoint(path[path.Count - 1] + polygon.offset);
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 current = t.TransformPoint(path[i] + polygon.offset);

                // Each edge that straddles the line crosses it once.
                if ((previous.y > y) != (current.y > y))
                {
                    float along = (y - previous.y) / (current.y - previous.y);
                    crossings.Add(previous.x + along * (current.x - previous.x));
                }

                previous = current;
            }

            // Sorted crossings alternate in, out, in, out along the line.
            crossings.Sort();
            for (int i = 0; i + 1 < crossings.Count; i += 2)
                spans.Add(new ClimbSpan { left = crossings[i], right = crossings[i + 1] });
        }
    }

    private static void Merge(List<ClimbSpan> spans, float mergeGap)
    {
        if (spans.Count < 2) return;

        spans.Sort((a, b) => a.left.CompareTo(b.left));

        int write = 0;
        for (int read = 1; read < spans.Count; read++)
        {
            ClimbSpan merged = spans[write];
            if (spans[read].left <= merged.right + mergeGap)
            {
                merged.right = Mathf.Max(merged.right, spans[read].right);
                spans[write] = merged;
            }
            else
            {
                spans[++write] = spans[read];
            }
        }

        spans.RemoveRange(write + 1, spans.Count - write - 1);
    }
}
}
