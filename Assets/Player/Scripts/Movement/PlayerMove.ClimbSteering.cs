using System.Collections.Generic;
using UnityEngine;

namespace Player
{
public partial class PlayerMoveManager : MonoBehaviour
{
    [Header("Climb Steering")]
    /// <summary>
    /// How fast the climb direction turns at full left or right input, in degrees per second.
    /// </summary>
    [SerializeField] private float climbHeadingTurnSpeed = 120f;

    /// <summary>
    /// Angle between the climb direction and the spline, in degrees, within which the climb keeps full speed.
    /// </summary>
    [SerializeField] private float climbFullSpeedAngle = 20f;

    /// <summary>
    /// Angle between the climb direction and the spline, in degrees, at which the climb is slowest.
    /// </summary>
    [SerializeField] private float climbSlowestAngle = 90f;

    /// <summary>
    /// Climb speed at climbSlowestAngle and beyond, as a fraction of full speed.
    /// </summary>
    [Range(0f, 1f)]
    [SerializeField] private float climbSlowestSpeed = 0.5f;

    /// <summary>
    /// How far outside a strand's width the player can be and still count as on it, in world units.
    /// </summary>
    [SerializeField] private float climbStrandReach = 0.5f;

    /// <summary>
    /// Draws the climb direction, the spline's direction and the strand being climbed in the Scene view.
    /// </summary>
    [SerializeField] private bool drawClimbGuide = true;

    /// <summary>
    /// Length of the climb direction and spline arrows, in world units.
    /// </summary>
    [SerializeField] private float climbGuideArrowLength = 3f;

    /// <summary>One spline of one tree piece's climb path.</summary>
    private struct StrandRef
    {
        public ClimbPath path;
        public int strand;
    }

    private readonly List<StrandRef> nearbyStrands = new List<StrandRef>();

    /// <summary>Direction of the climb, in degrees from straight up (positive is to the left).</summary>
    private float climbHeadingAngle;

    private Vector2 ClimbHeading => Quaternion.Euler(0f, 0f, climbHeadingAngle) * Vector2.up;

    /// <summary>
    /// Plans this step's climb move. The player steers the climb direction; it holds its angle with no input.
    /// The spline under the player only sets the speed (full within climbFullSpeedAngle of it, slowing the more
    /// the climb runs across it) and the soft edge at its sides.
    /// </summary>
    private Vector2 PlanClimbMove(float input)
    {
        Vector2 position = transform.position;
        SteerClimbHeading(input);
        TiltGraphic(climbHeadingAngle);
        Vector2 direction = ClimbHeading;
        float speed = currentClimbSpeed;

        if (!TryFindStrand(position, out StrandRef strand, out Vector2 center, out float t))
        {
            if (drawClimbGuide)
                DrawGuideArrow(position, direction, Color.white, Time.fixedDeltaTime);
            return position + direction * (speed * Time.deltaTime);
        }

        Vector2 tangent = strand.path.TangentAt(strand.strand, t);
        ClimbSpan here = HorizontalSpan(center, tangent, strand.path.WidthAt(strand.strand, t) * 0.5f, position.y);

        // The further the climb runs across the spline, the slower it goes.
        float angle = Vector2.Angle(direction, tangent);
        speed *= Mathf.Lerp(1f, climbSlowestSpeed, Mathf.InverseLerp(climbFullSpeedAngle, climbSlowestAngle, angle));

        // Soft edge: sideways movement eases off near the side of the spline being moved toward.
        float wall = direction.x > 0f ? here.right - position.x : position.x - here.left;
        direction.x *= Mathf.SmoothStep(0f, 1f, (wall - climbEdgeMargin) / Mathf.Max(climbEdgeZone, 0.001f));

        Vector2 move = position + direction * (speed * Time.deltaTime);

        // The soft edge stops at the margin at the new height too, so a fast step cannot skip past it.
        if (here.Contains(position.x))
        {
            strand.path.Nearest(strand.strand, move, out Vector2 nextCenter, out float nextT);
            ClimbSpan next = HorizontalSpan(nextCenter, strand.path.TangentAt(strand.strand, nextT),
                strand.path.WidthAt(strand.strand, nextT) * 0.5f, move.y);
            move.x = next.Clamp(move.x, climbEdgeMargin);
        }

        if (drawClimbGuide)
            DrawClimbGuide(position, here, strand, tangent, ClimbHeading);

        return move;
    }

    /// <summary>
    /// Turns the climb direction for this step at a constant rate while input is held: full rate for a key or
    /// full stick, proportionally slower for a partial stick, stopping as soon as input is released.
    /// Kept within maxClimbAngle of straight up.
    /// </summary>
    private void SteerClimbHeading(float input)
    {
        climbHeadingAngle -= input * climbHeadingTurnSpeed * Time.deltaTime;
        climbHeadingAngle = Mathf.Clamp(climbHeadingAngle, -maxClimbAngle, maxClimbAngle);
    }

    /// <summary>
    /// Tilts the squirrel's sprite to face along the climb direction, in degrees from straight up (positive is to
    /// the left). Facing left is a 180° turn about Y, which mirrors a tilt about Z, so the tilt is mirrored back to
    /// keep the sprite in line with the climb direction.
    /// </summary>
    private void TiltGraphic(float angle)
    {
        if (graphic == null) return;

        Vector3 rotation = graphic.transform.eulerAngles;
        rotation.z = IsGraphicFacingRight ? angle : -angle;
        graphic.transform.eulerAngles = rotation;
    }

    /// <summary>The strand the player is deepest inside, if any is within reach.</summary>
    private bool TryFindStrand(Vector2 position, out StrandRef strand, out Vector2 center, out float t)
    {
        GatherNearbyStrands(position);
        strand = default;
        center = position;
        t = 0f;

        float bestDepth = float.MaxValue;
        foreach (var candidate in nearbyStrands)
        {
            float distance = candidate.path.Nearest(candidate.strand, position, out Vector2 nearest, out float nearestT);
            float depth = distance - candidate.path.WidthAt(candidate.strand, nearestT) * 0.5f;
            if (depth > climbStrandReach || depth >= bestDepth) continue;

            bestDepth = depth;
            strand = candidate;
            center = nearest;
            t = nearestT;
        }

        return strand.path != null;
    }

    /// <summary>Collects the strands of every climb path whose bounds reach the player.</summary>
    private void GatherNearbyStrands(Vector2 position)
    {
        nearbyStrands.Clear();
        Bounds reach = new Bounds(position, new Vector3(4f, 4f, 1f));
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
    /// The trunk across a strand, as a horizontal span at height y. A leaning strand is wider measured horizontally.
    /// </summary>
    private static ClimbSpan HorizontalSpan(Vector2 center, Vector2 tangent, float halfWidth, float y)
    {
        float upright = Mathf.Max(Mathf.Abs(tangent.y), 0.3f);
        float x = Mathf.Abs(tangent.y) > 0.05f ? center.x + (y - center.y) * tangent.x / tangent.y : center.x;
        float half = halfWidth / upright;
        return new ClimbSpan { left = x - half, right = x + half };
    }

    /// <summary>Yellow: the strand's width here. Magenta: the strand. Green: the spline's direction. White: the climb direction.</summary>
    private void DrawClimbGuide(Vector2 position, ClimbSpan here, StrandRef strand, Vector2 tangent, Vector2 heading)
    {
        float duration = Time.fixedDeltaTime;
        Debug.DrawLine(new Vector2(here.left, position.y), new Vector2(here.right, position.y), Color.yellow, duration);
        DrawStrand(strand, Color.magenta);
        DrawGuideArrow(position, tangent, Color.green, duration);
        DrawGuideArrow(position, heading, Color.white, duration);
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

    /// <summary>
    /// Debug lines are one pixel wide, so the arrow is drawn as a few side-by-side lines with a head to read
    /// at gameplay zoom.
    /// </summary>
    private void DrawGuideArrow(Vector2 origin, Vector2 direction, Color color, float duration)
    {
        Vector2 tip = origin + direction * climbGuideArrowLength;
        Vector2 side = new Vector2(-direction.y, direction.x) * (climbGuideArrowLength * 0.01f);
        Vector2 back = -direction * (climbGuideArrowLength * 0.2f);

        for (int i = -1; i <= 1; i++)
        {
            Vector2 offset = side * i;
            Debug.DrawLine(origin + offset, tip + offset, color, duration);
            Debug.DrawLine(tip + offset, tip + offset + (Vector2)(Quaternion.Euler(0f, 0f, 30f) * back), color, duration);
            Debug.DrawLine(tip + offset, tip + offset + (Vector2)(Quaternion.Euler(0f, 0f, -30f) * back), color, duration);
        }
    }
}
}
