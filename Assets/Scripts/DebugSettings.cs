
using UnityEngine;
using UnityEngine.UI;


[System.Serializable]
public class DebugSettings : MonoBehaviour
{
    [Header("Debug Options")]
    [SerializeField] private bool defaultSpawnPoint = true;
    [SerializeField] private bool disablePersistence = false;
    [SerializeField] private bool showDebugLogs = false;

    public static DebugSettings Instance { get; private set; }

    public bool DisablePersistence => disablePersistence;
    public bool ShowDebugLogs => showDebugLogs;
    public bool DefaultSpawnPoint => defaultSpawnPoint;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (showDebugLogs)
            {
                Debug.Log($"[DebugSettings] Initialized - Persistence: {(!DisablePersistence ? "ENABLED" : "DISABLED")}");
                Debug.Log($"[DebugSettings] Initialized - Default Spawn Point: {(DefaultSpawnPoint ? "ENABLED" : "DISABLED")}");
            }
        }
        else Destroy(gameObject);

    }

    private void OnValidate()
    {
        if (Application.isPlaying && showDebugLogs)
            Debug.Log($"[DebugSettings] Settings changed - Persistence: {(!disablePersistence ? "ENABLED" : "DISABLED")}");

    }
}