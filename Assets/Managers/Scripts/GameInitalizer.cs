using System;
using System.Collections.Generic;
using UnityEngine;

public class GameInitalizer : MonoBehaviour
{
    [SerializeField] private List<GameObject> persistentManagerPrefabs; // ScoreManager, DebugSettings, ViewManager, audio, etc.
    public static GameInitalizer Instance { get; private set; }
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
}
