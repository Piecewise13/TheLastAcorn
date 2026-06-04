
using UnityEngine;
using UnityEngine.UI;


[System.Serializable]
public class DebugSettings : MonoBehaviour
{
    [Header("Debug Options")]
    [SerializeField] private bool defaultSpawnPoint = true;
    [SerializeField] private bool disablePersistence = false;
    [SerializeField] private bool showDebugLogs = false;

    [SerializeField] private bool bypassTutorial = false;

    public static DebugSettings Instance { get; private set; }

    public bool DisablePersistence => disablePersistence;
    public bool ShowDebugLogs => showDebugLogs;
    public bool BypassTutorial => bypassTutorial;
    public bool DefaultSpawnPoint => defaultSpawnPoint;

    private Transform playerTransform;
    private Vector3 playerStartingPosition;

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

    private void Start()
    {
        playerTransform = FindAnyObjectByType<PlayerMove>().transform;
        playerStartingPosition = playerTransform.position;
    }

    private void OnValidate()
    {
        if (Application.isPlaying && showDebugLogs)
            Debug.Log($"[DebugSettings] Settings changed - Persistence: {(!disablePersistence ? "ENABLED" : "DISABLED")}");

    }

    public void ResetPlayerPrefs()
    {
        if (showDebugLogs)
        {
            Debug.Log("[DebugSettings] Resetting PlayerPrefs...");
        }

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        if (showDebugLogs)
        {
            Debug.Log("[DebugSettings] PlayerPrefs reset complete.");
        }
    }

    public void ResetPlayerPostition()
    {
        playerTransform.position = playerStartingPosition;
    }
}