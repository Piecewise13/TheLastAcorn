using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared scene-view authoring for any <see cref="ScatterLineBase"/>: draw the line by clicking
/// points, drag the handles, set the normal offset by eye, and generate/clear the props. The two
/// variants ride on top of this and add only their own content: the graded line adds one prop
/// set + a parallax band, the layered line adds a stack of layers. Everything else — the shape, the
/// placement/orientation/sorting knobs, the generate buttons, and the whole Scene GUI — lives here so
/// both inspectors behave identically.
/// </summary>
public abstract class ScatterLineBaseEditor : Editor
{
    private const float PointHandleSize = 0.06f;
    private const float NormalArrowSize = 0.8f;
    private const float OffsetHandleSize = 0.12f;
    private const float InsertThreshold = 1.2f;

    private static readonly Color LineColor = new Color(0.35f, 0.85f, 1f, 0.95f);
    private static readonly Color NormalColor = new Color(1f, 0.75f, 0.2f, 0.9f);
    private static readonly Color SelectedColor = new Color(0.4f, 1f, 0.45f, 1f);

    private bool addMode;
    private int selectedPoint = -1;
    private int sceneControlId;

    // Set while a scene handle (point or offset) is being dragged; flushed to an auto-regenerate on
    // the next mouse-up so the rebuild happens once on release rather than every drag frame.
    private bool pendingSceneRegen;

    // -------------------------------------------------------------------------
    // Variant hooks
    // -------------------------------------------------------------------------

    /// <summary>The library and the variant's own content: a prop set for the graded line, a stack of
    /// layers for the layered one. Called inside the settings change-check.</summary>
    protected abstract void DrawContentSection(ScatterLineBase line);

    /// <summary>The variant's parallax controls, plus the shared vertical-influence knob.</summary>
    protected abstract void DrawParallaxSection(ScatterLineBase line);

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var line = (ScatterLineBase)target;

        // Everything up to the Generate section is settings the placement reads, so a change here
        // should rebuild. The action buttons (Generate/Clear/Center) live below and are deliberately
        // left out of the check — a Clear must not immediately auto-regenerate the props back.
        EditorGUI.BeginChangeCheck();
        DrawContentSection(line);
        DrawShapeSection(line);
        DrawPlacementSection();
        DrawParallaxSection(line);
        bool settingsChanged = EditorGUI.EndChangeCheck();

        DrawGenerateSection(line);

        serializedObject.ApplyModifiedProperties();

        if (settingsChanged)
            ScatterLineAutoRegenerate.Schedule(line);
    }

    protected void DrawIndexPopup(string propertyName, string label, string[] names, string emptyMessage)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (names.Length == 0)
        {
            EditorGUILayout.HelpBox(emptyMessage, MessageType.Warning);
            return;
        }

        int current = Mathf.Clamp(property.intValue, 0, names.Length - 1);
        int picked = EditorGUILayout.Popup(label, current, names);
        if (picked != property.intValue)
            property.intValue = picked;
    }

    private void DrawShapeSection(ScatterLineBase line)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);

        EditorGUILayout.LabelField(
            $"{line.Points.Count} points, {line.TotalLength():0.#} units long",
            EditorStyles.miniLabel);

        bool wasAddMode = addMode;
        addMode = GUILayout.Toggle(addMode, addMode ? "Adding Points (click to stop)" : "Add Points",
            "Button", GUILayout.Height(24f));
        if (addMode != wasAddMode)
            SceneView.RepaintAll();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selectedPoint < 0 || line.Points.Count <= 2))
            {
                if (GUILayout.Button(selectedPoint >= 0 ? $"Delete Point {selectedPoint}" : "Delete Point"))
                    DeleteSelectedPoint(line);
            }

            using (new EditorGUI.DisabledScope(line.Points.Count < 2))
            {
                if (GUILayout.Button("Reverse Direction"))
                {
                    Undo.RecordObject(line, "Reverse Scatter Line");
                    line.Points.Reverse();
                    selectedPoint = -1;
                    EditorUtility.SetDirty(line);
                    SceneView.RepaintAll();
                }
            }
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("flipNormal"));

        if (addMode)
        {
            string where = ContinuesFromSelection(line)
                ? $"after point {selectedPoint}"
                : "at the end of the line";

            EditorGUILayout.HelpBox(
                $"Click in the Scene view to add a point {where}. Click a handle first to draw on " +
                "from that node instead of the end. Shift-click near the line to insert one between " +
                "two existing points. Select a handle and press Delete to remove it.",
                MessageType.Info);
        }
    }

    private void DrawPlacementSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);

        SerializedProperty mode = serializedObject.FindProperty("placementMode");
        EditorGUILayout.PropertyField(mode);
        bool fill = mode.enumValueIndex == (int)ScatterLineBase.PlacementMode.Fill;

        if (fill)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fillSpacing"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fillJitter"));
            EditorGUILayout.LabelField(
                "Fill scatters props inside the shape — draw the outline as a closed loop back to the " +
                "start. Overlap and normal offset do not apply.",
                EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("overlap"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("overlapJitter"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("normalOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("normalOffsetJitter"));
            EditorGUILayout.LabelField(
                "Drag the green cone in the Scene view to set the offset by eye.",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
        if (!fill)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("alignToNormal"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationJitter"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mirrorVariety"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sorting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sortingOrder"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("offsetDepthSorting"));
    }

    private void DrawGenerateSection(ScatterLineBase line)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generate", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoRegenerate"));

        // The seed lives here beside its Re-roll button, so it sits outside the inspector-wide change
        // check. Watch it on its own and schedule a rebuild when either the field or the button moves it.
        bool seedChanged;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("seed"));
            seedChanged = EditorGUI.EndChangeCheck();

            if (GUILayout.Button("Re-roll", GUILayout.Width(70f)))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(line, "Re-roll Scatter Line");
                line.Seed = ScatterLineBase.NewSeed();
                EditorUtility.SetDirty(line);
                seedChanged = true;
            }
        }

        serializedObject.ApplyModifiedProperties();

        if (seedChanged)
            ScatterLineAutoRegenerate.Schedule(line);

        int alive = ScatterLineGenerator.CountAlive(line);
        int detached = ScatterLineGenerator.CountDetached(line);
        int clearable = ScatterLineGenerator.CountClearable(line);
        EditorGUILayout.LabelField(
            detached > 0
                ? $"{alive} props placed, {detached} moved by hand (kept on rebuild)"
                : $"{alive} props placed",
            EditorStyles.miniLabel);

        if (clearable > alive)
        {
            EditorGUILayout.LabelField(
                $"{clearable - alive} more object(s) under this line the tool did not place. " +
                "Clear takes those too.",
                EditorStyles.miniLabel);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(alive > 0 ? "Regenerate" : "Generate", GUILayout.Height(28f)))
                ScatterLineGenerator.Generate(line, false);

            using (new EditorGUI.DisabledScope(clearable == 0))
            {
                var clearLabel = new GUIContent(
                    "Clear",
                    "Deletes every object under this line, including props moved or added by hand. " +
                    "Undoable in one step.");

                if (GUILayout.Button(clearLabel, GUILayout.Height(28f), GUILayout.Width(70f)))
                    ScatterLineGenerator.Clear(line);
            }
        }

        using (new EditorGUI.DisabledScope(alive == 0))
        {
            if (GUILayout.Button("Center Pivot on Props"))
                ScatterLineGenerator.CenterPivotOnProps(line);
        }

        using (new EditorGUI.DisabledScope(detached == 0))
        {
            if (GUILayout.Button("Regenerate, discarding hand tweaks"))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Discard hand tweaks?",
                    $"{detached} prop(s) on '{line.name}' were moved by hand. Rebuilding will " +
                    "replace them with generated ones. This can be undone.",
                    "Discard", "Cancel");

                if (confirmed)
                    ScatterLineGenerator.Generate(line, true);
            }
        }
    }

    private void DeleteSelectedPoint(ScatterLineBase line)
    {
        if (selectedPoint < 0 || selectedPoint >= line.Points.Count || line.Points.Count <= 2)
            return;

        Undo.RecordObject(line, "Delete Scatter Line Point");
        line.Points.RemoveAt(selectedPoint);
        selectedPoint = -1;
        EditorUtility.SetDirty(line);
        SceneView.RepaintAll();
        ScatterLineAutoRegenerate.Schedule(line);
    }

    // -------------------------------------------------------------------------
    // Scene view
    // -------------------------------------------------------------------------

    protected virtual void OnSceneGUI()
    {
        var line = (ScatterLineBase)target;

        if (addMode)
        {
            sceneControlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(sceneControlId);
        }

        DrawLine(line);
        DrawNormals(line);
        DrawPointHandles(line);
        HandleInput(line);

        // A point or offset drag reports "changed" every frame it moves; rebuilding then would thrash
        // and fight the handle. Instead we flag the drag and rebuild once on release (mouse up).
        if (pendingSceneRegen && Event.current.type == EventType.MouseUp)
        {
            pendingSceneRegen = false;
            ScatterLineAutoRegenerate.Schedule(line);
        }
    }

    private void DrawLine(ScatterLineBase line)
    {
        if (line.Points.Count < 2)
            return;

        var worldPoints = new Vector3[line.Points.Count];
        for (int i = 0; i < worldPoints.Length; i++)
            worldPoints[i] = line.GetWorldPoint(i);

        Handles.color = LineColor;
        Handles.DrawAAPolyLine(3f, worldPoints);
    }

    /// <summary>
    /// Small arrows along the line showing which side is open air, plus one cone at the middle of the
    /// whole line that drags the normal offset for every prop at once.
    /// </summary>
    private void DrawNormals(ScatterLineBase line)
    {
        if (line.Points.Count < 2)
            return;

        float offset = line.NormalOffset;
        float total = line.TotalLength();
        float walked = 0f;

        Handles.color = NormalColor;
        for (int i = 1; i < line.Points.Count; i++)
        {
            float segmentLength = Vector3.Distance(line.GetWorldPoint(i - 1), line.GetWorldPoint(i));
            if (segmentLength <= 1e-4f)
                continue;

            line.Evaluate(walked + segmentLength * 0.5f, out Vector3 mid, out Vector3 normal);
            walked += segmentLength;

            float length = HandleUtility.GetHandleSize(mid) * NormalArrowSize * 0.6f;
            Vector3 from = mid + normal * offset;
            Handles.DrawAAPolyLine(2f, from, from + normal * length);
            if (Mathf.Abs(offset) > 1e-4f)
                Handles.DrawDottedLine(mid, from, 3f);
        }

        DrawOffsetHandle(line, total, offset);
    }

    private void DrawOffsetHandle(ScatterLineBase line, float totalLength, float offset)
    {
        line.Evaluate(totalLength * 0.5f, out Vector3 anchor, out Vector3 normal);

        float standoff = HandleUtility.GetHandleSize(anchor) * NormalArrowSize;
        Vector3 handlePosition = anchor + normal * (standoff + offset);
        float size = HandleUtility.GetHandleSize(handlePosition) * OffsetHandleSize;

        Handles.color = SelectedColor;

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(handlePosition, normal, size, Handles.ConeHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(line, "Offset Scatter Line");
            // The standoff only keeps the cone off the line, so back it out to get the real offset.
            line.NormalOffset = Vector3.Dot(moved - anchor, normal) - standoff;
            EditorUtility.SetDirty(line);
            Repaint();
            pendingSceneRegen = true;
        }

        float jitter = line.NormalOffsetJitter;
        string label = jitter > 0f
            ? $"offset {offset:0.##}  ±{jitter:0.##}"
            : $"offset {offset:0.##}";
        Handles.Label(handlePosition + normal * standoff * 0.4f, label);
    }

    private void DrawPointHandles(ScatterLineBase line)
    {
        for (int i = 0; i < line.Points.Count; i++)
        {
            Vector3 world = line.GetWorldPoint(i);
            float size = HandleUtility.GetHandleSize(world) * PointHandleSize;
            int id = GUIUtility.GetControlID(FocusType.Passive);

            Handles.color = i == selectedPoint ? SelectedColor : LineColor;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(id, world, size, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(line, "Move Scatter Line Point");
                line.SetWorldPoint(i, new Vector3(moved.x, moved.y, 0f));
                selectedPoint = i;
                EditorUtility.SetDirty(line);
                Repaint();
                pendingSceneRegen = true;
            }

            if (GUIUtility.hotControl == id && selectedPoint != i)
            {
                selectedPoint = i;
                Repaint();
            }

            if (addMode && i == selectedPoint && ContinuesFromSelection(line))
            {
                Handles.color = SelectedColor;
                Handles.Label(world + Vector3.up * size * 2f, "adding from here");
            }
        }
    }

    private void HandleInput(ScatterLineBase line)
    {
        Event evt = Event.current;

        if (addMode && evt.GetTypeForControl(sceneControlId) == EventType.MouseDown && evt.button == 0)
        {
            AddPoint(line, MouseToWorld(evt.mousePosition), evt.shift);
            evt.Use();
            return;
        }

        bool deletePressed = evt.type == EventType.KeyDown &&
                             (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace);
        if (deletePressed && selectedPoint >= 0 && line.Points.Count > 2)
        {
            DeleteSelectedPoint(line);
            evt.Use();
        }
    }

    private void AddPoint(ScatterLineBase line, Vector3 world, bool insert)
    {
        Undo.RecordObject(line, "Add Scatter Line Point");

        Vector3 local3 = line.transform.InverseTransformPoint(world);
        var local = new Vector2(local3.x, local3.y);

        int insertAt = insert ? FindSegmentToSplit(line, world) : -1;
        if (insertAt < 0 && ContinuesFromSelection(line))
        {
            // A selected handle is the pen tip: the point lands just after that node instead of on
            // the far end of the line. The new point becomes the tip in turn, so a run of clicks
            // draws forward from wherever the author started rather than jumping back to the end.
            insertAt = selectedPoint;
        }

        if (insertAt >= 0)
        {
            line.Points.Insert(insertAt + 1, local);
            selectedPoint = insertAt + 1;
        }
        else
        {
            line.Points.Add(local);
            selectedPoint = line.Points.Count - 1;
        }

        EditorUtility.SetDirty(line);
        SceneView.RepaintAll();
        Repaint();
        // The added point lands on mouse-down; rebuild on the following mouse-up like a drag does.
        pendingSceneRegen = true;
    }

    /// <summary>
    /// True when a selected handle should absorb the next added point. The last point is excluded
    /// because continuing from it is the same thing as appending, and appending keeps the simpler
    /// undo entry and label.
    /// </summary>
    private bool ContinuesFromSelection(ScatterLineBase line)
    {
        return selectedPoint >= 0 && selectedPoint < line.Points.Count - 1;
    }

    /// <summary>
    /// Index of the segment start whose segment the click landed on, or -1 when the click was not
    /// near the line.
    /// </summary>
    private static int FindSegmentToSplit(ScatterLineBase line, Vector3 world)
    {
        int best = -1;
        float bestDistance = float.MaxValue;

        for (int i = 1; i < line.Points.Count; i++)
        {
            Vector3 a = line.GetWorldPoint(i - 1);
            Vector3 b = line.GetWorldPoint(i);
            float distance = HandleUtility.DistancePointLine(world, a, b);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i - 1;
            }
        }

        float threshold = HandleUtility.GetHandleSize(world) * InsertThreshold;
        return bestDistance <= threshold ? best : -1;
    }

    private static Vector3 MouseToWorld(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 point = ray.GetPoint(enter);
            return new Vector3(point.x, point.y, 0f);
        }
        return new Vector3(ray.origin.x, ray.origin.y, 0f);
    }
}
