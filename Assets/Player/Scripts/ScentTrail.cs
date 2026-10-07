using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// A single scent-trail instance. Lives on the scent-trail prefab and drives a Visual Effect
/// Graph that draws a trail between the acorn and the player. Each frame it feeds the VFX:
/// "Player" and "Acorn" (Vector3 positions), "Strength" (how bold, 0–1) and "Reach" (how far
/// toward the player the trail gets, 0–1 of the distance).
///
/// Reach is set one of two ways: as a fraction of the distance (<see cref="SetReach"/>), or as a
/// length in world units (<see cref="SetLength"/>), which is turned into a fraction every frame so
/// the trail keeps the same length as the player moves. The last one called wins.
///
/// Strength and reach changes blend over <see cref="transitionSeconds"/> rather than jumping,
/// so a trail can fade out or ease to a new length instead of popping.
///
/// The trail doesn't aim at the player directly: it aims at a point that eases after them over
/// <see cref="followSmoothTime"/>, so it drifts behind as they move instead of snapping to them.
/// </summary>
public class ScentTrail : MonoBehaviour
{
    [SerializeField] private VisualEffect visualEffect;

    [Tooltip("Name of the exposed Vector3/Position property on the VFX for the player location.")]
    [SerializeField] private string playerProperty = "Player";
    [Tooltip("Name of the exposed Vector3/Position property on the VFX for the acorn location.")]
    [SerializeField] private string acornProperty = "Acorn";
    [Tooltip("Name of the exposed float on the VFX that scales how bold the trail looks. 1 = full strength.")]
    [SerializeField] private string strengthProperty = "Strength";
    [Tooltip("Name of the exposed float on the VFX for how far toward the player the trail reaches. 1 = all the way.")]
    [SerializeField] private string reachProperty = "Reach";
    [Tooltip("Name of the exposed float on the VFX that multiplies the trail colour. Above 1 it goes brighter than white and blooms.")]
    [SerializeField] private string glowProperty = "Glow";

    [Tooltip("Seconds a strength or reach change takes to blend in.")]
    [SerializeField] private float transitionSeconds = 0.75f;

    [Tooltip("Roughly how many seconds the trail's aim lags behind the player. 0 = points straight at them.")]
    [SerializeField] private float followSmoothTime = 0.6f;

    // Set the trail's source acorn and the player it points toward.
    private Transform playerGraphics;
    private Transform acornTransform;

    // Where the trail aims: eases after the player rather than tracking them exactly.
    private Vector3 followPoint;
    private Vector3 followVelocity;

    private int playerPropertyId;
    private int acornPropertyId;
    private int strengthPropertyId;
    private int reachPropertyId;
    private int glowPropertyId;

    // Not blended here: the caller (SenseVisuals, via the manager) already fades it.
    private float glow = 1f;

    private readonly BlendedValue strength = new BlendedValue(1f);
    private readonly BlendedValue reach = new BlendedValue(1f);
    private readonly BlendedValue length = new BlendedValue(0f);

    // True once SetLength is used: reach is then derived from the world length each frame.
    private bool reachFromLength;

    private bool destroyWhenFaded;

    public Acorn Target { get; private set; }

    private void Awake()
    {
        if (visualEffect == null)
        {
            visualEffect = GetComponent<VisualEffect>();
        }

        playerPropertyId = Shader.PropertyToID(playerProperty);
        acornPropertyId = Shader.PropertyToID(acornProperty);
        strengthPropertyId = Shader.PropertyToID(strengthProperty);
        reachPropertyId = Shader.PropertyToID(reachProperty);
        glowPropertyId = Shader.PropertyToID(glowProperty);
    }

    /// <summary>
    /// Configures the trail between the given acorn and the player's graphics.
    /// </summary>
    /// <param name="origin">The player graphics transform.</param>
    /// <param name="acorn">The acorn this trail belongs to.</param>
    /// <param name="initialStrength">Starting boldness, 0–1.</param>
    /// <param name="initialReach">Starting reach: how far toward the player the trail gets, 0–1.</param>
    public void SetEndpoints(Transform origin, Acorn acorn, float initialStrength = 1f, float initialReach = 1f)
    {
        playerGraphics = origin;
        Target = acorn;
        acornTransform = acorn != null ? acorn.transform : null;

        // Start aimed at the player, not easing in from wherever the prefab spawned.
        if (playerGraphics != null)
        {
            followPoint = playerGraphics.position;
            followVelocity = Vector3.zero;
        }

        strength.Snap(initialStrength);
        reach.Snap(Mathf.Clamp01(initialReach));
        reachFromLength = false;

        // The effect prewarms when it starts, which happened on Instantiate — before it knew
        // either endpoint. Push the real values and restart, so the prewarm builds the trail
        // toward the player and it appears already formed instead of growing in.
        if (PushToEffect())
        {
            visualEffect.Reinit();
        }
    }

    /// <summary>Blends the trail's boldness to <paramref name="target"/> (0–1).</summary>
    public void SetStrength(float target) => strength.BlendTo(target, transitionSeconds);

    /// <summary>Blends how far toward the player the trail reaches: 0 = at the acorn, 1 = all the way.</summary>
    public void SetReach(float target)
    {
        reachFromLength = false;
        reach.BlendTo(Mathf.Clamp01(target), transitionSeconds);
    }

    /// <summary>
    /// Blends the trail to <paramref name="worldUnits"/> long, measured from the acorn toward the
    /// player and capped at the distance between them. <paramref name="instant"/> snaps instead and
    /// restarts the effect, so a freshly spawned trail appears already formed at that length.
    /// </summary>
    public void SetLength(float worldUnits, bool instant = false)
    {
        reachFromLength = true;
        worldUnits = Mathf.Max(0f, worldUnits);

        if (!instant)
        {
            length.BlendTo(worldUnits, transitionSeconds);
            return;
        }

        length.Snap(worldUnits);
        if (PushToEffect())
        {
            visualEffect.Reinit();
        }
    }

    /// <summary>Multiplies the trail colour. 1 = authored colour; above 1 is brighter than white.</summary>
    public void SetGlow(float value) => glow = Mathf.Max(0f, value);

    /// <summary>Fades the trail out, then destroys it.</summary>
    public void FadeOutAndDestroy()
    {
        destroyWhenFaded = true;
        SetStrength(0f);
    }

    private void Update()
    {
        // A collected acorn has nothing left to smell.
        if (Target != null && !Target.gameObject.activeInHierarchy && !destroyWhenFaded)
        {
            FadeOutAndDestroy();
        }

        strength.Tick(Time.deltaTime);
        reach.Tick(Time.deltaTime);
        length.Tick(Time.deltaTime);

        if (playerGraphics != null)
        {
            followPoint = followSmoothTime > 0f
                ? Vector3.SmoothDamp(followPoint, playerGraphics.position, ref followVelocity, followSmoothTime)
                : playerGraphics.position;
        }

        PushToEffect();

        if (destroyWhenFaded && strength.Current <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private bool PushToEffect()
    {
        if (playerGraphics == null || acornTransform == null || visualEffect == null)
        {
            return false;
        }

        // The graph spawns particles at this object's position. Acorns carry a Rigidbody2D and
        // can move, so follow the acorn rather than trusting where the trail was spawned.
        transform.position = acornTransform.position;

        if (visualEffect.HasVector3(playerPropertyId))
        {
            visualEffect.SetVector3(playerPropertyId, followPoint);
        }

        if (visualEffect.HasVector3(acornPropertyId))
        {
            visualEffect.SetVector3(acornPropertyId, acornTransform.position);
        }

        if (visualEffect.HasFloat(strengthPropertyId))
        {
            visualEffect.SetFloat(strengthPropertyId, strength.Current);
        }

        if (visualEffect.HasFloat(reachPropertyId))
        {
            visualEffect.SetFloat(reachPropertyId, CurrentReach());
        }

        if (visualEffect.HasFloat(glowPropertyId))
        {
            visualEffect.SetFloat(glowPropertyId, glow);
        }

        return true;
    }

    private float CurrentReach()
    {
        if (!reachFromLength)
        {
            return reach.Current;
        }

        float distance = Vector3.Distance(acornTransform.position, followPoint);
        return distance > 0f ? Mathf.Clamp01(length.Current / distance) : 1f;
    }

    /// <summary>A value that moves linearly to its target over a set time, then stops.</summary>
    private sealed class BlendedValue
    {
        private float target;
        private float unitsPerSecond;

        public BlendedValue(float initial)
        {
            Current = initial;
            target = initial;
        }

        public float Current { get; private set; }

        public void Snap(float value)
        {
            Current = value;
            target = value;
        }

        public void BlendTo(float value, float seconds)
        {
            // Re-asking for the same target must not recompute the rate, or a caller that sets it
            // every refresh would keep slowing the blend down and it would never land.
            if (Mathf.Approximately(value, target)) return;

            target = value;
            unitsPerSecond = seconds > 0f ? Mathf.Abs(target - Current) / seconds : float.PositiveInfinity;
        }

        public void Tick(float deltaTime)
        {
            Current = Mathf.MoveTowards(Current, target, unitsPerSecond * deltaTime);
        }
    }
}
