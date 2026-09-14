
using UnityEngine;
using Player;


[System.Serializable]
public class DebugSettings : PersistentSingleton<DebugSettings>
{
    [Header("Debug Options")]
    [Tooltip("When hitting Play, leave the player GameObject where it sits instead of snapping it to this scene's start point. Off (the shipping behaviour): snap to the start point. Does not apply to scenes loaded after Play.")]
    [SerializeField] private bool skipSpawnAtSceneStartOnPlay = false;
    [SerializeField] private bool disablePersistence = false;
    [SerializeField] private bool showDebugLogs = false;

    [SerializeField] private bool bypassTutorial = false;

    [Tooltip("Treat every gameplay ability (Zoom, Glide, Leap) as already unlocked, without writing them to the save or playing the unlock cinematics.")]
    [SerializeField] private bool unlockAllAbilities = false;

    [Header("Upgrade Levels")]
    [Tooltip("Read the upgrade levels below instead of the saved ones. Nothing is written to the " +
             "save, so turning this off restores the real progress untouched.")]
    [SerializeField] private bool overrideUpgradeLevels = false;

    [Tooltip("Level to force for each stat. Clamped to the stat's max level, so an overshoot just " +
             "means 'maxed' rather than an out-of-range curve sample.")]
    [Range(0, 7)]
    [SerializeField] private int climbingLevel = 0;
    [Range(0, 7)]
    [SerializeField] private int visionLevel = 0;
    [Range(0, 7)]
    [SerializeField] private int glidingLevel = 0;

    private const string ProductionModeKey = "Debug.ProductionMode";

    /// <summary>
    /// Master kill switch for every debug option. Stored in PlayerPrefs so it persists
    /// across play sessions, editor restarts and builds. When on, every debug getter
    /// below reports its shipping value regardless of the serialized fields.
    /// </summary>
    public static bool ProductionMode
    {
        get => PlayerPrefs.GetInt(ProductionModeKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ProductionModeKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public bool DisablePersistence => !ProductionMode && disablePersistence;
    public bool ShowDebugLogs => !ProductionMode && showDebugLogs;
    public bool BypassTutorial => !ProductionMode && bypassTutorial;
    public bool SpawnAtSceneStartOnPlay => ProductionMode || !skipSpawnAtSceneStartOnPlay;
    public bool UnlockAllAbilities => !ProductionMode && unlockAllAbilities;
    public bool OverrideUpgradeLevels => !ProductionMode && overrideUpgradeLevels;

    /// <summary>
    /// The level to force for <paramref name="stat"/>. Only consulted when
    /// <see cref="OverrideUpgradeLevels"/> is on; the caller clamps to the stat's max level.
    /// </summary>
    public int GetUpgradeLevel(PlayerUpgradeManager.UpgradeStat stat) => stat switch
    {
        PlayerUpgradeManager.UpgradeStat.Climbing => climbingLevel,
        PlayerUpgradeManager.UpgradeStat.Vision => visionLevel,
        PlayerUpgradeManager.UpgradeStat.Gliding => glidingLevel,
        _ => 0
    };

    private Transform playerTransform;
    private Vector3 playerStartingPosition;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicate, being destroyed

        if (ProductionMode)
        {
            Debug.Log("[DebugSettings] PRODUCTION MODE - all debug options forced off");
        }
        else if (showDebugLogs)
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

        if (SpawnAtSceneStartOnPlay)
            CheckpointManager.Current?.SpawnAtStart(playerTransform.gameObject);

        playerStartingPosition = playerTransform.position;
    }

    private void OnValidate()
    {
        if (Application.isPlaying && ShowDebugLogs)
            Debug.Log($"[DebugSettings] Settings changed - Persistence: {(!DisablePersistence ? "ENABLED" : "DISABLED")}");

        // Re-push the stats so a level typed in during Play takes effect on the next frame rather
        // than on the next scene load. Also covers switching the override off again.
        if (Application.isPlaying && PlayerUpgradeManager.Instance != null)
            PlayerUpgradeManager.Instance.ApplyToPlayer();
    }

    public void ResetPlayerPrefs()
    {
        if (ShowDebugLogs)
        {
            Debug.Log("[DebugSettings] Resetting PlayerPrefs...");
        }

        bool productionMode = ProductionMode;

        PlayerPrefs.DeleteAll();
        ProductionMode = productionMode; // the master toggle is not game state, keep it

        if (ShowDebugLogs)
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
