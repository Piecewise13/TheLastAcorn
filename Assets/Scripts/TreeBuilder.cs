using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TreeNode : MonoBehaviour
{
    public string nodeName = "Node";
}

public class TreeBuilder : MonoBehaviour
{
    [System.Serializable]
    public class Connection
    {
        public TreeNode from;
        public TreeNode to;
        public float width = 1f; // Specify width for the rectangle sprite
    }

    public List<TreeNode> nodes = new List<TreeNode>();
    public List<Connection> connections = new List<Connection>();
    public Sprite rectangleSprite; // Assign a rectangle sprite in the inspector

#if UNITY_EDITOR
    [CustomEditor(typeof(TreeBuilder))]
    public class TreeBuilderEditor : Editor
    {
        TreeBuilder builder;
        bool drawConnections = false;

        void OnEnable()
        {
            builder = (TreeBuilder)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);

            if (GUILayout.Button("Add Node"))
            {
                var go = new GameObject("TreeNode");
                go.transform.parent = builder.transform;
                go.transform.position = builder.transform.position;
                var node = go.AddComponent<TreeNode>();
                node.nodeName = "Node " + (builder.nodes.Count + 1);
                builder.nodes.Add(node);
                EditorUtility.SetDirty(builder);
            }

            if (builder.nodes.Count >= 2)
            {
                if (GUILayout.Button("Add Connection"))
                {
                    builder.connections.Add(new TreeBuilder.Connection
                    {
                        from = builder.nodes[builder.nodes.Count - 2],
                        to = builder.nodes[builder.nodes.Count - 1],
                        width = 1f // Default width
                    });
                    EditorUtility.SetDirty(builder);
                }
            }

            drawConnections = GUILayout.Toggle(drawConnections, "Draw Connections");

            if (drawConnections)
            {
                SceneView.RepaintAll();
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawTreeGizmo(TreeBuilder builder, GizmoType gizmoType)
        {
            foreach (var node in builder.nodes)
            {
                Handles.Label(node.transform.position, node.nodeName);
            }

            foreach (var conn in builder.connections)
            {
                if (conn.from != null && conn.to != null)
                {
                    Vector3 start = conn.from.transform.position;
                    Vector3 end = conn.to.transform.position;
                    Vector3 mid = (start + end) / 2f;
                    Vector3 dir = (end - start).normalized;
                    float length = Vector3.Distance(start, end);

                    // Only draw if sprite is assigned
                    if (builder.rectangleSprite != null)
                    {
                        // Calculate rotation
                        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                        // Draw the rectangle sprite at midpoint, rotated and scaled
                        Handles.BeginGUI();
                        Vector3 screenPos = HandleUtility.WorldToGUIPoint(mid);

                        var rect = new Rect(
                            screenPos.x - length * 50f / 2f, // 50 pixels per unit, adjust as needed
                            screenPos.y - conn.width * 50f / 2f,
                            length * 50f,
                            conn.width * 50f
                        );

                        Matrix4x4 matrixBackup = GUI.matrix;
                        GUIUtility.RotateAroundPivot(angle, screenPos);
                        GUI.DrawTexture(rect, builder.rectangleSprite.texture, ScaleMode.StretchToFill, true);
                        GUI.matrix = matrixBackup;
                        Handles.EndGUI();
                    }
                }
            }
        }
    }
#endif
}

