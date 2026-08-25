using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authoring harness for camera zones. A zone's whole authored state is a position and an
/// orthographic size, so the fastest loop is to frame the room in the scene view by eye and capture
/// it — the gizmos then show whether the result covers the room.
/// </summary>
[CustomEditor(typeof(CameraZoneManager))]
public class CameraZoneManagerEditor : Editor
{
    private bool showValidation = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (CameraZoneManager)target;
        CameraZone[] zones = manager.CollectZones();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Zones ({zones.Length})", EditorStyles.boldLabel);

        if (zones.Length == 0)
        {
            EditorGUILayout.HelpBox("No CameraZone components under this manager.", MessageType.Info);
            return;
        }

        for (int i = 0; i < zones.Length; i++)
        {
            DrawZoneRow(manager, zones[i], i);
        }

        EditorGUILayout.Space();
        if (Application.isPlaying && GUILayout.Button("Release Preview Claim"))
        {
            manager.DebugRelease();
        }

        EditorGUILayout.Space();
        showValidation = EditorGUILayout.Foldout(showValidation, "Validation", true);
        if (showValidation)
        {
            DrawValidation(manager, zones);
        }
    }

    private void DrawZoneRow(CameraZoneManager manager, CameraZone zone, int index)
    {
        if (zone == null) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{index + 1}. {zone.ZoneId}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{zone.Activation}  ·  size {zone.OrthographicSize:0.##}",
                    EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select"))
                {
                    Selection.activeGameObject = zone.gameObject;
                }

                if (GUILayout.Button("Frame in Scene View"))
                {
                    FrameInSceneView(zone);
                }

                if (GUILayout.Button("Capture from Scene View"))
                {
                    CaptureFromSceneView(zone);
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Preview"))
                    {
                        manager.DebugActivate(zone);
                    }
                }
            }
        }
    }

    /// <summary>Points the scene view at exactly what the player will see in this zone.</summary>
    private static void FrameInSceneView(CameraZone zone)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;

        view.in2DMode = true;
        view.LookAt(zone.transform.position, Quaternion.identity, zone.OrthographicSize * 2f);
        view.Repaint();
    }

    /// <summary>
    /// Writes the scene view's current framing into the zone. Pan and zoom until the room looks
    /// right, then capture — this is the loop the harness exists for.
    /// </summary>
    private static void CaptureFromSceneView(CameraZone zone)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null) return;

        Undo.RecordObject(zone, "Capture Camera Zone");
        Undo.RecordObject(zone.transform, "Capture Camera Zone");

        Vector3 pivot = view.pivot;
        pivot.z = zone.transform.position.z;
        zone.transform.position = pivot;

        float size = view.camera.orthographic
            ? view.camera.orthographicSize
            : view.size;
        zone.SetOrthographicSizeFromEditor(size);

        EditorUtility.SetDirty(zone);
    }

    private static void DrawValidation(CameraZoneManager manager, CameraZone[] zones)
    {
        var messages = new List<string>();
        var ids = new Dictionary<string, int>();

        for (int i = 0; i < zones.Length; i++)
        {
            CameraZone zone = zones[i];
            if (zone == null) continue;

            if (zone.OrthographicSize <= 0.01f)
            {
                messages.Add($"{zone.ZoneId}: orthographic size is zero.");
            }

            if (zone.AllowsTrigger && !zone.HasTriggerCollider)
            {
                messages.Add($"{zone.ZoneId}: set to {zone.Activation} activation but has no trigger collider.");
            }

            ids.TryGetValue(zone.ZoneId, out int count);
            ids[zone.ZoneId] = count + 1;
        }

        foreach (KeyValuePair<string, int> pair in ids)
        {
            if (pair.Value > 1) messages.Add($"Duplicate zone id '{pair.Key}' used {pair.Value} times.");
        }

        messages.AddRange(FindCoverageGaps(zones));

        if (messages.Count == 0)
        {
            EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(string.Join("\n", messages), MessageType.Warning);
    }

    /// <summary>
    /// Flags trigger zones that touch no neighbour. Leaving all zones reverts to overworld follow, so
    /// an unintended gap between two rooms shows up in play as a follow-then-reframe stutter. Rooms
    /// are meant to overlap at doorways.
    /// </summary>
    private static IEnumerable<string> FindCoverageGaps(CameraZone[] zones)
    {
        var gaps = new List<string>();
        var bounds = new List<(CameraZone zone, Bounds bounds)>();

        for (int i = 0; i < zones.Length; i++)
        {
            CameraZone zone = zones[i];
            if (zone == null || !zone.AllowsTrigger) continue;

            Collider2D[] colliders = zone.GetComponentsInChildren<Collider2D>();
            if (colliders.Length == 0) continue;

            Bounds combined = colliders[0].bounds;
            for (int c = 1; c < colliders.Length; c++)
            {
                combined.Encapsulate(colliders[c].bounds);
            }

            bounds.Add((zone, combined));
        }

        if (bounds.Count < 2) return gaps;

        for (int i = 0; i < bounds.Count; i++)
        {
            bool touchesAnything = false;
            for (int j = 0; j < bounds.Count; j++)
            {
                if (i == j) continue;
                if (bounds[i].bounds.Intersects(bounds[j].bounds))
                {
                    touchesAnything = true;
                    break;
                }
            }

            if (!touchesAnything)
            {
                gaps.Add($"{bounds[i].zone.ZoneId}: overlaps no other zone, so leaving it reverts to overworld follow.");
            }
        }

        return gaps;
    }
}
