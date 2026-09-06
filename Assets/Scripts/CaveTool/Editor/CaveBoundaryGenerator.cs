using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates, rebuilds and clears the rocks along a <see cref="CaveBoundary"/>. Parallax is no longer
/// its concern — the boundary drives each rock's motion itself from the rock's Z at runtime.
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

        // AddComponent from script skips Reset, so a boundary can still reach here unseeded. Seed it
        // now rather than letting every such boundary share seed 0's arrangement.
        if (!boundary.HasSeed)
        {
            Undo.RecordObject(boundary, "Seed Cave Boundary");
            boundary.Seed = CaveBoundary.NewSeed();
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

        // Put the pivot in the middle of what was just placed, so the boundary's own transform sits
        // at the centre of the rock cluster rather than wherever the line happened to start.
        CenterPivotOnRocks(boundary);

        EditorUtility.SetDirty(boundary);
        Undo.CollapseUndoOperations(group);

        if (placements.Count >= CaveBoundary.MaxRocks)
        {
            Debug.LogWarning($"Cave Boundary: hit the {CaveBoundary.MaxRocks} rock cap and stopped " +
                             "early. Lower the overlap or shorten the boundary.", boundary);
        }
    }

    /// <summary>
    /// Empties the boundary: everything under the rock parent goes, recorded or not. Rocks moved by
    /// hand, rocks whose record was lost to an undo, rocks dropped in by hand — all of it. The
    /// non-destructive path is Regenerate; Clear means clear. Undoable in one step.
    /// </summary>
    public static void Clear(CaveBoundary boundary)
    {
        if (boundary == null)
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Clear Cave Boundary");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(boundary, "Clear Cave Boundary");

        // Snapshot the children first: destroying them walks the child list out from under us.
        Transform parent = boundary.RockParent;
        var children = new Transform[parent.childCount];
        for (int i = 0; i < children.Length; i++)
            children[i] = parent.GetChild(i);

        foreach (Transform child in children)
        {
            if (child != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        // A recorded rock the author dragged out of the boundary is still this tool's to clean up.
        foreach (CaveGeneratedRock record in boundary.Generated)
        {
            if (record != null && record.instance != null)
                Undo.DestroyObjectImmediate(record.instance);
        }

        boundary.Generated.Clear();

        EditorUtility.SetDirty(boundary);
        Undo.CollapseUndoOperations(group);
    }

    /// <summary>
    /// Moves the boundary's transform to the centre of the combined bounding box of every rock it
    /// has placed, without moving the rocks or the drawn line in the world. The rocks are children,
    /// so shifting the pivot would drag them; their world poses and the local <c>points</c> are
    /// cached and restored so only the pivot moves.
    ///
    /// This keeps the boundary's own transform in the middle of what it drew, which reads more
    /// naturally in the hierarchy and gizmos than a pivot stranded at the start of the rock line.
    /// </summary>
    public static void CenterPivotOnRocks(CaveBoundary boundary)
    {
        if (boundary == null)
            return;

        if (!TryGetRockBounds(boundary, out Bounds bounds))
            return;

        Transform t = boundary.transform;
        var target = new Vector3(bounds.center.x, bounds.center.y, t.position.z);
        if ((target - t.position).sqrMagnitude < 1e-8f)
            return;

        Undo.RecordObject(t, "Center Cave Boundary Pivot");
        Undo.RecordObject(boundary, "Center Cave Boundary Pivot");

        // Cache child world poses so the rocks hold still when the pivot moves out from under them.
        int childCount = t.childCount;
        var children = new Transform[childCount];
        var childWorldPos = new Vector3[childCount];
        var childWorldRot = new Quaternion[childCount];
        for (int i = 0; i < childCount; i++)
        {
            children[i] = t.GetChild(i);
            Undo.RecordObject(children[i], "Center Cave Boundary Pivot");
            childWorldPos[i] = children[i].position;
            childWorldRot[i] = children[i].rotation;
        }

        // The drawn line is stored in local space, so it would slide with the pivot too.
        var worldPoints = new Vector3[boundary.Points.Count];
        for (int i = 0; i < worldPoints.Length; i++)
            worldPoints[i] = boundary.GetWorldPoint(i);

        t.position = target;

        for (int i = 0; i < childCount; i++)
        {
            children[i].position = childWorldPos[i];
            children[i].rotation = childWorldRot[i];
        }

        for (int i = 0; i < worldPoints.Length; i++)
            boundary.SetWorldPoint(i, worldPoints[i]);

        // The recorded local poses drive non-destructive rebuilds; refresh them to the new local
        // space or every rock would read as hand-moved on the next Regenerate.
        foreach (CaveGeneratedRock record in boundary.Generated)
        {
            if (record == null || record.instance == null)
                continue;

            Transform rt = record.instance.transform;
            record.localPosition = rt.localPosition;
            record.localRotation = rt.localRotation;
            record.localScale = rt.localScale;
        }
    }

    /// <summary>World-space bounds of every alive rock renderer under the boundary.</summary>
    private static bool TryGetRockBounds(CaveBoundary boundary, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (CaveGeneratedRock record in boundary.Generated)
        {
            if (record == null || record.instance == null)
                continue;

            foreach (Renderer renderer in record.instance.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null)
                    continue;

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return any;
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
            // Every rock on the boundary takes the boundary's one sorting order.
            renderer.sortingOrder = sortingOrder;

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

    /// <summary>
    /// How many objects a Clear would destroy. Everything under the parent counts, whether the tool
    /// placed it or not, plus any recorded rock that has since been dragged out of the boundary.
    /// </summary>
    public static int CountClearable(CaveBoundary boundary)
    {
        if (boundary == null)
            return 0;

        Transform parent = boundary.RockParent;
        int count = parent.childCount;

        foreach (CaveGeneratedRock record in boundary.Generated)
        {
            if (record == null || record.instance == null)
                continue;

            if (!record.instance.transform.IsChildOf(parent))
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
}
