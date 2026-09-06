using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One depth band's parallax settings: how much its rocks scatter in Z, and the factors the nearest
/// and furthest of them move at. A boundary that selects this tier places its rocks within
/// <see cref="ZJitter"/> and, at runtime, spreads factor across that span — the rock at the minimum Z
/// takes <see cref="minParallaxFactor"/> and slides fastest, the one at the maximum Z takes
/// <see cref="maxParallaxFactor"/> and barely moves.
/// </summary>
[System.Serializable]
public class CaveParallaxTier
{
    [Tooltip("Display name shown in the boundary's parallax-tier dropdown.")]
    public string name = "Tier";

    [Tooltip("Random +/- in world Z given to each rock, so the band has depth rather than one plane. " +
             "This spread is what the factors interpolate across; 0 puts every rock at the midpoint.")]
    [Min(0f)] public float zJitter = 3f;

    [Tooltip("Factor for the nearest rock (minimum Z), the fastest one. More negative reads as " +
             "further in front of the player plane.")]
    [Range(-0.95f, 0.95f)] public float minParallaxFactor = -0.4f;

    [Tooltip("Factor for the furthest rock (maximum Z), the slowest one. Approaching 1 locks to the " +
             "camera and barely moves on screen.")]
    [Range(-0.95f, 0.95f)] public float maxParallaxFactor = 0.1f;

    /// <summary>Half-width of the Z spread; rocks land within +/- this of the boundary's centre.</summary>
    public float ZJitter => Mathf.Max(0f, zJitter);
}

/// <summary>
/// A reusable set of depth bands for cave parallax. Each tier fixes a Z spread and the near/far
/// factors that spread maps to; a <see cref="CaveBoundary"/> references a profile and picks a tier by
/// index. One asset can hold every band the cave uses — "front rocks", "deep rocks" — so the whole
/// cave reads at consistent depths from one place.
///
/// The cameras here are orthographic, so Z produces no size change on its own — it is purely the
/// input to the factor spread, and the depth cue is entirely the resulting motion.
/// </summary>
[CreateAssetMenu(fileName = "CaveParallaxProfile", menuName = "The Last Acorn/Cave Parallax Profile")]
public class CaveParallaxProfile : ScriptableObject
{
    [SerializeField] private List<CaveParallaxTier> tiers = new List<CaveParallaxTier>();

    public List<CaveParallaxTier> Tiers => tiers;

    public CaveParallaxTier GetTier(int index)
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
            CaveParallaxTier tier = tiers[i];
            string label = tier == null || string.IsNullOrEmpty(tier.name) ? $"Tier {i}" : tier.name;
            float jitter = tier != null ? tier.ZJitter : 0f;
            names[i] = $"{label}  (z ±{jitter:0.##})";
        }
        return names;
    }
}
