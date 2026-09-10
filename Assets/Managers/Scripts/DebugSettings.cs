
using UnityEngine;
using UnityEngine.Serialization;
using Player;


[System.Serializable]
public class DebugSettings : PersistentSingleton<DebugSettings>
{
    [Header("Debug Options")]
    [Tooltip("When hitting Play, snap the player to this scene's start point. Off: leave the player GameObject where it sits. Does not apply to scenes loaded after Play.")]
    [FormerlySerializedAs("defaultSpawnPoint")]
    [SerializeField] private bool spawnAtSceneStartOnPlay = true;
    [SerializeField] private bool disablePersistence = false;
    [SerializeField] private bool showDebugLogs = false;

    [SerializeField] private bool bypassTutorial = false;

    public bool DisablePersistence => disablePersistence;
    public bool ShowDebugLogs => showDebugLogs;
    public bool BypassTutorial => bypassTutorial;
    public bool SpawnAtSceneStartOnPlay => spawnAtSceneStartOnPlay;

    private Transform playerTransform;
    private Vector3 playerStartingPosition;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicate, being destroyed

        if (showDebugLogs)
        {
            Debug.Log($"[DebugSettings] Initialized - Persistence: {(!DisablePersistence ? "ENABLED" : "DISABLED")}");
            Debug.Log($"[DebugSettings] Initialized - Spawn At Scene Start On Play: {(SpawnAtSceneStartOnPlay ? "ENABLED" : "DISABLED")}");
        }
    }

    private void Start()
    {
        if (Instance != this) return;

        if (DisablePersistence)
        {
            ScoreManager.Instance?.ResetScore();
            if (LevelScoreManager.Instance != null)
                LevelScoreManager.Instance.ResetLevelScore();
        }

        PlayerMoveManager move = FindAnyObjectByType<PlayerMoveManager>();
        if (move == null) return;

        playerTransform = move.transform.root;

        if (spawnAtSceneStartOnPlay)
            CheckpointManager.Current?.SpawnAtStart(playerTransform.gameObject);

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
        if (playerTransform != null)
            playerTransform.position = playerStartingPosition;
    }

    public void RespawnPlayerAtSceneStart()
    {
        if (playerTransform == null)
        {
            PlayerMoveManager move = FindAnyObjectByType<PlayerMoveManager>();
            if (move == null) return;
            playerTransform = move.transform.root;
        }

        CheckpointManager checkpointManager = CheckpointManager.For(playerTransform.gameObject.scene);
        if (checkpointManager == null || !checkpointManager.HasStartPoint) return;

        checkpointManager.SpawnAtStart(playerTransform.gameObject);
        playerStartingPosition = playerTransform.position;
    }
}
