using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What the scene's <see cref="ParalaxManager"/> actually says about a boundary's rock parent,
/// checked against the depth tier the boundary claims to be in. A boundary can look right in the
/// editor and still not parallax at all — that is the state of the cave's TotemSection today — so
/// the tool reports this rather than assuming the wiring is there.
/// </summary>
public struct CaveParallaxStatus
{
    public ParalaxManager component;
    public int layerIndex;
    public Transform layerRoot;
    public bool hasLayer;
    public bool nested;
    public bool recurseIntoChildren;
    public float actualFactor;
    public float actualVariance;
    public int duplicateCount;

    public bool FactorMatches(CaveDepthTier tier) => tier != null && Mathf.Approximately(actualFactor, tier.parallaxFactor);
    public bool VarianceMatches(CaveDepthTier tier) => tier != null && Mathf.Approximately(actualVariance, tier.parallaxVariance);

    /// <summary>Rocks sitting below the layer root only move if the layer walks the whole subtree.</summary>
    public bool NeedsRecurse => hasLayer && nested && !recurseIntoChildren;
}

/// <summary>
/// Creates, rebuilds and clears the rocks along a <see cref="CaveBoundary"/>, and keeps the
/// boundary's depth tier honest against the scene's parallax layers.
///
/// Rebuilding is non-destructive: a rock whose pose still matches what the tool recorded when it
/// placed it gets replaced, and a rock the author has since nudged is left where they put it.
/// </summary>
public static class CaveBoundaryGenerator
{
    private const float PositionEpsilon = 1e-3f;
    private const float RotationEpsilon = 0.01f;
    private const float ScaleEpsilon = 1e-3f;

    // -------------------------------------------------------------------------
    // Generation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the boundary's rocks. Hand-moved rocks survive unless
    /// <paramref name="discardTweaks"/> is set.
    /// </summary>
    public static void Generate(CaveBoundary boundary, bool discardTweaks)
    {
        if (boundary == null)
            return;

        CaveDepthTier tier = boundary.Tier;
        if (boundary.Library == null || tier == null)
        {
            Debug.LogWarning("Cave Boundary: assign a Cave Rock Library with at least one depth tier.", boundary);
            return;
        }

        List<CaveRockPlacement> placements = boundary.BuildPlacements();
        if (placements.Count == 0)
        {
            Debug.LogWarning("Cave Boundary: nothing to place. Check the boundary has two or more " +
                             "points and the chosen rock set has prefabs.", boundary);
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Generate Cave Boundary");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(boundary, "Generate Cave Boundary");

        var kept = new List<CaveGeneratedRock>();
        foreach (CaveGeneratedRock record in boundary.Generated)
        {
            if (record == null || record.instance == null)
                continue;

            if (!discardTweaks && IsDetached(record))
            {
                kept.Add(record);
                continue;
            }

            Undo.DestroyObjectImmediate(record.instance);
        }

        boundary.Generated.Clear();
        boundary.Generated.AddRange(kept);

        Transform parent = boundary.RockParent;
        foreach (CaveRockPlacement placement in placements)
        {
            CaveGeneratedRock record = Place(placement, parent, tier);
            if (record != null)
                boundary.Generated.Add(record);
        }

        EditorUtility.SetDirty(boundary);
        Undo.CollapseUndoOperations(group);

        if (placements.Count >= CaveBoundary.MaxRocks)
        {
            Debug.LogWarning($"Cave Boundary: hit the {CaveBoundary.MaxRocks} rock cap and stopped " +
                             "early. Lower the overlap or shorten the boundary.", boundary);
        }
    }

    public static void Clear(CaveBoundary boundary)
    {
        if (boundary == null)
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Clear Cave Boundary");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(boundary, "Clear Cave Boundary");
        foreach (CaveGeneratedRock record in boundary.Generated)
        {
            if (record != null && record.instance != null)
                Undo.DestroyObjectImmediate(record.instance);
        }
        boundary.Generated.Clear();

        EditorUtility.SetDirty(boundary);
        Undo.CollapseUndoOperations(group);
    }

    private static CaveGeneratedRock Place(CaveRockPlacement placement, Transform parent, CaveDepthTier tier)
    {
        if (placement.entry == null || placement.entry.prefab == null)
            return null;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(placement.entry.prefab);
        if (instance == null)
            return null;

        Undo.RegisterCreatedObjectUndo(instance, "Generate Cave Boundary");

        Transform t = instance.transform;
        t.SetParent(parent, false);
        t.position = placement.worldPosition;
        t.rotation = placement.worldRotation;
        t.localScale = placement.localScale;

        int sortingOrder = placement.sortingOrder;
        SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            if (tier.overrideSortingOrder)
                renderer.sortingOrder = sortingOrder;
            else
                sortingOrder = renderer.sortingOrder;

            if (tier.materialOverride != null)
                renderer.sharedMaterial = tier.materialOverride;
        }

        return new CaveGeneratedRock
        {
            instance = instance,
            localPosition = t.localPosition,
            localRotation = t.localRotation,
            localScale = t.localScale,
            sortingOrder = sortingOrder
        };
    }

    /// <summary>
    /// True when the rock no longer sits where the tool put it, i.e. the author has adjusted it and
    /// a rebuild should leave it alone.
    /// </summary>
    private static bool IsDetached(CaveGeneratedRock record)
    {
        Transform t = record.instance.transform;

        if (Vector3.Distance(t.localPosition, record.localPosition) > PositionEpsilon)
            return true;
        if (Quaternion.Angle(t.localRotation, record.localRotation) > RotationEpsilon)
            return true;
        if (Vector3.Distance(t.localScale, record.localScale) > ScaleEpsilon)
            return true;

        SpriteRenderer renderer = record.instance.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null && renderer.sortingOrder != record.sortingOrder)
            return true;

        return false;
    }

    public static int CountDetached(CaveBoundary boundary)
    {
        int count = 0;
        foreach (CaveGeneratedRock record in boundary.Generated)
        {
            if (record != null && record.instance != null && IsDetached(record))
                count++;
        }
        return count;
    }

    public static int CountAlive(CaveBoundary boundary)
    {
        int count = 0;
        foreach (CaveGeneratedRock record in boundary.Generated)
        {
            if (record != null && record.instance != null)
                count++;
        }
        return count;
    }

    // -------------------------------------------------------------------------
    // Parallax wiring
    // -------------------------------------------------------------------------

    /// <summary>
    /// Looks for the layer that will actually move this boundary's rocks. Reads the private layer
    /// array through SerializedObject so BackgroundParalax keeps its own encapsulation.
    /// </summary>
    public static CaveParallaxStatus InspectParallax(CaveBoundary boundary)
    {
        var status = new CaveParallaxStatus();
        if (boundary == null)
            return status;

        Transform rockParent = boundary.RockParent;
        ParalaxManager[] components = Object.FindObjectsByType<ParalaxManager>(FindObjectsSortMode.None);

        foreach (ParalaxManager component in components)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty layers = serialized.FindProperty("layers");
            if (layers == null || !layers.isArray)
                continue;

            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                var root = element.FindPropertyRelative("layerRoot").objectReferenceValue as Transform;
                if (root == null)
                    continue;

                bool isSelf = root == rockParent;
                bool isAncestor = !isSelf && rockParent.IsChildOf(root);
                if (!isSelf && !isAncestor)
                    continue;

                if (status.hasLayer)
                {
                    // The cave already has SmallCave registered twice, which doubles its movement.
                    // Surface a repeat rather than silently reporting only the first match.
                    status.duplicateCount++;
                    continue;
                }

                status.component = component;
                status.layerIndex = i;
                status.layerRoot = root;
                status.hasLayer = true;
                status.nested = isAncestor;
                status.recurseIntoChildren = element.FindPropertyRelative("recurseIntoChildren").boolValue;
                status.actualFactor = element.FindPropertyRelative("baseParallaxFactor").floatValue;
                status.actualVariance = element.FindPropertyRelative("variance").floatValue;
            }
        }

        return status;
    }

    /// <summary>
    /// Adds the boundary's rock parent as a new parallax layer running at its tier's factor.
    /// </summary>
    public static void RegisterLayer(CaveBoundary boundary, ParalaxManager component)
    {
        CaveDepthTier tier = boundary.Tier;
        if (tier == null || component == null)
            return;

        var serialized = new SerializedObject(component);
        SerializedProperty layers = serialized.FindProperty("layers");
        if (layers == null)
            return;

        int index = layers.arraySize;
        layers.InsertArrayElementAtIndex(index);

        SerializedProperty element = layers.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("layerRoot").objectReferenceValue = boundary.RockParent;
        element.FindPropertyRelative("baseParallaxFactor").floatValue = tier.parallaxFactor;
        element.FindPropertyRelative("variance").floatValue = tier.parallaxVariance;
        element.FindPropertyRelative("recurseIntoChildren").boolValue = false;
        element.FindPropertyRelative("includeInactiveChildren").boolValue = false;
        element.FindPropertyRelative("seedSalt").intValue = 0;

        serialized.ApplyModifiedProperties();
    }

    /// <summary>Pulls the existing layer's factor and variance back in line with the tier.</summary>
    public static void ApplyTierToLayer(CaveBoundary boundary, CaveParallaxStatus status)
    {
        CaveDepthTier tier = boundary.Tier;
        if (tier == null || !status.hasLayer || status.component == null)
            return;

        var serialized = new SerializedObject(status.component);
        SerializedProperty element = serialized.FindProperty("layers").GetArrayElementAtIndex(status.layerIndex);
        element.FindPropertyRelative("baseParallaxFactor").floatValue = tier.parallaxFactor;
        element.FindPropertyRelative("variance").floatValue = tier.parallaxVariance;
        serialized.ApplyModifiedProperties();
    }

    /// <summary>Turns on subtree walking so rocks nested below the layer root actually move.</summary>
    public static void EnableRecurse(CaveParallaxStatus status)
    {
        if (!status.hasLayer || status.component == null)
            return;

        var serialized = new SerializedObject(status.component);
        SerializedProperty element = serialized.FindProperty("layers").GetArrayElementAtIndex(status.layerIndex);
        element.FindPropertyRelative("recurseIntoChildren").boolValue = true;
        serialized.ApplyModifiedProperties();
    }

    /// <summary>The parallax component a new layer should be added to: the one already doing the work.</summary>
    public static ParalaxManager FindBestParallaxHost()
    {
        ParalaxManager[] components = Object.FindObjectsByType<ParalaxManager>(FindObjectsSortMode.None);
        ParalaxManager best = null;
        int bestCount = -1;

        foreach (ParalaxManager component in components)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty layers = serialized.FindProperty("layers");
            int count = layers != null && layers.isArray ? layers.arraySize : 0;
            if (count > bestCount)
            {
                bestCount = count;
                best = component;
            }
        }

        return best;
    }
}
