using System.Collections.Generic;
using UnityEngine;

namespace Player
{
public partial class PlayerMoveManager : MonoBehaviour
{
    [Header("Climb Guide: Splines")]
    /// <summary>
    /// How far outside a strand's width the player can be and still count as on it, in world units.
    /// </summary>
    [SerializeField] private float climbStrandReach = 0.5f;

    /// <summary>One spline of one tree piece's climb path.</summary>
    private struct StrandRef
    {
        public ClimbPath path;
        public int strand;

        public bool IsValid => path != null && path.isActiveAndEnabled && path.IsClimbable(strand);
        public bool Is(StrandRef other) => path == other.path && strand == other.strand;
    }

    private readonly List<StrandRef> nearbyStrands = new List<StrandRef>();

    /// <summary>The strand being climbed.</summary>
    private StrandRef climbStrand;

    /// <summary>The strand the guide is steering toward: the climbed one, a split's other arm, or the next piece up.</summary>
    private StrandRef climbTargetStrand;

    /// <summary>
    /// True while the spline guide has the player on a strand. The strand is then what the squirrel is climbing,
    /// so the climb carries on across holes between the colliders of two pieces.
    /// </summary>
    private bool IsHeldByClimbStrand() =>
        useClimbGuide && climbGuideSource == ClimbGuideSource.Splines && IsOnStrand(climbStrand, transform.position);

    /// <summary>
    /// Spline source: the trunk here, read across the strand being climbed, and the direction toward a point a
    /// little way up the strand the heading points at. False when no strand is in reach.
    /// </summary>
    private bool TrySampleSplineGuide(Vector2 position, float lookahead, Vector2 heading, out ClimbSpan here, out Vector2 trunk)
    {
        here = default;
        trunk = Vector2.up;
        GatherNearbyStrands(position, lookahead);

        // Stay on the strand being climbed while still on it; otherwise take the nearest one in reach.
        if (!IsOnStrand(climbStrand, position) && !TryAcquireStrand(position)) return false;

        climbStrand.path.Nearest(climbStrand.strand, position, out Vector2 center, out float t);
        Vector2 tangent = climbStrand.path.TangentAt(climbStrand.strand, t);
        float halfWidth = climbStrand.path.WidthAt(climbStrand.strand, t) * 0.5f;
        here = HorizontalSpan(center, tangent, halfWidth, position.y);

        if (TryPickStrandAhead(position, center, halfWidth, t, lookahead, heading, out Vector2 ahead)
            && (ahead - center).sqrMagnitude > 0.0025f)
        {
            trunk = (ahead - center).normalized;
        }

        if (drawClimbGuide)
            DrawStrand(climbStrand, Color.magenta);

        return true;
    }

    /// <summary>Spline source: the trunk at a point's height, read across the strand being climbed.</summary>
    private bool TrySplineSpanAt(Vector2 point, out ClimbSpan span)
    {
        span = default;
        if (!climbStrand.IsValid) return false;

        climbStrand.path.Nearest(climbStrand.strand, point, out Vector2 center, out float t);
        span = HorizontalSpan(center, climbStrand.path.TangentAt(climbStrand.strand, t),
            climbStrand.path.WidthAt(climbStrand.strand, t) * 0.5f, point.y);
        return true;
    }

    /// <summary>
    /// Picks the point ahead to steer toward. Candidates are further up the climbed strand, and the start of any
    /// strand that begins on it within the look-ahead: a split's other arm, or the next piece's strand where two
    /// pieces overlap. The candidate closest to the heading wins, but the current target is kept unless another
    /// lines up better by climbArmSwitchAngle. Near the end of the climbed strand, only what carries on counts.
    /// </summary>
    private bool TryPickStrandAhead(Vector2 position, Vector2 center, float halfWidth, float t,
        float lookahead, Vector2 heading, out Vector2 ahead)
    {
        ahead = center;
        float here = climbStrand.path.DistanceAt(climbStrand.strand, t);
        StrandRef best = default, kept = default;
        Vector2 bestPoint = center, keptPoint = center;
        float bestAngle = float.MaxValue, keptAngle = float.MaxValue;
        bool anyOther = false;

        foreach (var candidate in nearbyStrands)
        {
            if (candidate.Is(climbStrand)) continue;

            // Starts on the climbed strand itself, between a little behind the player and the look-ahead. Measured
            // along the strand, not along a straight line past its end, so a piece further up is never picked
            // over the one directly above.
            Vector2 start = candidate.path.PointAt(candidate.strand, 0f);
            float offStrand = climbStrand.path.Nearest(climbStrand.strand, start, out _, out float startT);
            if (offStrand > climbStrand.path.WidthAt(climbStrand.strand, startT) * 0.5f + climbStrandReach) continue;
            float along = climbStrand.path.DistanceAt(climbStrand.strand, startT) - here;
            if (along < -halfWidth || along > lookahead) continue;

            Vector2 point = candidate.path.PointAhead(candidate.strand, 0f, Mathf.Max(lookahead - Mathf.Max(along, 0f), climbGuideMinLookahead), out _);
            Consider(candidate, point);
            anyOther = true;
        }

        Vector2 own = climbStrand.path.PointAhead(climbStrand.strand, t, lookahead, out float ownT);
        if (ownT < 0.999f || !anyOther)
            Consider(climbStrand, own);

        if (best.path == null) return false;

        bool keep = kept.path != null && keptAngle - bestAngle < climbArmSwitchAngle;
        climbTargetStrand = keep ? kept : best;
        ahead = keep ? keptPoint : bestPoint;

        // Move onto the target once the player is on it and either closer to it than to the strand being
        // climbed, or the climbed strand has run out.
        if (!climbTargetStrand.Is(climbStrand) && IsOnStrand(climbTargetStrand, position))
        {
            float onTarget = climbTargetStrand.path.Nearest(climbTargetStrand.strand, position, out _, out _);
            float onCurrent = climbStrand.path.Nearest(climbStrand.strand, position, out _, out float currentT);
            if (onTarget <= onCurrent || currentT >= 0.999f)
                climbStrand = climbTargetStrand;
        }

        if (drawClimbGuide)
            DrawCross(ahead, Color.cyan);

        return true;

        void Consider(StrandRef candidate, Vector2 point)
        {
            float angle = Vector2.Angle(point - center, heading);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = candidate;
                bestPoint = point;
            }
            if (candidate.Is(climbTargetStrand))
            {
                keptAngle = angle;
                kept = candidate;
                keptPoint = point;
            }
        }
    }

    /// <summary>True when the player is within the strand's width plus climbStrandReach.</summary>
    private bool IsOnStrand(StrandRef strand, Vector2 position)
    {
        if (!strand.IsValid) return false;
        float distance = strand.path.Nearest(strand.strand, position, out _, out float t);
        return distance <= strand.path.WidthAt(strand.strand, t) * 0.5f + climbStrandReach;
    }

    /// <summary>Takes the nearby strand the player is deepest inside, if any is in reach.</summary>
    private bool TryAcquireStrand(Vector2 position)
    {
        float bestDepth = float.MaxValue;
        StrandRef best = default;
        foreach (var candidate in nearbyStrands)
        {
            float distance = candidate.path.Nearest(candidate.strand, position, out _, out float t);
            float depth = distance - candidate.path.WidthAt(candidate.strand, t) * 0.5f;
            if (depth <= climbStrandReach && depth < bestDepth)
            {
                bestDepth = depth;
                best = candidate;
            }
        }

        climbStrand = best;
        climbTargetStrand = best;
        return best.path != null;
    }

    /// <summary>Collects the strands of every climb path whose bounds reach the player and the look-ahead.</summary>
    private void GatherNearbyStrands(Vector2 position, float lookahead)
    {
        nearbyStrands.Clear();
        Bounds reach = new Bounds(position + Vector2.up * (lookahead * 0.5f), new Vector3(lookahead * 2f + 2f, lookahead + 2f, 1f));
        foreach (var path in ClimbPath.All)
        {
            Bounds bounds = path.WorldBounds;
            bounds.center = new Vector3(bounds.center.x, bounds.center.y, 0f);
            if (!bounds.Intersects(reach)) continue;

            for (int strand = 0; strand < path.StrandCount; strand++)
                if (path.IsClimbable(strand))
                    nearbyStrands.Add(new StrandRef { path = path, strand = strand });
        }
    }

    /// <summary>
    /// The trunk across a strand, as a horizontal span at height y so it plugs into the same edge and centre
    /// rules as the slices. A leaning strand is wider measured horizontally.
    /// </summary>
    private static ClimbSpan HorizontalSpan(Vector2 center, Vector2 tangent, float halfWidth, float y)
    {
        float upright = Mathf.Max(Mathf.Abs(tangent.y), 0.3f);
        float x = Mathf.Abs(tangent.y) > 0.05f ? center.x + (y - center.y) * tangent.x / tangent.y : center.x;
        float half = halfWidth / upright;
        return new ClimbSpan { left = x - half, right = x + half };
    }

    private void DrawStrand(StrandRef strand, Color color)
    {
        const int Samples = 16;
        Vector2 previous = strand.path.PointAt(strand.strand, 0f);
        for (int i = 1; i <= Samples; i++)
        {
            Vector2 point = strand.path.PointAt(strand.strand, i / (float)Samples);
            Debug.DrawLine(previous, point, color, Time.fixedDeltaTime);
            previous = point;
        }
    }

    private void DrawCross(Vector2 point, Color color)
    {
        float size = climbGuideArrowLength * 0.1f;
        Debug.DrawLine(point + new Vector2(-size, -size), point + new Vector2(size, size), color, Time.fixedDeltaTime);
        Debug.DrawLine(point + new Vector2(-size, size), point + new Vector2(size, -size), color, Time.fixedDeltaTime);
    }
}
}
