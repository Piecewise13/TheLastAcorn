using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// One prop prefab in a set, with the weight controlling how often it gets picked.
/// </summary>
[System.Serializable]
public class PropEntry
{
    public GameObject prefab;

    [Tooltip("Relative chance of being picked. 2 is twice as likely as 1. To drop a prop from the " +
             "pool delete its row rather than zeroing this.")]
    [Min(0.01f)] public float weight = 1f;
}

/// <summary>
/// A named pool of prop prefabs drawn from at random along a ScatterLine, e.g. "Blue Variants".
/// </summary>
[System.Serializable]
public class PropSet
{
    [Tooltip("Display name shown in the line's set dropdown.")]
    public string name = "Set";

    [FormerlySerializedAs("rocks")]
    public List<PropEntry> props = new List<PropEntry>();

    public float TotalWeight
    {
        get
        {
            float total = 0f;
            foreach (PropEntry entry in props)
            {
                if (entry != null && entry.prefab != null)
                    total += Mathf.Max(0f, entry.weight);
            }
            return total;
        }
    }
}

/// <summary>
/// A material preset for a line's props. Choosing a tier fixes what material the props wear.
/// Sorting lives on the line (one flat order for all its props), and depth/parallax live on the
/// line's <see cref="ParallaxProfile"/>, so a tier carries neither Z, factor, nor sorting.
/// </summary>
[System.Serializable]
public class PropTier
{
    [Tooltip("Display name shown in the line's tier dropdown.")]
    public string name = "Tier";

    [Tooltip("Optional material forced onto every prop in this tier. Leave empty to keep the " +
             "prefab's own material.")]
    public Material materialOverride;
}

/// <summary>
/// Reusable asset behind the ScatterLine tool: the prop pools it draws from and the material
/// tiers it places them in. Create via Assets > Create > The Last Acorn > Prop Library.
/// </summary>
[CreateAssetMenu(fileName = "PropLibrary", menuName = "The Last Acorn/Prop Library")]
public class PropLibrary : ScriptableObject
{
    [Header("Scale")]
    [Tooltip("Every prop this library places gets a local scale rolled between these two, so the " +
             "whole line stays at one size with a little variation. The cave's hand-placed Blue " +
             "variants sit at 1.5, which is what this range is centred on.")]
    [Min(0.01f)] [SerializeField] private float minScale = 1.4f;

    [Min(0.01f)] [SerializeField] private float maxScale = 1.6f;

    [Header("Content")]
    [FormerlySerializedAs("rockSets")]
    [SerializeField] private List<PropSet> propSets = new List<PropSet>();
    [FormerlySerializedAs("depthTiers")]
    [SerializeField] private List<PropTier> tiers = new List<PropTier>();

    public List<PropSet> PropSets => propSets;
    public List<PropTier> Tiers => tiers;

    public float MinScale => Mathf.Max(0.01f, Mathf.Min(minScale, maxScale));
    public float MaxScale => Mathf.Max(MinScale, maxScale);

    /// <summary>
    /// Rolls one prop's scale. Takes the RNG so scale comes from the line's seeded stream and a
    /// rebuild of an unchanged line reproduces the same sizes.
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
        foreach (PropSet set in propSets)
        {
            if (set == null || set.props == null)
                continue;

            foreach (PropEntry entry in set.props)
            {
                if (entry != null && entry.prefab != null && entry.weight <= 0f)
                    entry.weight = 1f;
            }
        }
    }

    public string[] GetPropSetNames()
    {
        string[] names = new string[propSets.Count];
        for (int i = 0; i < propSets.Count; i++)
        {
            names[i] = string.IsNullOrEmpty(propSets[i].name) ? $"Set {i}" : propSets[i].name;
        }
        return names;
    }

    public string[] GetTierNames()
    {
        string[] names = new string[tiers.Count];
        for (int i = 0; i < tiers.Count; i++)
        {
            PropTier tier = tiers[i];
            string label = string.IsNullOrEmpty(tier.name) ? $"Tier {i}" : tier.name;
            string material = tier.materialOverride != null ? tier.materialOverride.name : "prefab material";
            names[i] = $"{label}  ({material})";
        }
        return names;
    }

    public PropSet GetPropSet(int index)
    {
        if (index < 0 || index >= propSets.Count)
            return null;
        return propSets[index];
    }

    public PropTier GetTier(int index)
    {
        if (index < 0 || index >= tiers.Count)
            return null;
        return tiers[index];
    }

    /// <summary>
    /// Picks a prefab from the set by weight. Takes the RNG so a line's whole chain comes
    /// from one seeded stream and regenerating an unchanged line reproduces it exactly.
    /// </summary>
    public PropEntry PickProp(int setIndex, System.Random rng)
    {
        PropSet set = GetPropSet(setIndex);
        if (set == null)
            return null;

        float total = set.TotalWeight;
        if (total <= 0f)
            return null;

        float roll = (float)rng.NextDouble() * total;
        foreach (PropEntry entry in set.props)
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
        for (int i = set.props.Count - 1; i >= 0; i--)
        {
            if (set.props[i] != null && set.props[i].prefab != null && set.props[i].weight > 0f)
                return set.props[i];
        }
        return null;
    }
}
