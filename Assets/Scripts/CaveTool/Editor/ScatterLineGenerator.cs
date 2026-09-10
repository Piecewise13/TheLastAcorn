using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates, rebuilds and clears the props along a <see cref="ScatterLineBase"/>. Parallax is not its
/// concern — the line drives each prop's motion itself at runtime. Works for any variant: it asks the
/// line to validate itself and to build placements, then instantiates them, so the graded and layered
/// lines share one generator.
///
/// Rebuilding is non-destructive: a prop whose pose still matches what the tool recorded when it
/// placed it gets replaced, and a prop the author has since nudged is left where they put it.
/// </summary>
public static class ScatterLineGenerator
{
    private const float PositionEpsilon = 1e-3f;
    private const float RotationEpsilon = 0.01f;
    private const float ScaleEpsilon = 1e-3f;

    // -------------------------------------------------------------------------
    // Generation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the line's props. Hand-moved props survive unless <paramref name="discardTweaks"/>
    /// is set.
    /// </summary>
    public static void Generate(ScatterLineBase line, bool discardTweaks)
    {
        if (line == null)
            return;

        if (!line.ValidateForGenerate(out string warning))
        {
            Debug.LogWarning(warning, line);
            return;
        }

        // AddComponent from script skips Reset, so a line can still reach here unseeded. Seed it now
        // rather than letting every such line share seed 0's arrangement.
        if (!line.HasSeed)
        {
            Undo.RecordObject(line, "Seed Scatter Line");
            line.Seed = ScatterLineBase.NewSeed();
        }

        List<PropPlacement> placements = line.BuildPlacements();
        if (placements.Count == 0)
        {
            Debug.LogWarning("Scatter Line: nothing to place. Check the line has two or more points " +
                             "and the chosen prop set has prefabs.", line);
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Generate Scatter Line");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(line, "Generate Scatter Line");

        var kept = new List<GeneratedProp>();
        foreach (GeneratedProp record in line.Generated)
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

        line.Generated.Clear();
        line.Generated.AddRange(kept);

        Transform parent = line.PropParent;
        foreach (PropPlacement placement in placements)
        {
            GeneratedProp record = Place(placement, parent);
            if (record != null)
                line.Generated.Add(record);
        }

        // Put the pivot in the middle of what was just placed, so the line's own transform sits at
        // the centre of the prop cluster rather than wherever the line happened to start.
        CenterPivotOnProps(line);

        EditorUtility.SetDirty(line);
        Undo.CollapseUndoOperations(group);

        if (placements.Count >= ScatterLineBase.MaxProps)
        {
            Debug.LogWarning($"Scatter Line: hit the {ScatterLineBase.MaxProps} prop cap and stopped " +
                             "early. Lower the overlap or shorten the line.", line);
        }
    }

    /// <summary>
    /// Empties the line: everything under the prop parent goes, recorded or not. Props moved by hand,
    /// props whose record was lost to an undo, props dropped in by hand — all of it. The
    /// non-destructive path is Regenerate; Clear means clear. Undoable in one step.
    /// </summary>
    public static void Clear(ScatterLineBase line)
    {
        if (line == null)
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Clear Scatter Line");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(line, "Clear Scatter Line");

        // Snapshot the children first: destroying them walks the child list out from under us.
        Transform parent = line.PropParent;
        var children = new Transform[parent.childCount];
        for (int i = 0; i < children.Length; i++)
            children[i] = parent.GetChild(i);

        foreach (Transform child in children)
        {
            if (child != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        // A recorded prop the author dragged out of the line is still this tool's to clean up.
        foreach (GeneratedProp record in line.Generated)
        {
            if (record != null && record.instance != null)
                Undo.DestroyObjectImmediate(record.instance);
        }

        line.Generated.Clear();

        EditorUtility.SetDirty(line);
        Undo.CollapseUndoOperations(group);
    }

    /// <summary>
    /// Moves the line's transform to the centre of the combined bounding box of every prop it has
    /// placed, without moving the props or the drawn line in the world. The props are children, so
    /// shifting the pivot would drag them; their world poses and the local <c>points</c> are cached
    /// and restored so only the pivot moves.
    /// </summary>
    public static void CenterPivotOnProps(ScatterLineBase line)
    {
        if (line == null)
            return;

        if (!TryGetPropBounds(line, out Bounds bounds))
            return;

        Transform t = line.transform;
        var target = new Vector3(bounds.center.x, bounds.center.y, t.position.z);
        if ((target - t.position).sqrMagnitude < 1e-8f)
            return;

        Undo.RecordObject(t, "Center Scatter Line Pivot");
        Undo.RecordObject(line, "Center Scatter Line Pivot");

        // Cache child world poses so the props hold still when the pivot moves out from under them.
        int childCount = t.childCount;
        var children = new Transform[childCount];
        var childWorldPos = new Vector3[childCount];
        var childWorldRot = new Quaternion[childCount];
        for (int i = 0; i < childCount; i++)
        {
            children[i] = t.GetChild(i);
            Undo.RecordObject(children[i], "Center Scatter Line Pivot");
            childWorldPos[i] = children[i].position;
            childWorldRot[i] = children[i].rotation;
        }

        // The drawn line is stored in local space, so it would slide with the pivot too.
        var worldPoints = new Vector3[line.Points.Count];
        for (int i = 0; i < worldPoints.Length; i++)
            worldPoints[i] = line.GetWorldPoint(i);

        t.position = target;

        for (int i = 0; i < childCount; i++)
        {
            children[i].position = childWorldPos[i];
            children[i].rotation = childWorldRot[i];
        }

        for (int i = 0; i < worldPoints.Length; i++)
            line.SetWorldPoint(i, worldPoints[i]);

        // The recorded local poses drive non-destructive rebuilds; refresh them to the new local
        // space or every prop would read as hand-moved on the next Regenerate.
        foreach (GeneratedProp record in line.Generated)
        {
            if (record == null || record.instance == null)
                continue;

            Transform rt = record.instance.transform;
            record.localPosition = rt.localPosition;
            record.localRotation = rt.localRotation;
            record.localScale = rt.localScale;
        }
    }

    /// <summary>World-space bounds of every alive prop renderer under the line.</summary>
    private static bool TryGetPropBounds(ScatterLineBase line, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (GeneratedProp record in line.Generated)
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

    private static GeneratedProp Place(PropPlacement placement, Transform parent)
    {
        if (placement.entry == null || placement.entry.prefab == null)
            return null;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(placement.entry.prefab);
        if (instance == null)
            return null;

        Undo.RegisterCreatedObjectUndo(instance, "Generate Scatter Line");

        Transform t = instance.transform;
        t.SetParent(parent, false);
        t.position = placement.worldPosition;
        t.rotation = placement.worldRotation;
        t.localScale = placement.localScale;

        int sortingOrder = placement.sortingOrder;
        SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;

            if (placement.materialOverride != null)
                renderer.sharedMaterial = placement.materialOverride;
        }

        return new GeneratedProp
        {
            instance = instance,
            localPosition = t.localPosition,
            localRotation = t.localRotation,
            localScale = t.localScale,
            sortingOrder = sortingOrder,
            parallaxFactor = placement.parallaxFactor
        };
    }

    /// <summary>
    /// True when the prop no longer sits where the tool put it, i.e. the author has adjusted it and a
    /// rebuild should leave it alone.
    /// </summary>
    private static bool IsDetached(GeneratedProp record)
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

    public static int CountDetached(ScatterLineBase line)
    {
        int count = 0;
        foreach (GeneratedProp record in line.Generated)
        {
            if (record != null && record.instance != null && IsDetached(record))
                count++;
        }
        return count;
    }

    /// <summary>
    /// How many objects a Clear would destroy. Everything under the parent counts, whether the tool
    /// placed it or not, plus any recorded prop that has since been dragged out of the line.
    /// </summary>
    public static int CountClearable(ScatterLineBase line)
    {
        if (line == null)
            return 0;

        Transform parent = line.PropParent;
        int count = parent.childCount;

        foreach (GeneratedProp record in line.Generated)
        {
            if (record == null || record.instance == null)
                continue;

            if (!record.instance.transform.IsChildOf(parent))
                count++;
        }

        return count;
    }

    public static int CountAlive(ScatterLineBase line)
    {
        int count = 0;
        foreach (GeneratedProp record in line.Generated)
        {
            if (record != null && record.instance != null)
                count++;
        }
        return count;
    }
}
