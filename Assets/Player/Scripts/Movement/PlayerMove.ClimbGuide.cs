using System.Collections.Generic;
using UnityEngine;

namespace Player
{
public partial class PlayerMoveManager : MonoBehaviour
{
    [Header("Climb Guide")]
    /// <summary>
    /// Climb along a heading that follows the trunk on its own, with the stick or keys turning it.
    /// Off falls back to the plain soft edge climb.
    /// </summary>
    [SerializeField] private bool useClimbGuide = true;

    /// <summary>Where the guide reads the trunk from.</summary>
    private enum ClimbGuideSource
    {
        /// <summary>Cut the trunk's colliders along horizontal lines.</summary>
        Slices,
        /// <summary>Follow the hand-editable centreline splines on each tree piece (ClimbPath).</summary>
        Splines,
    }

    /// <summary>
    /// Where the guide reads the middle and direction of the trunk from. Switchable in Play mode to compare.
    /// </summary>
    [SerializeField] private ClimbGuideSource climbGuideSource = ClimbGuideSource.Splines;

    /// <summary>
    /// How far ahead the guide looks for the middle of the trunk, in seconds of climbing at the current speed.
    /// </summary>
    [SerializeField] private float climbGuideLookahead = 0.2f;

    /// <summary>
    /// Shortest look-ahead, in world units, so a slow or fatigued climb still reads the trunk ahead.
    /// </summary>
    [SerializeField] private float climbGuideMinLookahead = 0.4f;

    /// <summary>
    /// How fast full stick or a held key turns the heading, in degrees per second.
    /// </summary>
    [SerializeField] private float climbHeadingTurnSpeed = 120f;

    /// <summary>
    /// How quickly the heading lines up with the trunk on its own, per second. Lower lags further behind on
    /// bends. Holding a direction settles the heading about climbHeadingTurnSpeed / this many degrees off the trunk.
    /// </summary>
    [SerializeField] private float climbHeadingFollowRate = 3f;

    /// <summary>
    /// Angle between the heading and the trunk, in degrees, within which the climb keeps full speed.
    /// </summary>
    [SerializeField] private float climbFullSpeedAngle = 10f;

    /// <summary>
    /// Angle between the heading and the trunk, in degrees, at which the climb is slowest.
    /// </summary>
    [SerializeField] private float climbSlowestAngle = 90f;

    /// <summary>
    /// Climb speed at climbSlowestAngle and beyond, as a fraction of full speed.
    /// </summary>
    [Range(0f, 1f)]
    [SerializeField] private float climbSlowestSpeed = 0.5f;

    /// <summary>
    /// Climb speed at the very edge of the trunk, as a fraction of the speed in the middle.
    /// </summary>
    [Range(0f, 1f)]
    [SerializeField] private float climbEdgeSpeed = 0.6f;

    /// <summary>
    /// Gaps narrower than this between two stretches of trunk are treated as solid, in world units.
    /// Smooths over seams between pieces and notches in a collider outline.
    /// </summary>
    [SerializeField] private float climbSliceMergeGap = 0.3f;

    /// <summary>
    /// How much better another arm of a split must line up with the heading, in degrees, before the guide
    /// switches to it. Keeps a split from flickering between arms.
    /// </summary>
    [SerializeField] private float climbArmSwitchAngle = 10f;

    /// <summary>
    /// Draws the spans and the chosen target in the Scene view while climbing.
    /// </summary>
    [SerializeField] private bool drawClimbGuide = true;

    /// <summary>
    /// Length of the trunk and heading arrows drawn by the guide, in world units.
    /// </summary>
    [SerializeField] private float climbGuideArrowLength = 3f;

    private readonly List<Collider2D> guideColliders = new List<Collider2D>();
    private readonly List<ClimbSpan> hereSpans = new List<ClimbSpan>();
    private readonly List<ClimbSpan> aheadSpans = new List<ClimbSpan>();
    private readonly List<ClimbSpan> nextSpans = new List<ClimbSpan>();

    /// <summary>World x of the arm the guide is heading for, NaN until one is picked.</summary>
    private float climbGuideTargetX = float.NaN;

    /// <summary>Direction of the climb, in degrees from straight up (positive is to the left).</summary>
    private float climbHeadingAngle;

    private Vector2 ClimbHeading => Quaternion.Euler(0f, 0f, climbHeadingAngle) * Vector2.up;

    /// <summary>
    /// Plans this step's climb move along the heading. The heading keeps its direction, eases toward the trunk's
    /// direction on its own (lagging on bends, lining up on straights), and turns with the stick or keys. The closer
    /// it lines up with the trunk, the faster the climb. Returns false when the guide cannot read the surface here,
    /// leaving the soft edge climb to handle the step.
    /// </summary>
    private bool TryGuideClimb(Vector2 moveInput, out Vector2 moveLocation)
    {
        Vector2 position = transform.position;
        moveLocation = position;

        float lookahead = Mathf.Max(currentClimbSpeed * climbGuideLookahead, climbGuideMinLookahead);

        // The trunk here, and its direction: from its middle here toward its middle a little way ahead, on the
        // arm the heading points at. Only the direction, so being off-centre is left for the player to correct.
        bool sampled = climbGuideSource == ClimbGuideSource.Splines
            ? TrySampleSplineGuide(position, lookahead, ClimbHeading, out ClimbSpan here, out Vector2 trunk)
            : TrySampleSliceGuide(position, lookahead, ClimbHeading, out here, out trunk);
        if (!sampled) return false;

        bool onTrunk = here.Contains(position.x);

        SteerClimbHeading(moveInput.x, trunk);
        Vector2 direction = ClimbHeading;

        // The further the heading is from the trunk, the slower the climb.
        float angle = Vector2.Angle(trunk, direction);
        float alignSpeed = Mathf.Lerp(1f, climbSlowestSpeed, Mathf.InverseLerp(climbFullSpeedAngle, climbSlowestAngle, angle));

        // Ease sideways movement off near the side being moved toward. The heading itself is left pointing
        // where it was, so the player steers it back.
        float wall = direction.x > 0f ? here.right - position.x : position.x - here.left;
        direction.x *= Mathf.SmoothStep(0f, 1f, (wall - climbEdgeMargin) / Mathf.Max(climbEdgeZone, 0.001f));

        // Fastest in the middle of the trunk.
        float offCenter = Mathf.Clamp01(Mathf.Abs(position.x - here.Center) / Mathf.Max(here.HalfWidth, 0.001f));
        float centerSpeed = Mathf.Lerp(1f, climbEdgeSpeed, offCenter * offCenter);

        moveLocation = position + direction * (currentClimbSpeed * alignSpeed * centerSpeed * Time.deltaTime);

        // Keep the move inside the trunk at its new height, so a fast step cannot skip past the soft edge. From
        // outside it (just grabbed on, or a spline narrower than the art), ease in at climb speed instead of snapping.
        bool hasNext = climbGuideSource == ClimbGuideSource.Splines
            ? TrySplineSpanAt(moveLocation, out ClimbSpan next)
            : TrySliceSpanAt(moveLocation, out next);
        if (hasNext)
        {
            float inside = next.Clamp(moveLocation.x, climbEdgeMargin);
            moveLocation.x = onTrunk ? inside : Mathf.MoveTowards(moveLocation.x, inside, currentClimbSpeed * Time.deltaTime);
        }

        if (drawClimbGuide)
            DrawClimbGuide(position, here, trunk, ClimbHeading);

        return true;
    }

    /// <summary>
    /// Slice source: the span of trunk the player is in, and the direction toward the middle of the arm ahead.
    /// False when the player is not inside a slice yet.
    /// </summary>
    private bool TrySampleSliceGuide(Vector2 position, float lookahead, Vector2 heading, out ClimbSpan here, out Vector2 trunk)
    {
        trunk = Vector2.up;
        GatherGuideColliders(position, lookahead);

        ClimbSlices.Slice(guideColliders, position.y, hereSpans, climbSliceMergeGap);
        if (!TryFindSpan(hereSpans, position.x, out here) || !here.Contains(position.x)) return false;
        Vector2 hereCenter = new Vector2(here.Center, position.y);

        float aheadY = position.y + lookahead;
        ClimbSlices.Slice(guideColliders, aheadY, aheadSpans, climbSliceMergeGap);
        if (TryPickArm(position, hereCenter, aheadY, heading, out Vector2 aheadCenter))
            trunk = (aheadCenter - hereCenter).normalized;
        else
            climbGuideTargetX = float.NaN;

        if (drawClimbGuide)
        {
            foreach (var span in aheadSpans)
                Debug.DrawLine(new Vector2(span.left, aheadY), new Vector2(span.right, aheadY), Color.cyan, Time.fixedDeltaTime);
        }

        return true;
    }

    /// <summary>Slice source: the span of trunk at a point's height, nearest the point.</summary>
    private bool TrySliceSpanAt(Vector2 point, out ClimbSpan span)
    {
        ClimbSlices.Slice(guideColliders, point.y, nextSpans, climbSliceMergeGap);
        return TryFindSpan(nextSpans, point.x, out span);
    }

    /// <summary>
    /// Turns the heading for this step: it eases toward the trunk's direction, and input turns it, full rate for a
    /// key or full stick and proportionally for a partial stick, so keyboard and gamepad reach the same angles.
    /// Kept within maxClimbAngle of straight up.
    /// </summary>
    private void SteerClimbHeading(float input, Vector2 trunk)
    {
        float follow = 1f - Mathf.Exp(-climbHeadingFollowRate * Time.deltaTime);
        float trunkAngle = Vector2.SignedAngle(Vector2.up, trunk);

        climbHeadingAngle = Mathf.Lerp(climbHeadingAngle, trunkAngle, follow);
        climbHeadingAngle -= input * climbHeadingTurnSpeed * Time.deltaTime;
        climbHeadingAngle = Mathf.Clamp(climbHeadingAngle, -maxClimbAngle, maxClimbAngle);
    }

    /// <summary>
    /// Picks the span ahead to climb toward. Only spans reachable through the trunk count, so a neighbouring
    /// tree is never chosen. The arm whose direction is closest to the heading wins, but the arm already being
    /// climbed is kept unless another lines up better by climbArmSwitchAngle.
    /// </summary>
    private bool TryPickArm(Vector2 position, Vector2 hereCenter, float aheadY, Vector2 heading, out Vector2 center)
    {
        center = position;
        int best = -1;
        float bestAngle = float.MaxValue;
        int current = -1;
        float currentGap = float.MaxValue;
        float currentAngle = 0f;

        for (int i = 0; i < aheadSpans.Count; i++)
        {
            Vector2 candidate = new Vector2(aheadSpans[i].Center, aheadY);
            if (!IsReachable(position, candidate)) continue;

            float candidateAngle = Vector2.Angle(candidate - hereCenter, heading);
            if (candidateAngle < bestAngle)
            {
                bestAngle = candidateAngle;
                best = i;
            }

            if (!float.IsNaN(climbGuideTargetX))
            {
                float gap = Mathf.Abs(candidate.x - climbGuideTargetX);
                if (gap < currentGap)
                {
                    currentGap = gap;
                    current = i;
                    currentAngle = candidateAngle;
                }
            }
        }

        if (best < 0) return false;

        int chosen = current >= 0 && currentAngle - bestAngle < climbArmSwitchAngle ? current : best;
        center = new Vector2(aheadSpans[chosen].Center, aheadY);
        climbGuideTargetX = center.x;
        return true;
    }

    /// <summary>True when the straight line to the target stays on climbable surface.</summary>
    private bool IsReachable(Vector2 from, Vector2 to)
    {
        for (int i = 1; i <= 3; i++)
        {
            if (Physics2D.OverlapPoint(Vector2.Lerp(from, to, i * 0.25f), climbableLayer) == null)
                return false;
        }
        return true;
    }

    /// <summary>The span containing x, or else the nearest one.</summary>
    private static bool TryFindSpan(List<ClimbSpan> spans, float x, out ClimbSpan span)
    {
        span = default;
        float bestDistance = float.MaxValue;
        foreach (var candidate in spans)
        {
            float distance = candidate.DistanceTo(x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                span = candidate;
            }
        }
        return bestDistance < float.MaxValue;
    }

    /// <summary>Collects the climbable colliders between the player and the look-ahead line.</summary>
    private void GatherGuideColliders(Vector2 position, float lookahead)
    {
        float reach = lookahead * Mathf.Tan(maxClimbAngle * Mathf.Deg2Rad) + 1f;
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = climbableLayer,
            useTriggers = Physics2D.queriesHitTriggers,
        };
        Physics2D.OverlapArea(
            new Vector2(position.x - reach, position.y - 0.5f),
            new Vector2(position.x + reach, position.y + lookahead + 0.5f),
            filter, guideColliders);
    }

    /// <summary>
    /// Yellow: the trunk here. Green: the trunk's direction. White: the heading. Each source adds its own cyan
    /// marks for what it looked at ahead.
    /// </summary>
    private void DrawClimbGuide(Vector2 position, ClimbSpan here, Vector2 trunk, Vector2 heading)
    {
        float duration = Time.fixedDeltaTime;
        Debug.DrawLine(new Vector2(here.left, position.y), new Vector2(here.right, position.y), Color.yellow, duration);
        DrawGuideArrow(position, trunk, Color.green, duration);
        DrawGuideArrow(position, heading, Color.white, duration);
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
