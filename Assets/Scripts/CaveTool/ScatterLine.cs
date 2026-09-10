using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The graded ScatterLine: one prop pool drawn along the whole line, scattered in Z, with parallax
/// speed graded across that Z spread — the nearest prop slides fastest, the furthest barely moves.
/// This is how the cave's blue rocks and cliff rocks work, and the default for a line that reads as a
/// single band of depth. For discrete per-art-layer bands (tree tops), use LayeredScatterLine.
///
/// Existing cave/cliff components deserialize onto this type, so their look is unchanged.
/// </summary>
public class ScatterLine : ScatterLineBase
{
    [Header("Graded content")]
    [SerializeField] private int rockSetIndex;
    [SerializeField] private int depthTierIndex;

    [Tooltip("The set of depth bands this line can read at. Shared asset — drop the same one on every " +
             "line and pick a band per line. Leave empty and the props hold still.")]
    [SerializeField] private ParallaxProfile parallaxProfile;

    [Tooltip("Which band in the profile this line uses — its Z spread and near/far factors.")]
    [SerializeField] private int parallaxTierIndex;

    public int PropSetIndex => rockSetIndex;
    public int TierIndex => depthTierIndex;

    /// <summary>The material tier this line draws its props in, or null if unset.</summary>
    public PropTier Tier => library != null ? library.GetTier(depthTierIndex) : null;

    /// <summary>The parallax profile this line reads its depth band from.</summary>
    public ParallaxProfile ParallaxProfile => parallaxProfile;

    /// <summary>The selected depth band — its Z spread and near/far factors — or null if unset.</summary>
    public ParallaxTier ParallaxTier => parallaxProfile != null ? parallaxProfile.GetTier(parallaxTierIndex) : null;

    public override bool ValidateForGenerate(out string message)
    {
        if (library == null)
        {
            message = "ScatterLine: assign a Prop Library.";
            return false;
        }

        if (Tier == null)
        {
            message = "ScatterLine: the Prop Library has no material tier — add one on the library asset.";
            return false;
        }

        message = null;
        return true;
    }

    /// <summary>One walk of the whole line from a single set, jittered in Z by the selected band.</summary>
    public override List<PropPlacement> BuildPlacements()
    {
        var placements = new List<PropPlacement>();

        PropTier tier = Tier;
        if (library == null || tier == null || points.Count < 2)
            return placements;

        ParallaxTier band = ParallaxTier;
        float zJitter = band != null ? band.ZJitter : 0f;

        // Factor is Z-derived at runtime (NaN baked factor), so it spreads across the actual Z span.
        AppendScatter(placements, rockSetIndex, tier.materialOverride, zJitter, float.NaN,
                      sortingOrder, new System.Random(seed));
        return placements;
    }

    /// <summary>
    /// Caches every placed prop with its anchor and a factor interpolated across this line's own Z
    /// span: the nearest prop (minimum Z) takes the band's min factor and slides fastest, the furthest
    /// (maximum Z) takes its max factor and barely moves. A missing profile/band leaves every factor
    /// at 0, so the props hold still rather than throwing.
    /// </summary>
    protected override void BuildRuntimeProps()
    {
        var transforms = new List<Transform>(generated.Count);
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        foreach (GeneratedProp record in generated)
        {
            if (record == null || record.instance == null)
                continue;

            Transform t = record.instance.transform;
            float z = t.position.z;
            transforms.Add(t);

            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }

        ParallaxTier band = ParallaxTier;
        float minFactor = band != null ? band.minParallaxFactor : 0f;
        float maxFactor = band != null ? band.maxParallaxFactor : 0f;

        // A line with no Z spread (jitter off) has no near/far to interpolate, so put every prop
        // halfway between the two factors rather than dividing by zero.
        bool flat = maxZ - minZ < 1e-4f;

        var built = new PropParallax[transforms.Count];
        for (int i = 0; i < transforms.Count; i++)
        {
            Vector3 world = transforms[i].position;
            float t = flat ? 0.5f : Mathf.InverseLerp(minZ, maxZ, world.z);

            built[i] = new PropParallax
            {
                transform = transforms[i],
                anchor = new Vector2(world.x, world.y),
                factor = Mathf.Lerp(minFactor, maxFactor, t)
            };
        }

        runtimeProps = built;
    }
}
