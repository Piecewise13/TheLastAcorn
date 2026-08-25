using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One rock prefab in a set, with the weight controlling how often it gets picked.
/// </summary>
[System.Serializable]
public class CaveRockEntry
{
    public GameObject prefab;

    [Tooltip("Relative chance of being picked. 2 is twice as likely as 1. To drop a rock from the " +
             "pool delete its row rather than zeroing this.")]
    [Min(0.01f)] public float weight = 1f;
}

/// <summary>
/// A named pool of rock prefabs drawn from at random along a boundary, e.g. "Blue Variants".
/// </summary>
[System.Serializable]
public class CaveRockSet
{
    [Tooltip("Display name shown in the boundary's set dropdown.")]
    public string name = "Set";

    public List<CaveRockEntry> rocks = new List<CaveRockEntry>();

    public float TotalWeight
    {
        get
        {
            float total = 0f;
            foreach (CaveRockEntry entry in rocks)
            {
                if (entry != null && entry.prefab != null)
                    total += Mathf.Max(0f, entry.weight);
            }
            return total;
        }
    }
}

/// <summary>
/// A depth band in the cave. Choosing a tier is the single knob that decides how far back a
/// boundary reads: it fixes the parallax factor the boundary's layer should run at, how big the
/// rocks are, and where they sort. The cave's existing bands are Very Front (-0.4),
/// Frontground (-0.2), the player plane (0) and Background (+0.05); negative moves faster than
/// the player and so reads as in front.
/// </summary>
[System.Serializable]
public class CaveDepthTier
{
    [Tooltip("Display name shown in the boundary's tier dropdown.")]
    public string name = "Tier";

    [Tooltip("Parallax factor this tier's layer should run at. Negative reads as in front of the " +
             "player plane, positive as behind. The boundary inspector checks the scene's " +
             "BackgroundParalax against this and offers to fix a mismatch.")]
    public float parallaxFactor = -0.2f;

    [Tooltip("Per-rock spread around the factor, applied by BackgroundParalax itself.")]
    [Min(0f)] public float parallaxVariance = 0.05f;

    [Tooltip("Leave off to keep whatever sorting order each prefab ships with.")]
    public bool overrideSortingOrder = true;

    [Tooltip("Sorting order for the first rock on the boundary. The boundary's Sorting Step " +
             "walks this along the chain.")]
    public int sortingOrder = 3;

    [Tooltip("Optional material forced onto every rock in this tier. Leave empty to keep the " +
             "prefab's own material.")]
    public Material materialOverride;
}

/// <summary>
/// Reusable asset behind the Cave Boundary tool: the rock pools it draws from and the depth
/// tiers it places them in. Create via Assets > Create > The Last Acorn > Cave Rock Library.
/// </summary>
[CreateAssetMenu(fileName = "CaveRockLibrary", menuName = "The Last Acorn/Cave Rock Library")]
public class CaveRockLibrary : ScriptableObject
{
    [Header("Scale")]
    [Tooltip("Every rock this library places gets a local scale rolled between these two, so the " +
             "whole cave stays at one size with a little variation. The cave's hand-placed Blue " +
             "variants sit at 1.5, which is what this range is centred on.")]
    [Min(0.01f)] [SerializeField] private float minScale = 1.4f;

    [Min(0.01f)] [SerializeField] private float maxScale = 1.6f;

    [Header("Content")]
    [SerializeField] private List<CaveRockSet> rockSets = new List<CaveRockSet>();
    [SerializeField] private List<CaveDepthTier> depthTiers = new List<CaveDepthTier>();

    public List<CaveRockSet> RockSets => rockSets;
    public List<CaveDepthTier> DepthTiers => depthTiers;

    public float MinScale => Mathf.Max(0.01f, Mathf.Min(minScale, maxScale));
    public float MaxScale => Mathf.Max(MinScale, maxScale);

    /// <summary>
    /// Rolls one rock's scale. Takes the RNG so scale comes from the boundary's seeded stream and a
    /// rebuild of an unchanged boundary reproduces the same sizes.
    /// </summary>
    public float RollScale(System.Random rng)
    {
        float min = MinScale;
        float max = MaxScale;
        return min + (float)rng.NextDouble() * (max - min);
    }

    private void OnValidate()
    {
        minScale = Mathf.Max(0.01f, minScale);
        maxScale = Mathf.Max(minScale, maxScale);

        // Unity zero-fills a list element added with the inspector's + button, ignoring the C#
        // field initialiser, so a fresh row arrives at weight 0 and would never be picked. Repair
        // it rather than leaving a row that silently contributes nothing.
        foreach (CaveRockSet set in rockSets)
        {
            if (set == null || set.rocks == null)
                continue;

            foreach (CaveRockEntry entry in set.rocks)
            {
                if (entry != null && entry.prefab != null && entry.weight <= 0f)
                    entry.weight = 1f;
            }
        }
    }

    public string[] GetRockSetNames()
    {
        string[] names = new string[rockSets.Count];
        for (int i = 0; i < rockSets.Count; i++)
        {
            names[i] = string.IsNullOrEmpty(rockSets[i].name) ? $"Set {i}" : rockSets[i].name;
        }
        return names;
    }

    public string[] GetDepthTierNames()
    {
        string[] names = new string[depthTiers.Count];
        for (int i = 0; i < depthTiers.Count; i++)
        {
            CaveDepthTier tier = depthTiers[i];
            string label = string.IsNullOrEmpty(tier.name) ? $"Tier {i}" : tier.name;
            names[i] = $"{label}  ({tier.parallaxFactor:0.###})";
        }
        return names;
    }

    public CaveRockSet GetRockSet(int index)
    {
        if (index < 0 || index >= rockSets.Count)
            return null;
        return rockSets[index];
    }

    public CaveDepthTier GetDepthTier(int index)
    {
        if (index < 0 || index >= depthTiers.Count)
            return null;
        return depthTiers[index];
    }

    /// <summary>
    /// Picks a prefab from the set by weight. Takes the RNG so a boundary's whole chain comes
    /// from one seeded stream and regenerating an unchanged boundary reproduces it exactly.
    /// </summary>
    public CaveRockEntry PickRock(int setIndex, System.Random rng)
    {
        CaveRockSet set = GetRockSet(setIndex);
        if (set == null)
            return null;

        float total = set.TotalWeight;
        if (total <= 0f)
            return null;

        float roll = (float)rng.NextDouble() * total;
        foreach (CaveRockEntry entry in set.rocks)
        {
            if (entry == null || entry.prefab == null)
                continue;

            float weight = Mathf.Max(0f, entry.weight);
            if (weight <= 0f)
                continue;

            roll -= weight;
            if (roll <= 0f)
                return entry;
        }

        // Float drift on the last subtraction can leave roll marginally above zero.
        for (int i = set.rocks.Count - 1; i >= 0; i--)
        {
            if (set.rocks[i] != null && set.rocks[i].prefab != null && set.rocks[i].weight > 0f)
                return set.rocks[i];
        }
        return null;
    }
}
