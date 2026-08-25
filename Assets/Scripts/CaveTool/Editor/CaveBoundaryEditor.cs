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

    [MenuItem("Tools/The Last Acorn/Cave Boundary")]
    private static void CreateBoundary()
    {
        var go = new GameObject("Cave Boundary");
        Undo.RegisterCreatedObjectUndo(go, "Create Cave Boundary");

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 center = view != null ? view.pivot : Vector3.zero;
        go.transform.position = new Vector3(center.x, center.y, 0f);

        CaveBoundary boundary = go.AddComponent<CaveBoundary>();
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

        DrawLibrarySection(boundary);
        DrawOutputSection(boundary);
        DrawShapeSection(boundary);
        DrawPlacementSection();
        DrawGenerateSection(boundary);

        serializedObject.ApplyModifiedProperties();
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
            EditorGUILayout.LabelField(
                $"Scale {library.MinScale:0.##}–{library.MaxScale:0.##} (library-wide)   " +
                $"sorting {tier.sortingOrder}   parallax {tier.parallaxFactor:0.###} ±{tier.parallaxVariance:0.###}",
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

    private void DrawOutputSection(CaveBoundary boundary)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rockParent"));

        if (boundary.Tier == null)
            return;

        // Apply first: the status is read from the scene, so a rock parent edited above needs to be
        // live before we check it, or the box lags a frame behind the field.
        serializedObject.ApplyModifiedProperties();
        DrawParallaxStatus(boundary);
    }

    private void DrawParallaxStatus(CaveBoundary boundary)
    {
        CaveDepthTier tier = boundary.Tier;
        CaveParallaxStatus status = CaveBoundaryGenerator.InspectParallax(boundary);
        string parentName = boundary.RockParent.name;

        if (!status.hasLayer)
        {
            EditorGUILayout.HelpBox(
                $"'{parentName}' is not registered with any BackgroundParalax layer, so these rocks " +
                "will not parallax at all.",
                MessageType.Warning);

            ParalaxManager host = CaveBoundaryGenerator.FindBestParallaxHost();
            using (new EditorGUI.DisabledScope(host == null))
            {
                string hostLabel = host != null ? host.gameObject.name : "no BackgroundParalax in scene";
                if (GUILayout.Button($"Register as layer on '{hostLabel}'"))
                {
                    CaveBoundaryGenerator.RegisterLayer(boundary, host);
                }
            }
            return;
        }

        EditorGUILayout.LabelField(
            $"Layer: {status.layerRoot.name} on {status.component.gameObject.name}" +
            (status.nested ? "  (rocks nested below the root)" : string.Empty),
            EditorStyles.miniLabel);

        if (!status.FactorMatches(tier) || !status.VarianceMatches(tier))
        {
            EditorGUILayout.HelpBox(
                $"Layer runs at {status.actualFactor:0.###} ±{status.actualVariance:0.###} but the " +
                $"'{tier.name}' tier expects {tier.parallaxFactor:0.###} ±{tier.parallaxVariance:0.###}. " +
                "The rocks will read at the wrong depth.",
                MessageType.Warning);

            if (GUILayout.Button("Set layer to this tier's factor"))
                CaveBoundaryGenerator.ApplyTierToLayer(boundary, status);
        }

        if (status.NeedsRecurse)
        {
            EditorGUILayout.HelpBox(
                $"'{status.layerRoot.name}' does not walk its subtree, so only its direct children " +
                $"move — and these rocks sit deeper, under '{parentName}'. They will stay still.",
                MessageType.Warning);

            if (GUILayout.Button("Enable Recurse Into Children on the layer"))
                CaveBoundaryGenerator.EnableRecurse(status);
        }

        if (status.duplicateCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"This layer root is registered {status.duplicateCount + 1} times on the same " +
                "BackgroundParalax. Every duplicate moves the rocks again, so they parallax at a " +
                "multiple of the intended factor. Remove the extras by hand.",
                MessageType.Error);
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
            EditorGUILayout.HelpBox(
                "Click in the Scene view to append a point. Shift-click near the line to insert one " +
                "between two existing points. Select a handle and press Delete to remove it.",
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
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sortingStep"));
    }


    private void DrawGenerateSection(CaveBoundary boundary)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generate", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("seed"));
            if (GUILayout.Button("Re-roll", GUILayout.Width(70f)))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(boundary, "Re-roll Cave Boundary");
                boundary.Seed = Random.Range(1, int.MaxValue);
                EditorUtility.SetDirty(boundary);
            }
        }

        serializedObject.ApplyModifiedProperties();

        int alive = CaveBoundaryGenerator.CountAlive(boundary);
        int detached = CaveBoundaryGenerator.CountDetached(boundary);
        EditorGUILayout.LabelField(
            detached > 0
                ? $"{alive} rocks placed, {detached} moved by hand (kept on rebuild)"
                : $"{alive} rocks placed",
            EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(alive > 0 ? "Regenerate" : "Generate", GUILayout.Height(28f)))
                CaveBoundaryGenerator.Generate(boundary, false);

            using (new EditorGUI.DisabledScope(alive == 0))
            {
                if (GUILayout.Button("Clear", GUILayout.Height(28f), GUILayout.Width(70f)))
                    CaveBoundaryGenerator.Clear(boundary);
            }
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
            }

            if (GUIUtility.hotControl == id && selectedPoint != i)
            {
                selectedPoint = i;
                Repaint();
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
