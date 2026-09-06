using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fails loudly when a gameplay scene is missing a <see cref="SceneService{T}"/> it needs (the camera
/// rig, director, overlay). Scene services must be authored into the scene — nothing spawns them — so
/// this is the guard against shipping a scene where you forgot to drop the SceneCameraManagement bundle.
/// Runs on demand from the menu and automatically before every build.
/// </summary>
public static class SceneServiceValidator
{
    // Scenes with no gameplay camera stack. Matched by file name (no extension), case-insensitive.
    private static readonly HashSet<string> ExemptScenes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Main Menu",
    };

    // Scene services that only some scenes need. Camera zones exist in caves, not the
    // overworld. CheckpointManager is the scene start/respawn — extension scenes have none.
    private static readonly HashSet<string> OptionalServices = new(StringComparer.Ordinal)
    {
        "CameraZoneManager",
        "CheckpointManager",
    };

    [MenuItem("Tools/The Last Acorn/Validate Scene Services")]
    private static void ValidateMenu()
    {
        // Opening scenes single-mode discards unsaved work, so let the user save first / bail.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        List<string> problems = Validate();
        if (problems.Count == 0)
        {
            Debug.Log("[SceneServiceValidator] All gameplay scenes carry their scene services.");
            return;
        }

        foreach (string problem in problems)
        {
            Debug.LogError($"[SceneServiceValidator] {problem}");
        }

        EditorUtility.DisplayDialog(
            "Scene service validation failed",
            $"{problems.Count} scene(s) are missing a scene service. See the Console for details.",
            "OK");
    }

    /// <summary>
    /// Opens each enabled, non-exempt build scene and returns one message per missing service. Empty
    /// means every scene is covered. Restores the active scene afterwards.
    /// </summary>
    public static List<string> Validate()
    {
        var problems = new List<string>();

        List<Type> required = FindConcreteSceneServiceTypes();
        if (required.Count == 0)
        {
            return problems; // Nothing derives from SceneService<T>; nothing to enforce.
        }

        string activeScenePath = SceneManager.GetActiveScene().path;

        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || string.IsNullOrEmpty(entry.path)) continue;

            string sceneName = Path.GetFileNameWithoutExtension(entry.path);
            if (ExemptScenes.Contains(sceneName)) continue;

            Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            HashSet<Type> present = CollectComponentTypes(scene);

            // A service counts as present if any component in the scene is that type or a subclass.
            foreach (Type service in required)
            {
                bool found = present.Any(service.IsAssignableFrom);
                if (!found)
                {
                    problems.Add($"'{sceneName}' is missing scene service '{service.Name}'.");
                }
            }
        }

        // Reopen whatever was active before the scan so the editor lands where the user left it.
        if (!string.IsNullOrEmpty(activeScenePath))
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }

        return problems;
    }

    // Every concrete MonoBehaviour whose base chain passes through the open generic SceneService<>.
    private static List<Type> FindConcreteSceneServiceTypes()
    {
        return TypeCache.GetTypesDerivedFrom(typeof(MonoBehaviour))
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition && DerivesFromSceneService(t))
            .Where(t => !OptionalServices.Contains(t.Name))
            .ToList();
    }

    private static bool DerivesFromSceneService(Type type)
    {
        for (Type b = type.BaseType; b != null; b = b.BaseType)
        {
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(SceneService<>))
            {
                return true;
            }
        }
        return false;
    }

    private static HashSet<Type> CollectComponentTypes(Scene scene)
    {
        var types = new HashSet<Type>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null) types.Add(behaviour.GetType());
            }
        }
        return types;
    }

    // Fails the build if any scene is missing a service — the "build-list scan" enforcement.
    private sealed class BuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> problems = Validate();
            if (problems.Count == 0) return;

            foreach (string problem in problems)
            {
                Debug.LogError($"[SceneServiceValidator] {problem}");
            }
            throw new BuildFailedException(
                $"Scene service validation failed: {problems.Count} missing service(s). See Console.");
        }
    }
}
