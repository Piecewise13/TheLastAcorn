using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene-view drawing tool that assembles one tree from multiple strokes. The first
/// stroke lays down a base and chains trunk/split pieces capped by a top; later
/// strokes snap onto authored branch points (split branch-Out sockets) and grow
/// branches. Pieces are snapped together by matching two-point sockets with an
/// optional uniform scale for an exact fit. Piece selection is randomized within the
/// species you choose in this window.
/// </summary>
public class TreeBuilderWindow : EditorWindow
{
    private enum StrokeMode
    {
        None,
        Main,
        Branch
    }

    [Header("Library")]
    [SerializeField] private TreePieceLibrary library;
    [SerializeField] private int speciesIndex;

    [Header("Connection")]
    [SerializeField] private bool scaleToFit = true;
    [SerializeField] private float branchSnapRadius = 3f;

    [Header("Sorting")]
    [SerializeField] private bool overrideSortingOrder = false;
    [SerializeField] private int baseSortingOrder = 0;

    [Header("Stroke")]
    [SerializeField] private float sampleSpacing = 0.4f;

    [Header("State")]
    [SerializeField] private bool drawing;
    [SerializeField] private GameObject openTreeRoot;
    [SerializeField] private int treeCounter;

    // Per-drag transient state (not serialized).
    private readonly List<Vector3> strokePoints = new List<Vector3>();
    private bool strokeActive;
    private StrokeMode strokeMode = StrokeMode.None;
    private TreePiece branchTargetPiece;
    private TreeSocket branchTargetSocket;
    private int sceneControlId;

    private const float ConsumeEpsilonFraction = 0.3f;
    private const int MaxPiecesPerChain = 60;

    [MenuItem("Tools/The Last Acorn/Tree Builder")]
    public static void Open()
    {
        TreeBuilderWindow window = GetWindow<TreeBuilderWindow>("Tree Builder");
        window.minSize = new Vector2(280f, 300f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Tree Builder", EditorStyles.boldLabel);

        library = (TreePieceLibrary)EditorGUILayout.ObjectField("Library", library, typeof(TreePieceLibrary), false);

        if (library == null || library.Species.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Assign a Tree Piece Library with at least one species. Create one via " +
                "Assets > Create > The Last Acorn > Tree Piece Library.",
                MessageType.Warning);
            drawing = false;
            return;
        }

        string[] names = library.GetSpeciesNames();
        speciesIndex = Mathf.Clamp(speciesIndex, 0, names.Length - 1);
        speciesIndex = EditorGUILayout.Popup("Species", speciesIndex, names);

        EditorGUILayout.Space();
        scaleToFit = EditorGUILayout.Toggle(
            new GUIContent("Scale To Fit", "Uniformly scale each piece so both connection points line up exactly."),
            scaleToFit);
        branchSnapRadius = EditorGUILayout.FloatField(
            new GUIContent("Branch Snap Radius", "How close a branch stroke must start to a branch point to attach."),
            branchSnapRadius);
        sampleSpacing = Mathf.Max(0.05f, EditorGUILayout.FloatField(
            new GUIContent("Sample Spacing", "Minimum world distance between recorded stroke samples."),
            sampleSpacing));

        EditorGUILayout.Space();
        overrideSortingOrder = EditorGUILayout.Toggle(
            new GUIContent("Override Sorting", "Assign an incrementing sprite sorting order to placed pieces."),
            overrideSortingOrder);
        using (new EditorGUI.DisabledScope(!overrideSortingOrder))
        {
            baseSortingOrder = EditorGUILayout.IntField("Base Sorting Order", baseSortingOrder);
        }

        EditorGUILayout.Space();
        bool newDrawing = GUILayout.Toggle(drawing, drawing ? "Drawing (click to stop)" : "Draw", "Button", GUILayout.Height(30f));
        if (newDrawing != drawing)
        {
            drawing = newDrawing;
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(openTreeRoot == null))
            {
                if (GUILayout.Button("Commit Tree"))
                    CommitTree();
                if (GUILayout.Button("Cancel Tree"))
                    CancelTree();
            }
        }

        EditorGUILayout.Space();
        if (openTreeRoot != null)
            EditorGUILayout.HelpBox(
                $"Open tree: {openTreeRoot.name}. Draw more strokes starting on an orange branch point, " +
                "then Commit when done.",
                MessageType.Info);
        else
            EditorGUILayout.HelpBox("Draw a stroke in the Scene view to start a new tree's main trunk line.", MessageType.None);
    }

    // -------------------------------------------------------------------------
    // Scene interaction
    // -------------------------------------------------------------------------

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!drawing || library == null)
            return;

        sceneControlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(sceneControlId);

        Event evt = Event.current;

        DrawBranchPointOverlay();
        DrawStrokePreview();

        EventType type = evt.GetTypeForControl(sceneControlId);
        if (type == EventType.MouseDown && evt.button == 0)
            BeginStroke(evt);
        else if (type == EventType.MouseDrag && evt.button == 0 && strokeActive)
            ContinueStroke(evt, sceneView);
        else if (type == EventType.MouseUp && evt.button == 0 && strokeActive)
            EndStroke(evt);
        else if (type == EventType.KeyDown)
            HandleShortcuts(evt);
    }

    private void HandleShortcuts(Event evt)
    {
        if (openTreeRoot == null)
            return;

        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            CommitTree();
            evt.Use();
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            CancelTree();
            evt.Use();
        }
    }

    private void BeginStroke(Event evt)
    {
        Vector3 worldPos = MouseToWorld(evt.mousePosition);
        strokeMode = ResolveStrokeMode(worldPos, out branchTargetPiece, out branchTargetSocket);

        if (strokeMode == StrokeMode.None)
        {
            // Nothing valid to start here (e.g. a tree is open but the click missed every
            // branch point). Consume the click so we don't accidentally select scene objects.
            evt.Use();
            return;
        }

        strokeActive = true;
        strokePoints.Clear();
        strokePoints.Add(worldPos);
        GUIUtility.hotControl = sceneControlId;
        evt.Use();
    }

    private void ContinueStroke(Event evt, SceneView sceneView)
    {
        Vector3 worldPos = MouseToWorld(evt.mousePosition);
        if (strokePoints.Count == 0 || Vector3.Distance(worldPos, strokePoints[strokePoints.Count - 1]) >= sampleSpacing)
            strokePoints.Add(worldPos);

        sceneView.Repaint();
        evt.Use();
    }

    private void EndStroke(Event evt)
    {
        Vector3 worldPos = MouseToWorld(evt.mousePosition);
        if (strokePoints.Count == 0 || Vector3.Distance(worldPos, strokePoints[strokePoints.Count - 1]) > 0.001f)
            strokePoints.Add(worldPos);

        float strokeLength = StrokeLength();

        if (strokeMode == StrokeMode.Main)
            BuildMainStroke(strokePoints[0], strokeLength);
        else if (strokeMode == StrokeMode.Branch)
            BuildBranchStroke(strokeLength);

        strokeActive = false;
        strokeMode = StrokeMode.None;
        branchTargetPiece = null;
        branchTargetSocket = null;
        strokePoints.Clear();
        GUIUtility.hotControl = 0;
        evt.Use();
        Repaint();
    }

    private StrokeMode ResolveStrokeMode(Vector3 worldPos, out TreePiece targetPiece, out TreeSocket targetSocket)
    {
        targetPiece = null;
        targetSocket = null;

        if (openTreeRoot == null)
            return StrokeMode.Main;

        float bestDist = float.MaxValue;
        foreach (var candidate in GetAvailableBranchPoints(openTreeRoot))
        {
            Vector3 mid = candidate.piece.WorldMidpoint(candidate.socket);
            float dist = Vector3.Distance(worldPos, mid);
            if (dist < bestDist)
            {
                bestDist = dist;
                targetPiece = candidate.piece;
                targetSocket = candidate.socket;
            }
        }

        if (targetPiece != null && bestDist <= branchSnapRadius)
            return StrokeMode.Branch;

        return StrokeMode.None;
    }

    // -------------------------------------------------------------------------
    // Placement
    // -------------------------------------------------------------------------

    private void BuildMainStroke(Vector3 startWorld, float strokeLength)
    {
        TreeSpeciesEntry entry = library.GetSpecies(speciesIndex);
        if (entry == null)
            return;

        List<GameObject> chainPool = CombinePools(entry.trunks, entry.splits);
        if (chainPool.Count == 0)
        {
            Debug.LogWarning("Tree Builder: selected species has no trunk or split prefabs.");
            return;
        }

        int group = BeginStrokeUndo("Draw Tree Main");

        openTreeRoot = new GameObject($"Tree {treeCounter++}");
        openTreeRoot.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(openTreeRoot, "Draw Tree Main");

        int sortCounter = 0;
        GameObject baseObj = RandomPick(entry.bases);
        Vector3 targetA;
        Vector3 targetB;

        if (baseObj != null)
        {
            TreePiece basePiece = InstantiatePiece(baseObj, openTreeRoot.transform, ref sortCounter);
            basePiece.transform.position = new Vector3(startWorld.x, startWorld.y, 0f);
            TreeSocket baseOut = basePiece.GetMainOutSocket();
            if (baseOut == null)
            {
                Debug.LogWarning($"Tree Builder: base '{baseObj.name}' has no Out socket.");
                targetA = startWorld;
                targetB = startWorld + Vector3.right;
            }
            else
            {
                targetA = basePiece.WorldPointA(baseOut);
                targetB = basePiece.WorldPointB(baseOut);
            }
        }
        else
        {
            // No base pool: start the trunk chain from a horizontal seam at the click point.
            targetA = startWorld + Vector3.left * 0.5f;
            targetB = startWorld + Vector3.right * 0.5f;
        }

        BuildChain(openTreeRoot.transform, targetA, targetB, chainPool, entry.tops, strokeLength, 1, ref sortCounter);

        Selection.activeGameObject = openTreeRoot;
        EndStrokeUndo(group);
    }

    private void BuildBranchStroke(float strokeLength)
    {
        if (branchTargetPiece == null || branchTargetSocket == null || openTreeRoot == null)
            return;

        TreeSpeciesEntry entry = library.GetSpecies(speciesIndex);
        if (entry == null)
            return;

        List<GameObject> branchPool = entry.branches.Count > 0
            ? CombinePools(entry.branches, null)
            : CombinePools(entry.trunks, entry.splits);

        if (branchPool.Count == 0)
        {
            Debug.LogWarning("Tree Builder: selected species has no branch (or trunk) prefabs to grow a branch.");
            return;
        }

        int group = BeginStrokeUndo("Draw Tree Branch");

        int sortCounter = openTreeRoot.GetComponentsInChildren<SpriteRenderer>().Length;
        Vector3 targetA = branchTargetPiece.WorldPointA(branchTargetSocket);
        Vector3 targetB = branchTargetPiece.WorldPointB(branchTargetSocket);

        BuildChain(openTreeRoot.transform, targetA, targetB, branchPool, entry.tops, strokeLength, 1, ref sortCounter);

        EndStrokeUndo(group);
    }

    private void BuildChain(Transform parent, Vector3 targetA, Vector3 targetB, List<GameObject> pool,
        List<GameObject> topPool, float budget, int minCount, ref int sortCounter)
    {
        Vector3 currentA = targetA;
        Vector3 currentB = targetB;
        float remaining = budget;
        int count = 0;

        while (count < MaxPiecesPerChain)
        {
            GameObject prefab = RandomPick(pool);
            if (prefab == null)
                break;

            TreePiece piece = InstantiatePiece(prefab, parent, ref sortCounter);
            if (!AlignPieceIn(piece, currentA, currentB))
            {
                Undo.DestroyObjectImmediate(piece.gameObject);
                break;
            }

            count++;
            remaining -= Mathf.Max(PieceSpan(piece), 0.01f);

            TreeSocket outSocket = piece.GetMainOutSocket();
            if (outSocket == null)
                break;
            currentA = piece.WorldPointA(outSocket);
            currentB = piece.WorldPointB(outSocket);

            if (count >= minCount && remaining <= 0f)
                break;
        }

        GameObject topPrefab = RandomPick(topPool);
        if (topPrefab != null)
        {
            TreePiece top = InstantiatePiece(topPrefab, parent, ref sortCounter);
            if (!AlignPieceIn(top, currentA, currentB))
                Undo.DestroyObjectImmediate(top.gameObject);
        }
    }

    /// <summary>
    /// Positions, rotates and (optionally) uniformly scales <paramref name="piece"/> so
    /// its In socket's two points land exactly on the target seam (worldA, worldB).
    /// Returns false if the piece has no In socket.
    /// </summary>
    private bool AlignPieceIn(TreePiece piece, Vector3 worldA, Vector3 worldB)
    {
        TreeSocket inSocket = piece.GetInSocket();
        if (inSocket == null)
            return false;

        Vector2 localA = inSocket.a;
        Vector2 localB = inSocket.b;
        Vector2 u = localB - localA;
        Vector3 v = worldB - worldA;

        float uLen = u.magnitude;
        float vLen = v.magnitude;

        float scale = 1f;
        if (scaleToFit && uLen > 1e-5f)
            scale = vLen / uLen;

        float thetaTarget = Mathf.Atan2(v.y, v.x);
        float thetaLocal = Mathf.Atan2(u.y, u.x);
        float thetaDeg = (thetaTarget - thetaLocal) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, thetaDeg);

        Vector3 scaledLocalA = new Vector3(localA.x, localA.y, 0f) * scale;
        Vector3 position = worldA - rotation * scaledLocalA;

        Undo.RecordObject(piece.transform, "Align Tree Piece");
        piece.transform.rotation = rotation;
        piece.transform.localScale = new Vector3(scale, scale, 1f);
        piece.transform.position = new Vector3(position.x, position.y, 0f);
        return true;
    }

    private static float PieceSpan(TreePiece piece)
    {
        TreeSocket inSocket = piece.GetInSocket();
        TreeSocket outSocket = piece.GetMainOutSocket();
        if (inSocket != null && outSocket != null)
            return Vector3.Distance(piece.WorldMidpoint(inSocket), piece.WorldMidpoint(outSocket));

        SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null)
            return renderer.bounds.size.y;

        return 1f;
    }

    private TreePiece InstantiatePiece(GameObject prefab, Transform parent, ref int sortCounter)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, true);
        Undo.RegisterCreatedObjectUndo(instance, "Place Tree Piece");

        TreePiece piece = instance.GetComponent<TreePiece>();
        if (piece == null)
        {
            Debug.LogWarning($"Tree Builder: prefab '{prefab.name}' has no TreePiece component; " +
                             "it will be placed at its pivot without socket alignment.");
        }

        if (overrideSortingOrder)
        {
            SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
                renderer.sortingOrder = baseSortingOrder + sortCounter;
        }
        sortCounter++;

        return piece != null ? piece : instance.AddComponent<TreePiece>();
    }

    // -------------------------------------------------------------------------
    // Branch point availability
    // -------------------------------------------------------------------------

    private IEnumerable<(TreePiece piece, TreeSocket socket)> GetAvailableBranchPoints(GameObject root)
    {
        TreePiece[] pieces = root.GetComponentsInChildren<TreePiece>();

        var inMidpoints = new List<Vector3>();
        foreach (TreePiece piece in pieces)
        {
            TreeSocket inSocket = piece.GetInSocket();
            if (inSocket != null)
                inMidpoints.Add(piece.WorldMidpoint(inSocket));
        }

        foreach (TreePiece piece in pieces)
        {
            foreach (TreeSocket socket in piece.GetOutSockets())
            {
                if (!socket.isBranchPoint)
                    continue;

                Vector3 mid = piece.WorldMidpoint(socket);
                float seamLength = Vector3.Distance(piece.WorldPointA(socket), piece.WorldPointB(socket));
                float epsilon = Mathf.Max(0.05f, seamLength * ConsumeEpsilonFraction);

                bool consumed = false;
                foreach (Vector3 inMid in inMidpoints)
                {
                    if (Vector3.Distance(inMid, mid) <= epsilon)
                    {
                        consumed = true;
                        break;
                    }
                }

                if (!consumed)
                    yield return (piece, socket);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Tree lifecycle
    // -------------------------------------------------------------------------

    private void CommitTree()
    {
        openTreeRoot = null;
        SceneView.RepaintAll();
        Repaint();
    }

    private void CancelTree()
    {
        if (openTreeRoot != null)
            Undo.DestroyObjectImmediate(openTreeRoot);
        openTreeRoot = null;
        SceneView.RepaintAll();
        Repaint();
    }

    // -------------------------------------------------------------------------
    // Undo helpers
    // -------------------------------------------------------------------------

    private static int BeginStrokeUndo(string name)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(name);
        return Undo.GetCurrentGroup();
    }

    private static void EndStrokeUndo(int group)
    {
        Undo.CollapseUndoOperations(group);
    }

    // -------------------------------------------------------------------------
    // Scene overlay
    // -------------------------------------------------------------------------

    private void DrawBranchPointOverlay()
    {
        if (openTreeRoot == null)
            return;

        Handles.color = new Color(1f, 0.55f, 0.1f);
        foreach (var candidate in GetAvailableBranchPoints(openTreeRoot))
        {
            Vector3 mid = candidate.piece.WorldMidpoint(candidate.socket);
            float size = HandleUtility.GetHandleSize(mid) * 0.12f;
            Handles.DrawSolidDisc(mid, Vector3.forward, size);
        }
    }

    private void DrawStrokePreview()
    {
        if (!strokeActive || strokePoints.Count < 2)
            return;

        Handles.color = strokeMode == StrokeMode.Branch ? new Color(1f, 0.55f, 0.1f) : new Color(0.3f, 1f, 0.3f);
        Handles.DrawAAPolyLine(4f, strokePoints.ToArray());
    }

    private float StrokeLength()
    {
        float length = 0f;
        for (int i = 1; i < strokePoints.Count; i++)
            length += Vector3.Distance(strokePoints[i - 1], strokePoints[i]);
        return length;
    }

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    private static Vector3 MouseToWorld(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 point = ray.GetPoint(enter);
            return new Vector3(point.x, point.y, 0f);
        }
        return new Vector3(ray.origin.x, ray.origin.y, 0f);
    }

    private static List<GameObject> CombinePools(List<GameObject> a, List<GameObject> b)
    {
        var result = new List<GameObject>();
        if (a != null)
        {
            foreach (GameObject go in a)
                if (go != null)
                    result.Add(go);
        }
        if (b != null)
        {
            foreach (GameObject go in b)
                if (go != null)
                    result.Add(go);
        }
        return result;
    }

    private static GameObject RandomPick(List<GameObject> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        // Build a compact list of non-null entries so empty slots don't bias the roll.
        var valid = new List<GameObject>(pool.Count);
        foreach (GameObject go in pool)
            if (go != null)
                valid.Add(go);

        if (valid.Count == 0)
            return null;

        return valid[Random.Range(0, valid.Count)];
    }
}
