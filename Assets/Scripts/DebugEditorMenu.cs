using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DebugSettings))] 
public class DebugEditorMenu : Editor
{
    public override void OnInspectorGUI()
    {
        bool productionMode = DebugSettings.ProductionMode;

        using (new EditorGUI.DisabledScope(productionMode))
        {
            // Draw the default inspector fields first (optional)
            DrawDefaultInspector();
        }

        DebugSettings myComponent = (DebugSettings)target;

        // Add a button
        if (GUILayout.Button("Reset PlayerPrefs"))
        {
            // Call a method on your component when the button is pressed
            myComponent.ResetPlayerPrefs();
        }

        if (GUILayout.Button("Reset Player Position"))
        {
            myComponent.ResetPlayerPostition();
        }

        if (GUILayout.Button("Respawn Player At Scene Start"))
        {
            myComponent.RespawnPlayerAtSceneStart();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Master Switch", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        bool toggled = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "Production Mode",
                "Forces every debug option above off, no matter how it is serialized. Saved to PlayerPrefs, so it survives play sessions, editor restarts and builds."),
            productionMode);
        if (EditorGUI.EndChangeCheck())
            DebugSettings.ProductionMode = toggled;

        if (toggled)
            EditorGUILayout.HelpBox("Production mode is ON - all debug options above are ignored.", MessageType.Warning);
    }
}
