using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> persistentManagerPrefabs; // ScoreManager, DebugSettings, ViewManager, audio, etc.

    [Tooltip("Scene-dependent managers (e.g. CameraManager, DebugManager). Spawned only if an instance is not already present in the loaded scene.")]
    [SerializeField] private List<GameObject> stage2Objects;

    public static GameManager Instance { get; private set; }
    public static bool IsReady { get; private set; }
    public static event Action OnReady;
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        // 1. Seed static state (SaveLoadManager) deterministically
        // 2. Instantiate persistent managers in order, parented under this
        // 3. Mark ready
        IsReady = true;
        OnReady?.Invoke();
        InstantiatePersistent();
        SpawnStage2Objects();
        SaveLoadManager.InitializeSceneState(SceneManager.GetActiveScene().name);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SaveLoadManager.InitializeSceneState(scene.name);
        SpawnStage2Objects();
    }

    private void InstantiatePersistent()
    {
        foreach (var prefab in persistentManagerPrefabs)
        {
            var go = Instantiate(prefab);
            go.name = prefab.name;
            go.transform.SetParent(transform, false); // under the DontDestroyOnLoad initializer
        }
    }

    // Scene-dependent managers are spawned into the active scene only when a matching
    // instance is not already present, so hand-placed scene copies take precedence.
    private void SpawnStage2Objects()
    {
        if (stage2Objects == null) return;

        foreach (var prefab in stage2Objects)
        {
            if (prefab == null) continue;
            if (IsAlreadyInScene(prefab)) continue;

            var go = Instantiate(prefab);
            go.name = prefab.name;
        }
    }

    // Considers a prefab present if any manager component on its root already exists in a loaded scene.
    private static bool IsAlreadyInScene(GameObject prefab)
    {
        foreach (var component in prefab.GetComponents<MonoBehaviour>())
        {
            if (component == null) continue; // missing script reference
            if (FindAnyObjectByType(component.GetType(), FindObjectsInactive.Include) != null)
            {
                return true;
            }
        }
        return false;
    }
}
