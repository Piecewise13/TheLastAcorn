using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SequentialNamer : EditorWindow
{
    string baseName = "Name";
    int startIndex = 1;
    int count = 5;

    [MenuItem("Tools/Sequential Namer")]
    public static void ShowWindow()
    {
        GetWindow<SequentialNamer>("Sequential Namer");
    }

    void OnGUI()
    {
        GUILayout.Label("Create Named Objects", EditorStyles.boldLabel);
        baseName = EditorGUILayout.TextField("Base Name", baseName);
        startIndex = EditorGUILayout.IntField("Start Index", startIndex);
        count = EditorGUILayout.IntField("Count", count);

        if (GUILayout.Button("Generate Objects"))
        {
            GenerateObjects();
        }
    }

    void GenerateObjects()
    {
        if (count <= 0)
        {
            return;
        }

        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        var created = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            GameObject newObj = new GameObject(baseName + (startIndex + i));

            if (prefabStage != null)
            {
                // Objects outside the prefab root live in the stage scene but are never saved.
                newObj.transform.SetParent(prefabStage.prefabContentsRoot.transform, false);
            }
            else
            {
                StageUtility.PlaceGameObjectInCurrentStage(newObj);
            }

            Undo.RegisterCreatedObjectUndo(newObj, "Create " + newObj.name);
            created[i] = newObj;
        }

        if (prefabStage != null)
        {
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
        }

        Selection.objects = created;
    }
}