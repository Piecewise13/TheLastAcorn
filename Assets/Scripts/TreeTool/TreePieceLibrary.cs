using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A named set of tree piece prefabs for one species (e.g. Sycamore, Birch). Each
/// pool is drawn from at random by the Tree Builder while a piece of that role is
/// needed.
/// </summary>
[System.Serializable]
public class TreeSpeciesEntry
{
    [Tooltip("Display name shown in the builder's species dropdown.")]
    public string name = "Species";

    [Tooltip("Ground/root pieces placed at the start of the main stroke (Out socket only).")]
    public List<GameObject> bases = new List<GameObject>();

    [Tooltip("Straight trunk segments chained along the main stroke.")]
    public List<GameObject> trunks = new List<GameObject>();

    [Tooltip("Split pieces that fan one trunk into two limbs and expose a branch point.")]
    public List<GameObject> splits = new List<GameObject>();

    [Tooltip("Limb segments chained along a branch stroke.")]
    public List<GameObject> branches = new List<GameObject>();

    [Tooltip("Canopy / cap pieces that finish a stroke (In socket only).")]
    public List<GameObject> tops = new List<GameObject>();
}

/// <summary>
/// Reusable asset that groups tree piece prefabs by species for the Tree Builder
/// tool. Create via Assets > Create > The Last Acorn > Tree Piece Library.
/// </summary>
[CreateAssetMenu(fileName = "TreePieceLibrary", menuName = "The Last Acorn/Tree Piece Library")]
public class TreePieceLibrary : ScriptableObject
{
    [SerializeField] private List<TreeSpeciesEntry> species = new List<TreeSpeciesEntry>();

    public List<TreeSpeciesEntry> Species => species;

    public string[] GetSpeciesNames()
    {
        string[] names = new string[species.Count];
        for (int i = 0; i < species.Count; i++)
            names[i] = string.IsNullOrEmpty(species[i].name) ? $"Species {i}" : species[i].name;
        return names;
    }

    public TreeSpeciesEntry GetSpecies(int index)
    {
        if (index < 0 || index >= species.Count)
            return null;
        return species[index];
    }
}
