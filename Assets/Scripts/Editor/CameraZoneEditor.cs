using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene view authoring for a single zone. The framed region is drawn as a rectangle and its top
/// edge is a drag handle for the orthographic size, so a room can be framed by eye without entering
/// play mode.
/// </summary>
[CustomEditor(typeof(CameraZone))]
public class CameraZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var zone = (CameraZone)target;
        float aspect = CameraZoneManager.EditorTargetAspect(zone);
        Bounds framed = zone.GetFramedBounds(aspect);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            $"Frames {framed.size.x:0.#} × {framed.size.y:0.#} world units at {aspect:0.00} aspect",
            EditorStyles.miniLabel);

        if (GUILayout.Button("Frame in Scene View"))
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.in2DMode = true;
                view.LookAt(zone.transform.position, Quaternion.identity, zone.OrthographicSize * 2f);
                view.Repaint();
            }
        }
    }

    private void OnSceneGUI()
    {
        var zone = (CameraZone)target;
        float aspect = CameraZoneManager.EditorTargetAspect(zone);
        Bounds framed = zone.GetFramedBounds(aspect);

        Handles.color = new Color(0.35f, 0.85f, 1f, 0.9f);

        var handlePosition = new Vector3(framed.center.x, framed.max.y, framed.center.z);
        float handleSize = HandleUtility.GetHandleSize(handlePosition) * 0.1f;

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(handlePosition, Vector3.up, handleSize, Handles.DotHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(zone, "Resize Camera Zone");
            zone.SetOrthographicSizeFromEditor(Mathf.Abs(moved.y - zone.transform.position.y));
            EditorUtility.SetDirty(zone);
        }

        Handles.Label(new Vector3(framed.min.x, framed.max.y, framed.center.z),
            $"{zone.ZoneId}  ({zone.OrthographicSize:0.##})");
    }
}
