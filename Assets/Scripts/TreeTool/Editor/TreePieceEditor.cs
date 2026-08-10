using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector + scene authoring for <see cref="TreePiece"/>. Adds buttons to create
/// common socket layouts and lets you drag each socket's two seam points directly on
/// the sprite in the Scene view (works in prefab edit mode).
/// </summary>
[CustomEditor(typeof(TreePiece))]
public class TreePieceEditor : Editor
{
    private const float HandleScreenSize = 0.08f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TreePiece piece = (TreePiece)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add Socket", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("In"))
                AddSocket(piece, TreeSocketRole.In, false);
            if (GUILayout.Button("Out"))
                AddSocket(piece, TreeSocketRole.Out, false);
            if (GUILayout.Button("Branch Out"))
                AddSocket(piece, TreeSocketRole.Out, true);
        }

        EditorGUILayout.HelpBox(
            "Drag the seam endpoints in the Scene view. Blue = In, Green = Out, Orange = branch point.\n" +
            "Typical layouts: Base = 1 Out; Trunk = 1 In + 1 Out; Split = 1 In + 2 Out (one Branch Out); " +
            "Branch = 1 In + 1 Out; Top = 1 In.",
            MessageType.Info);
    }

    private static void AddSocket(TreePiece piece, TreeSocketRole role, bool isBranchPoint)
    {
        Undo.RecordObject(piece, "Add Tree Socket");

        // Seed the new socket near the sprite's top or bottom edge so it starts on the art.
        float y = role == TreeSocketRole.In ? -0.5f : 0.5f;
        Bounds bounds = GetSpriteLocalBounds(piece);
        if (bounds.size != Vector3.zero)
            y = role == TreeSocketRole.In ? bounds.min.y : bounds.max.y;
        float halfWidth = bounds.size != Vector3.zero ? bounds.extents.x * 0.4f : 0.5f;

        piece.Sockets.Add(new TreeSocket
        {
            name = isBranchPoint ? "Branch" : role.ToString(),
            role = role,
            isBranchPoint = isBranchPoint,
            a = new Vector2(-halfWidth, y),
            b = new Vector2(halfWidth, y)
        });

        EditorUtility.SetDirty(piece);
    }

    private static Bounds GetSpriteLocalBounds(TreePiece piece)
    {
        SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null)
            return renderer.sprite.bounds;
        return new Bounds(Vector3.zero, Vector3.zero);
    }

    private void OnSceneGUI()
    {
        TreePiece piece = (TreePiece)target;
        if (piece.Sockets == null)
            return;

        Transform t = piece.transform;

        for (int i = 0; i < piece.Sockets.Count; i++)
        {
            TreeSocket socket = piece.Sockets[i];
            if (socket == null)
                continue;

            Handles.color = SocketColor(socket);

            Vector3 worldA = t.TransformPoint(socket.a);
            Vector3 worldB = t.TransformPoint(socket.b);

            Handles.DrawLine(worldA, worldB);
            Handles.Label((worldA + worldB) * 0.5f, $"{socket.name} ({socket.role})");

            EditorGUI.BeginChangeCheck();
            float sizeA = HandleUtility.GetHandleSize(worldA) * HandleScreenSize;
            float sizeB = HandleUtility.GetHandleSize(worldB) * HandleScreenSize;
            Vector3 newWorldA = Handles.FreeMoveHandle(worldA, sizeA, Vector3.zero, Handles.DotHandleCap);
            Vector3 newWorldB = Handles.FreeMoveHandle(worldB, sizeB, Vector3.zero, Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(piece, "Move Tree Socket");
                socket.a = ToLocal2D(t, newWorldA);
                socket.b = ToLocal2D(t, newWorldB);
                EditorUtility.SetDirty(piece);
            }
        }
    }

    private static Vector2 ToLocal2D(Transform t, Vector3 worldPoint)
    {
        Vector3 local = t.InverseTransformPoint(worldPoint);
        return new Vector2(local.x, local.y);
    }

    private static Color SocketColor(TreeSocket socket)
    {
        if (socket.role == TreeSocketRole.In)
            return new Color(0.2f, 0.7f, 1f);
        if (socket.isBranchPoint)
            return new Color(1f, 0.55f, 0.1f);
        return new Color(0.3f, 1f, 0.3f);
    }
}
