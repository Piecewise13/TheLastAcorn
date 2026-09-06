using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene view authoring for a <see cref="CaveBoundary"/>. Click points to draw the rock line, drag
/// the handles to adjust it, and generate the rocks along it. The arrows show which side of the line
/// is open air, which is the side the rocks face.
/// </summary>
[CustomEditor(typeof(CaveBoundary))]
public class CaveBoundaryEditor : Editor
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

    [MenuItem("Tools/The Last Acorn/Cave Boundary")]
    private static void CreateBoundary()
    {
        var go = new GameObject("Cave Boundary");
        Undo.RegisterCreatedObjectUndo(go, "Create Cave Boundary");

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 center = view != null ? view.pivot : Vector3.zero;
        go.transform.position = new Vector3(center.x, center.y, 0f);

        CaveBoundary boundary = go.AddComponent<CaveBoundary>();
        boundary.Seed = CaveBoundary.NewSeed();
        boundary.Points.Add(new Vector2(-10f, 0f));
        boundary.Points.Add(new Vector2(10f, 0f));

        Selection.activeGameObject = go;
    }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var boundary = (CaveBoundary)target;

        // Everything up to the Generate section is settings the placement reads, so a change here
        // should rebuild. The action buttons (Generate/Clear/Center) live below and are deliberately
        // left out of the check — a Clear must not immediately auto-regenerate the rocks back.
        EditorGUI.BeginChangeCheck();
        DrawLibrarySection(boundary);
        DrawShapeSection(boundary);
        DrawPlacementSection();
        DrawParallaxSection(boundary);
        bool settingsChanged = EditorGUI.EndChangeCheck();

        DrawGenerateSection(boundary);

        serializedObject.ApplyModifiedProperties();

        if (settingsChanged)
            CaveBoundaryAutoRegenerate.Schedule(boundary);
    }

    private void DrawLibrarySection(CaveBoundary boundary)
    {
        EditorGUILayout.LabelField("Library", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("library"));

        CaveRockLibrary library = boundary.Library;
        if (library == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Cave Rock Library. Create one via Assets > Create > The Last Acorn > " +
                "Cave Rock Library, then add a rock set for the Blue variants and a depth tier per band.",
                MessageType.Warning);
            return;
        }

        DrawIndexPopup("rockSetIndex", "Rock Set", library.GetRockSetNames(),
            "The library has no rock sets yet.");
        DrawIndexPopup("depthTierIndex", "Depth Tier", library.GetDepthTierNames(),
            "The library has no depth tiers yet.");

        CaveDepthTier tier = boundary.Tier;
        if (tier != null)
        {
            string material = tier.materialOverride != null ? tier.materialOverride.name : "prefab material";
            EditorGUILayout.LabelField(
                $"Scale {library.MinScale:0.##}–{library.MaxScale:0.##} (library-wide)   " +
                $"material {material}",
                EditorStyles.miniLabel);
        }
    }

    private void DrawIndexPopup(string propertyName, string label, string[] names, string emptyMessage)
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

    private void DrawParallaxSection(CaveBoundary boundary)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Parallax", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("parallaxProfile"));

        CaveParallaxProfile profile = boundary.ParallaxProfile;
        if (profile == null)
        {
            EditorGUILayout.HelpBox(
                "No Cave Parallax Profile assigned, so these rocks hold still at runtime. Create one " +
                "via Assets > Create > The Last Acorn > Cave Parallax Profile and drop it here.",
                MessageType.Warning);
        }
        else
        {
            DrawIndexPopup("parallaxTierIndex", "Parallax Tier", profile.GetTierNames(),
                "The profile has no tiers yet — add one on the Cave Parallax Profile asset.");
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalInfluence"));

        if (profile != null && profile.Tiers.Count > 0)
        {
            EditorGUILayout.LabelField(
                "Rocks scatter across the tier's Z Jitter; each rock's factor is interpolated by " +
                "where its Z lands — nearest takes the min factor and slides fastest, furthest takes " +
                "the max. Regenerate after changing Z Jitter to re-place the rocks.",
                EditorStyles.miniLabel);
        }
    }

    private void DrawShapeSection(CaveBoundary boundary)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);

        EditorGUILayout.LabelField(
            $"{boundary.Points.Count} points, {boundary.TotalLength():0.#} units long",
            EditorStyles.miniLabel);

        bool wasAddMode = addMode;
        addMode = GUILayout.Toggle(addMode, addMode ? "Adding Points (click to stop)" : "Add Points",
            "Button", GUILayout.Height(24f));
        if (addMode != wasAddMode)
            SceneView.RepaintAll();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selectedPoint < 0 || boundary.Points.Count <= 2))
            {
                if (GUILayout.Button(selectedPoint >= 0 ? $"Delete Point {selectedPoint}" : "Delete Point"))
                    DeleteSelectedPoint(boundary);
            }

            using (new EditorGUI.DisabledScope(boundary.Points.Count < 2))
            {
                if (GUILayout.Button("Reverse Direction"))
                {
                    Undo.RecordObject(boundary, "Reverse Cave Boundary");
                    boundary.Points.Reverse();
                    selectedPoint = -1;
                    EditorUtility.SetDirty(boundary);
                    SceneView.RepaintAll();
                }
            }
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("flipNormal"));

        if (addMode)
        {
            string where = ContinuesFromSelection(boundary)
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
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overlap"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overlapJitter"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("normalOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("normalOffsetJitter"));
        EditorGUILayout.LabelField(
            "Drag the green cone in the Scene view to set the offset by eye.",
            EditorStyles.miniLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("alignToNormal"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationJitter"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mirrorVariety"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sorting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sortingOrder"));
    }


    private void DrawGenerateSection(CaveBoundary boundary)
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
                Undo.RecordObject(boundary, "Re-roll Cave Boundary");
                boundary.Seed = CaveBoundary.NewSeed();
                EditorUtility.SetDirty(boundary);
                seedChanged = true;
            }
        }

        serializedObject.ApplyModifiedProperties();

        if (seedChanged)
            CaveBoundaryAutoRegenerate.Schedule(boundary);

        int alive = CaveBoundaryGenerator.CountAlive(boundary);
        int detached = CaveBoundaryGenerator.CountDetached(boundary);
        int clearable = CaveBoundaryGenerator.CountClearable(boundary);
        EditorGUILayout.LabelField(
            detached > 0
                ? $"{alive} rocks placed, {detached} moved by hand (kept on rebuild)"
                : $"{alive} rocks placed",
            EditorStyles.miniLabel);

        if (clearable > alive)
        {
            EditorGUILayout.LabelField(
                $"{clearable - alive} more object(s) under this boundary the tool did not place. " +
                "Clear takes those too.",
                EditorStyles.miniLabel);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(alive > 0 ? "Regenerate" : "Generate", GUILayout.Height(28f)))
                CaveBoundaryGenerator.Generate(boundary, false);

            using (new EditorGUI.DisabledScope(clearable == 0))
            {
                var clearLabel = new GUIContent(
                    "Clear",
                    "Deletes every object under this boundary, including rocks moved or added by " +
                    "hand. Undoable in one step.");

                if (GUILayout.Button(clearLabel, GUILayout.Height(28f), GUILayout.Width(70f)))
                    CaveBoundaryGenerator.Clear(boundary);
            }
        }

        using (new EditorGUI.DisabledScope(alive == 0))
        {
            if (GUILayout.Button("Center Pivot on Rocks"))
                CaveBoundaryGenerator.CenterPivotOnRocks(boundary);
        }

        using (new EditorGUI.DisabledScope(detached == 0))
        {
            if (GUILayout.Button("Regenerate, discarding hand tweaks"))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Discard hand tweaks?",
                    $"{detached} rock(s) on '{boundary.name}' were moved by hand. Rebuilding will " +
                    "replace them with generated ones. This can be undone.",
                    "Discard", "Cancel");

                if (confirmed)
                    CaveBoundaryGenerator.Generate(boundary, true);
            }
        }
    }

    private void DeleteSelectedPoint(CaveBoundary boundary)
    {
        if (selectedPoint < 0 || selectedPoint >= boundary.Points.Count || boundary.Points.Count <= 2)
            return;

        Undo.RecordObject(boundary, "Delete Cave Boundary Point");
        boundary.Points.RemoveAt(selectedPoint);
        selectedPoint = -1;
        EditorUtility.SetDirty(boundary);
        SceneView.RepaintAll();
        CaveBoundaryAutoRegenerate.Schedule(boundary);
    }

    // -------------------------------------------------------------------------
    // Scene view
    // -------------------------------------------------------------------------

    private void OnSceneGUI()
    {
        var boundary = (CaveBoundary)target;

        if (addMode)
        {
            sceneControlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(sceneControlId);
        }

        DrawLine(boundary);
        DrawNormals(boundary);
        DrawPointHandles(boundary);
        HandleInput(boundary);

        // A point or offset drag reports "changed" every frame it moves; rebuilding then would thrash
        // and fight the handle. Instead we flag the drag and rebuild once on release (mouse up).
        if (pendingSceneRegen && Event.current.type == EventType.MouseUp)
        {
            pendingSceneRegen = false;
            CaveBoundaryAutoRegenerate.Schedule(boundary);
        }
    }

    private void DrawLine(CaveBoundary boundary)
    {
        if (boundary.Points.Count < 2)
            return;

        var worldPoints = new Vector3[boundary.Points.Count];
        for (int i = 0; i < worldPoints.Length; i++)
            worldPoints[i] = boundary.GetWorldPoint(i);

        Handles.color = LineColor;
        Handles.DrawAAPolyLine(3f, worldPoints);
    }

    /// <summary>
    /// Small arrows along the line showing which side is open air, plus one cone at the middle of
    /// the whole boundary that drags the normal offset for every rock at once.
    /// </summary>
    private void DrawNormals(CaveBoundary boundary)
    {
        if (boundary.Points.Count < 2)
            return;

        float offset = boundary.NormalOffset;
        float total = boundary.TotalLength();
        float walked = 0f;

        Handles.color = NormalColor;
        for (int i = 1; i < boundary.Points.Count; i++)
        {
            float segmentLength = Vector3.Distance(boundary.GetWorldPoint(i - 1), boundary.GetWorldPoint(i));
            if (segmentLength <= 1e-4f)
                continue;

            boundary.Evaluate(walked + segmentLength * 0.5f, out Vector3 mid, out Vector3 normal);
            walked += segmentLength;

            float length = HandleUtility.GetHandleSize(mid) * NormalArrowSize * 0.6f;
            Vector3 from = mid + normal * offset;
            Handles.DrawAAPolyLine(2f, from, from + normal * length);
            if (Mathf.Abs(offset) > 1e-4f)
                Handles.DrawDottedLine(mid, from, 3f);
        }

        DrawOffsetHandle(boundary, total, offset);
    }

    private void DrawOffsetHandle(CaveBoundary boundary, float totalLength, float offset)
    {
        boundary.Evaluate(totalLength * 0.5f, out Vector3 anchor, out Vector3 normal);

        float standoff = HandleUtility.GetHandleSize(anchor) * NormalArrowSize;
        Vector3 handlePosition = anchor + normal * (standoff + offset);
        float size = HandleUtility.GetHandleSize(handlePosition) * OffsetHandleSize;

        Handles.color = SelectedColor;

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(handlePosition, normal, size, Handles.ConeHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(boundary, "Offset Cave Boundary");
            // The standoff only keeps the cone off the line, so back it out to get the real offset.
            boundary.NormalOffset = Vector3.Dot(moved - anchor, normal) - standoff;
            EditorUtility.SetDirty(boundary);
            Repaint();
            pendingSceneRegen = true;
        }

        float jitter = boundary.NormalOffsetJitter;
        string label = jitter > 0f
            ? $"offset {offset:0.##}  ±{jitter:0.##}"
            : $"offset {offset:0.##}";
        Handles.Label(handlePosition + normal * standoff * 0.4f, label);
    }

    private void DrawPointHandles(CaveBoundary boundary)
    {
        for (int i = 0; i < boundary.Points.Count; i++)
        {
            Vector3 world = boundary.GetWorldPoint(i);
            float size = HandleUtility.GetHandleSize(world) * PointHandleSize;
            int id = GUIUtility.GetControlID(FocusType.Passive);

            Handles.color = i == selectedPoint ? SelectedColor : LineColor;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(id, world, size, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(boundary, "Move Cave Boundary Point");
                boundary.SetWorldPoint(i, new Vector3(moved.x, moved.y, 0f));
                selectedPoint = i;
                EditorUtility.SetDirty(boundary);
                Repaint();
                pendingSceneRegen = true;
            }

            if (GUIUtility.hotControl == id && selectedPoint != i)
            {
                selectedPoint = i;
                Repaint();
            }

            if (addMode && i == selectedPoint && ContinuesFromSelection(boundary))
            {
                Handles.color = SelectedColor;
                Handles.Label(world + Vector3.up * size * 2f, "adding from here");
            }
        }
    }

    private void HandleInput(CaveBoundary boundary)
    {
        Event evt = Event.current;

        if (addMode && evt.GetTypeForControl(sceneControlId) == EventType.MouseDown && evt.button == 0)
        {
            AddPoint(boundary, MouseToWorld(evt.mousePosition), evt.shift);
            evt.Use();
            return;
        }

        bool deletePressed = evt.type == EventType.KeyDown &&
                             (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace);
        if (deletePressed && selectedPoint >= 0 && boundary.Points.Count > 2)
        {
            DeleteSelectedPoint(boundary);
            evt.Use();
        }
    }

    private void AddPoint(CaveBoundary boundary, Vector3 world, bool insert)
    {
        Undo.RecordObject(boundary, "Add Cave Boundary Point");

        Vector3 local3 = boundary.transform.InverseTransformPoint(world);
        var local = new Vector2(local3.x, local3.y);

        int insertAt = insert ? FindSegmentToSplit(boundary, world) : -1;
        if (insertAt < 0 && ContinuesFromSelection(boundary))
        {
            // A selected handle is the pen tip: the point lands just after that node instead of on
            // the far end of the line. The new point becomes the tip in turn, so a run of clicks
            // draws forward from wherever the author started rather than jumping back to the end.
            insertAt = selectedPoint;
        }

        if (insertAt >= 0)
        {
            boundary.Points.Insert(insertAt + 1, local);
            selectedPoint = insertAt + 1;
        }
        else
        {
            boundary.Points.Add(local);
            selectedPoint = boundary.Points.Count - 1;
        }

        EditorUtility.SetDirty(boundary);
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
    private bool ContinuesFromSelection(CaveBoundary boundary)
    {
        return selectedPoint >= 0 && selectedPoint < boundary.Points.Count - 1;
    }

    /// <summary>
    /// Index of the segment start whose segment the click landed on, or -1 when the click was not
    /// near the line.
    /// </summary>
    private static int FindSegmentToSplit(CaveBoundary boundary, Vector3 world)
    {
        int best = -1;
        float bestDistance = float.MaxValue;

        for (int i = 1; i < boundary.Points.Count; i++)
        {
            Vector3 a = boundary.GetWorldPoint(i - 1);
            Vector3 b = boundary.GetWorldPoint(i);
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
