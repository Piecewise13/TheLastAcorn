using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds a <see cref="CaveBoundary"/> automatically so the author never has to press Regenerate:
/// once after any settings or shape change (debounced, so a slider or handle drag rebuilds a single
/// time when released), and once when entering play mode so the run always reflects the latest
/// settings.
///
/// This is editor-only — generation leans on <c>PrefabUtility</c> and <c>Undo</c> and cannot run in a
/// build, which is fine because the rocks a boundary places are saved into the scene. Every rebuild
/// goes through the non-destructive <see cref="CaveBoundaryGenerator.Generate"/> path, so rocks the
/// author moved by hand are kept. Each boundary opts in via its <see cref="CaveBoundary.AutoRegenerate"/>
/// toggle (on by default); turn it off on a boundary whose play-mode rebuild hitch or scene-dirtying
/// is not worth it.
/// </summary>
[InitializeOnLoad]
public static class CaveBoundaryAutoRegenerate
{
    // Boundaries with a rebuild queued for the next editor tick. The set collapses a burst of changes
    // (dragging a slider, nudging several fields) into one rebuild instead of one per event.
    private static readonly HashSet<CaveBoundary> pending = new HashSet<CaveBoundary>();

    static CaveBoundaryAutoRegenerate()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>
    /// Queues one rebuild for <paramref name="boundary"/> on the next editor tick. Safe to call from
    /// every changed event during a drag — the pending set dedupes, so the boundary rebuilds once when
    /// the dust settles rather than mid-drag.
    /// </summary>
    public static void Schedule(CaveBoundary boundary)
    {
        if (boundary == null || !boundary.AutoRegenerate)
            return;

        // The play-mode transition regenerates on its own; don't race it from a stale inspector event.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!pending.Add(boundary))
            return;

        EditorApplication.delayCall += () =>
        {
            pending.Remove(boundary);

            if (boundary == null || !boundary.AutoRegenerate)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            CaveBoundaryGenerator.Generate(boundary, false);
        };
    }

    // Rebuild every opted-in boundary in the open scenes just before play starts, so pressing Play is
    // enough to see current rocks even if a rebuild was somehow skipped.
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        foreach (CaveBoundary boundary in Object.FindObjectsByType<CaveBoundary>(FindObjectsInactive.Include))
        {
            if (boundary != null && boundary.AutoRegenerate)
                CaveBoundaryGenerator.Generate(boundary, false);
        }
    }
}
