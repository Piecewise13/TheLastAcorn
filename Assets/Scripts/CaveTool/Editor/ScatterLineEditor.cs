using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for the graded <see cref="ScatterLine"/>: one prop set and one parallax band, on top of
/// the shared shape/placement/generate authoring in <see cref="ScatterLineBaseEditor"/>.
/// </summary>
[CustomEditor(typeof(ScatterLine))]
public class ScatterLineEditor : ScatterLineBaseEditor
{
    [MenuItem("Tools/The Last Acorn/Scatter Line")]
    private static void CreateScatterLine()
    {
        var go = new GameObject("Scatter Line");
        Undo.RegisterCreatedObjectUndo(go, "Create Scatter Line");

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 center = view != null ? view.pivot : Vector3.zero;
        go.transform.position = new Vector3(center.x, center.y, 0f);

        ScatterLine line = go.AddComponent<ScatterLine>();
        line.Seed = ScatterLineBase.NewSeed();
        line.Points.Add(new Vector2(-10f, 0f));
        line.Points.Add(new Vector2(10f, 0f));

        Selection.activeGameObject = go;
    }

    protected override void DrawContentSection(ScatterLineBase lineBase)
    {
        var line = (ScatterLine)lineBase;

        EditorGUILayout.LabelField("Library", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("library"));

        PropLibrary library = line.Library;
        if (library == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Prop Library. Create one via Assets > Create > The Last Acorn > Prop " +
                "Library, then add a prop set and a material tier.",
                MessageType.Warning);
            return;
        }

        DrawIndexPopup("rockSetIndex", "Prop Set", library.GetPropSetNames(),
            "The library has no prop sets yet.");
        DrawIndexPopup("depthTierIndex", "Material Tier", library.GetTierNames(),
            "The library has no material tiers yet.");

        PropTier tier = line.Tier;
        if (tier != null)
        {
            string material = tier.materialOverride != null ? tier.materialOverride.name : "prefab material";
            EditorGUILayout.LabelField(
                $"Scale {library.MinScale:0.##}–{library.MaxScale:0.##} (library-wide)   " +
                $"material {material}",
                EditorStyles.miniLabel);
        }
    }

    protected override void DrawParallaxSection(ScatterLineBase lineBase)
    {
        var line = (ScatterLine)lineBase;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Parallax", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("parallaxProfile"));

        ParallaxProfile profile = line.ParallaxProfile;
        if (profile == null)
        {
            EditorGUILayout.HelpBox(
                "No Parallax Profile assigned, so these props hold still at runtime. Create one via " +
                "Assets > Create > The Last Acorn > Parallax Profile and drop it here.",
                MessageType.Warning);
        }
        else
        {
            DrawIndexPopup("parallaxTierIndex", "Parallax Tier", profile.GetTierNames(),
                "The profile has no tiers yet — add one on the Parallax Profile asset.");
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalInfluence"));

        if (profile != null && profile.Tiers.Count > 0)
        {
            EditorGUILayout.LabelField(
                "Props scatter across the tier's Z Jitter; each prop's factor is interpolated by " +
                "where its Z lands — nearest takes the min factor and slides fastest, furthest takes " +
                "the max. Regenerate after changing Z Jitter to re-place the props.",
                EditorStyles.miniLabel);
        }
    }
}
