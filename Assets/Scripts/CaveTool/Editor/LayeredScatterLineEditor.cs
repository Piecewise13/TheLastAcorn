using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for the layered <see cref="LayeredScatterLine"/>: a stack of layers, each a prop set at
/// its own fixed parallax speed, on top of the shared shape/placement/generate authoring in
/// <see cref="ScatterLineBaseEditor"/>.
/// </summary>
[CustomEditor(typeof(LayeredScatterLine))]
public class LayeredScatterLineEditor : ScatterLineBaseEditor
{
    [MenuItem("Tools/The Last Acorn/Layered Scatter Line")]
    private static void CreateLayeredScatterLine()
    {
        var go = new GameObject("Layered Scatter Line");
        Undo.RegisterCreatedObjectUndo(go, "Create Layered Scatter Line");

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 center = view != null ? view.pivot : Vector3.zero;
        go.transform.position = new Vector3(center.x, center.y, 0f);

        LayeredScatterLine line = go.AddComponent<LayeredScatterLine>();
        line.Seed = ScatterLineBase.NewSeed();
        line.Points.Add(new Vector2(-10f, 0f));
        line.Points.Add(new Vector2(10f, 0f));

        Selection.activeGameObject = go;
    }

    protected override void DrawContentSection(ScatterLineBase lineBase)
    {
        var line = (LayeredScatterLine)lineBase;

        EditorGUILayout.LabelField("Library", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("library"));

        PropLibrary library = line.Library;
        if (library == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Prop Library. Create one via Assets > Create > The Last Acorn > Prop " +
                "Library, then add a prop set per layer and a material tier.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layers (back to front)", EditorStyles.boldLabel);

        string[] setNames = library.GetPropSetNames();
        string[] tierNames = library.GetTierNames();

        SerializedProperty layers = serializedObject.FindProperty("layers");
        if (setNames.Length == 0)
        {
            EditorGUILayout.HelpBox("The library has no prop sets yet.", MessageType.Warning);
        }

        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            SerializedProperty name = layer.FindPropertyRelative("name");
            SerializedProperty setIndex = layer.FindPropertyRelative("propSetIndex");
            SerializedProperty tierIndex = layer.FindPropertyRelative("materialTierIndex");
            SerializedProperty factor = layer.FindPropertyRelative("parallaxFactor");
            SerializedProperty sortingOffset = layer.FindPropertyRelative("sortingOffset");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(name, GUIContent.none);
                    if (GUILayout.Button("✕", GUILayout.Width(24f)))
                    {
                        layers.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                if (setNames.Length > 0)
                {
                    int set = Mathf.Clamp(setIndex.intValue, 0, setNames.Length - 1);
                    int pickedSet = EditorGUILayout.Popup("Prop Set", set, setNames);
                    if (pickedSet != setIndex.intValue)
                        setIndex.intValue = pickedSet;
                }

                if (tierNames.Length > 0)
                {
                    int tier = Mathf.Clamp(tierIndex.intValue, 0, tierNames.Length - 1);
                    int pickedTier = EditorGUILayout.Popup("Material Tier", tier, tierNames);
                    if (pickedTier != tierIndex.intValue)
                        tierIndex.intValue = pickedTier;
                }

                EditorGUILayout.PropertyField(factor, new GUIContent("Parallax Factor"));
                EditorGUILayout.PropertyField(sortingOffset, new GUIContent("Sorting Offset"));
            }
        }

        if (GUILayout.Button("Add Layer"))
        {
            int end = layers.arraySize;
            layers.InsertArrayElementAtIndex(end);
            // A freshly inserted element copies the one above it; give the first a sane default.
            SerializedProperty added = layers.GetArrayElementAtIndex(end);
            if (end == 0)
            {
                added.FindPropertyRelative("name").stringValue = "Layer";
                added.FindPropertyRelative("propSetIndex").intValue = 0;
                added.FindPropertyRelative("materialTierIndex").intValue = 0;
                added.FindPropertyRelative("parallaxFactor").floatValue = 0f;
                added.FindPropertyRelative("sortingOffset").intValue = 0;
            }
        }
    }

    protected override void DrawParallaxSection(ScatterLineBase lineBase)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Parallax", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalInfluence"));
        EditorGUILayout.LabelField(
            "Each layer's speed is set per layer above. Regenerate after changing a layer's set or " +
            "factor to re-place its props.",
            EditorStyles.miniLabel);
    }
}
