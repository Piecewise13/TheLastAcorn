using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds a <see cref="ScatterLineBase"/> automatically so the author never has to press Regenerate:
/// once after any settings or shape change (debounced, so a slider or handle drag rebuilds a single
/// time when released), and once when entering play mode so the run always reflects the latest
/// settings.
///
/// This is editor-only — generation leans on <c>PrefabUtility</c> and <c>Undo</c> and cannot run in a
/// build, which is fine because the props a line places are saved into the scene. Every rebuild goes
/// through the non-destructive <see cref="ScatterLineGenerator.Generate"/> path, so props the author
/// moved by hand are kept. Each line opts in via its <see cref="ScatterLineBase.AutoRegenerate"/>
/// toggle (on by default).
/// </summary>
[InitializeOnLoad]
public static class ScatterLineAutoRegenerate
{
    // Lines with a rebuild queued for the next editor tick. The set collapses a burst of changes
    // (dragging a slider, nudging several fields) into one rebuild instead of one per event.
    private static readonly HashSet<ScatterLineBase> pending = new HashSet<ScatterLineBase>();

    static ScatterLineAutoRegenerate()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>
    /// Queues one rebuild for <paramref name="line"/> on the next editor tick. Safe to call from every
    /// changed event during a drag — the pending set dedupes, so the line rebuilds once when the dust
    /// settles rather than mid-drag.
    /// </summary>
    public static void Schedule(ScatterLineBase line)
    {
        if (line == null || !line.AutoRegenerate)
            return;

        // The play-mode transition regenerates on its own; don't race it from a stale inspector event.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!pending.Add(line))
            return;

        EditorApplication.delayCall += () =>
        {
            pending.Remove(line);

            if (line == null || !line.AutoRegenerate)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ScatterLineGenerator.Generate(line, false);
        };
    }

    // Rebuild every opted-in line in the open scenes just before play starts, so pressing Play is
    // enough to see current props even if a rebuild was somehow skipped.
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        foreach (ScatterLineBase line in Object.FindObjectsByType<ScatterLineBase>(FindObjectsInactive.Include))
        {
            if (line != null && line.AutoRegenerate)
                ScatterLineGenerator.Generate(line, false);
        }
    }
}
