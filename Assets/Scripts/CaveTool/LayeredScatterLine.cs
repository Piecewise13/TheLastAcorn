using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One depth layer of a <see cref="LayeredScatterLine"/>: a prop set drawn along the whole line at a
/// fixed parallax speed. Stacking several — a dark far layer, a mid layer, a light near layer — is
/// what gives tree tops their read of depth, with each art tier pinned to its own speed rather than
/// left to random Z. Ordered back-to-front by <see cref="parallaxFactor"/>; the sorting offset keeps
/// the render order matching.
/// </summary>
[System.Serializable]
public class ScatterLayer
{
    [Tooltip("Display name, e.g. 'Dark', 'Mid', 'Light'. Cosmetic.")]
    public string name = "Layer";

    [Tooltip("Which prop set in the library this layer draws from.")]
    public int propSetIndex;

    [Tooltip("Which material tier in the library this layer's props wear.")]
    public int materialTierIndex;

    [Tooltip("The parallax speed for the whole layer. Positive tracks the camera and reads as far " +
             "back; negative overshoots and reads as in front of the player; 0 holds still on its spot.")]
    [Range(-0.95f, 0.95f)] public float parallaxFactor;

    [Tooltip("Added to the line's sorting order for this layer, so a slower far layer can draw behind " +
             "a faster near one. Back layers want a lower (more negative) offset.")]
    public int sortingOffset;
}

/// <summary>
/// The layered ScatterLine: several <see cref="ScatterLayer"/>s drawn along one shape, each its own
/// prop set at its own fixed parallax speed. Use this when the art is authored as discrete depth
/// tiers (tree tops: dark / mid / light) and you want each pinned to a speed, rather than the graded
/// <see cref="ScatterLine"/>'s single pool spread across a Z range.
///
/// The whole stack shares the line's one seed, so a rebuild is reproducible; layers walk in order.
/// </summary>
public class LayeredScatterLine : ScatterLineBase
{
    [Header("Layers")]
    [Tooltip("Back-to-front. Each layer draws the full line from its own set at its own speed.")]
    [SerializeField] private List<ScatterLayer> layers = new List<ScatterLayer>();

    public List<ScatterLayer> Layers => layers;

    private bool HasUsableLayer()
    {
        if (library == null || layers == null)
            return false;

        foreach (ScatterLayer layer in layers)
        {
            if (layer != null && library.GetPropSet(layer.propSetIndex) != null)
                return true;
        }
        return false;
    }

    public override bool ValidateForGenerate(out string message)
    {
        if (library == null)
        {
            message = "Layered Scatter Line: assign a Prop Library.";
            return false;
        }

        if (!HasUsableLayer())
        {
            message = "Layered Scatter Line: add at least one layer with a valid prop set.";
            return false;
        }

        message = null;
        return true;
    }

    /// <summary>Walks the line once per layer, baking each layer's fixed factor onto its props.</summary>
    public override List<PropPlacement> BuildPlacements()
    {
        var placements = new List<PropPlacement>();
        if (library == null || layers == null || points.Count < 2)
            return placements;

        // One shared stream across all layers so the whole stack rebuilds identically.
        var rng = new System.Random(seed);

        foreach (ScatterLayer layer in layers)
        {
            if (layer == null || library.GetPropSet(layer.propSetIndex) == null)
                continue;

            PropTier tier = library.GetTier(layer.materialTierIndex);
            Material material = tier != null ? tier.materialOverride : null;

            // Layered props take a fixed per-layer factor (no Z spread), so zJitter is 0 and the
            // baked factor is the layer's own. Sorting offsets keep near layers drawing over far ones.
            AppendScatter(placements, layer.propSetIndex, material, 0f, layer.parallaxFactor,
                          sortingOrder + layer.sortingOffset, rng);
        }

        return placements;
    }

    /// <summary>
    /// Each prop reads the factor baked onto it at generation time (its layer's speed), anchored to
    /// where it was placed. A prop with no baked factor (NaN) holds still.
    /// </summary>
    protected override void BuildRuntimeProps()
    {
        var built = new List<PropParallax>(generated.Count);

        foreach (GeneratedProp record in generated)
        {
            if (record == null || record.instance == null)
                continue;

            Vector3 world = record.instance.transform.position;
            float factor = float.IsNaN(record.parallaxFactor) ? 0f : record.parallaxFactor;

            built.Add(new PropParallax
            {
                transform = record.instance.transform,
                anchor = new Vector2(world.x, world.y),
                factor = factor
            });
        }

        runtimeProps = built.ToArray();
    }
}
