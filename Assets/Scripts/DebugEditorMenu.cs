using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DebugSettings))] 
public class DebugEditorMenu : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields first (optional)
        DrawDefaultInspector();

        DebugSettings myComponent = (DebugSettings)target;

        // Add a button
        if (GUILayout.Button("Reset PlayerPrefs"))
        {
            // Call a method on your component when the button is pressed
            myComponent.ResetPlayerPrefs();
        }
    }
}
