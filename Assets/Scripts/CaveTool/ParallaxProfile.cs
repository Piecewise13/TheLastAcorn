using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One depth band's parallax settings: how much its props scatter in Z, and the factors the nearest
/// and furthest of them move at. A ScatterLine that selects this tier places its props within
/// <see cref="ZJitter"/> and, at runtime, spreads factor across that span — the prop at the minimum Z
/// takes <see cref="minParallaxFactor"/> and slides fastest, the one at the maximum Z takes
/// <see cref="maxParallaxFactor"/> and barely moves.
/// </summary>
[System.Serializable]
public class ParallaxTier
{
    [Tooltip("Display name shown in the line's parallax-tier dropdown.")]
    public string name = "Tier";

    [Tooltip("Random +/- in world Z given to each prop, so the band has depth rather than one plane. " +
             "This spread is what the factors interpolate across; 0 puts every prop at the midpoint.")]
    [Min(0f)] public float zJitter = 3f;

    [Tooltip("Factor for the furthest prop (maximum Z), the slowest one. Approaching 1 locks to the " +
             "camera and barely moves on screen.")]
    [Range(-0.95f, 0.95f)] public float maxParallaxFactor = 0.1f;

    [Tooltip("Factor for the nearest prop (minimum Z), the fastest one. More negative reads as " +
             "further in front of the player plane.")]
    [Range(-0.95f, 0.95f)] public float minParallaxFactor = -0.4f;

    /// <summary>Half-width of the Z spread; props land within +/- this of the line's centre.</summary>
    public float ZJitter => Mathf.Max(0f, zJitter);
}

/// <summary>
/// A reusable set of depth bands for ScatterLine parallax. Each tier fixes a Z spread and the
/// near/far factors that spread maps to; a ScatterLine references a profile and picks a tier by
/// index. One asset can hold every band a scene uses — "front rocks", "deep rocks" — so the whole
/// scene reads at consistent depths from one place.
///
/// The cameras here are orthographic, so Z produces no size change on its own — it is purely the
/// input to the factor spread, and the depth cue is entirely the resulting motion.
/// </summary>
[CreateAssetMenu(fileName = "ParallaxProfile", menuName = "The Last Acorn/Parallax Profile")]
public class ParallaxProfile : ScriptableObject
{
    [SerializeField] private List<ParallaxTier> tiers = new List<ParallaxTier>();

    public List<ParallaxTier> Tiers => tiers;

    public ParallaxTier GetTier(int index)
    {
        if (index < 0 || index >= tiers.Count)
            return null;
        return tiers[index];
    }

    public string[] GetTierNames()
    {
        string[] names = new string[tiers.Count];
        for (int i = 0; i < tiers.Count; i++)
        {
            ParallaxTier tier = tiers[i];
            string label = tier == null || string.IsNullOrEmpty(tier.name) ? $"Tier {i}" : tier.name;
            float jitter = tier != null ? tier.ZJitter : 0f;
            names[i] = $"{label}  (z ±{jitter:0.##})";
        }
        return names;
    }
}
