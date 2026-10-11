using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;

namespace Player
{
/// <summary>
/// Owns the player's stat upgrades (strength, instinct, endurance)
/// as well as acorn collection and segment progress that drives when upgrades are offered.
/// </summary>
public class PlayerUpgradeManager : PersistentSingleton<PlayerUpgradeManager>
{
    public enum UpgradeStat
    {
        /// <summary>Climb duration and climb speed.</summary>
        Climbing = 0,
        /// <summary>How far the camera zooms out, how long scent trails reach, and how far away acorns can be sensed.</summary>
        Vision = 1,
        /// <summary>Top glide speed and how slowly the glide loses height.</summary>
        Gliding = 2,
    }

    public enum UpgradeEffectTarget
    {
        MaxClimbTime,
        ClimbSpeed,
        MaxGlideSpeed,
        ZoomOutAmount,
        EnergyShiftSpeed,
        MaxVelocityZoom,
        MinimumZoom,
        ScentTrailLength,
        ScentDetectionRange,
    }

    [Serializable]
    struct ValueProgression
    {
        public float minValue;
        public float maxValue;
        [Tooltip("Remaps normalized upgrade progress before interpolating from minValue to maxValue. Empty curves are treated as linear.")]
        public AnimationCurve interpolationCurve;
    }

    [Serializable]
    struct UpgradeEffectProgression
    {
        public UpgradeEffectTarget target;
        public ValueProgression progression;
    }

    [Serializable]
    struct UpgradeProgression
    {
        public UpgradeStat stat;
        public UpgradeEffectProgression[] effects;
    }

    // ── Stat upgrade data ────────────────────────────────────────────────────

    /// <summary>
    /// Levels every stat takes to reach its maxValue. Shared and fixed rather than authored per
    /// effect, so all three stats progress at the same rate and one number retunes the whole
    /// upgrade curve.
    /// </summary>
    private const int MAXUPGRADELEVEL = 7;

    public static int MaxUpgradeLevel => MAXUPGRADELEVEL;

    [SerializeField]
    private UpgradeProgression[] upgradeProgressions = new UpgradeProgression[]
    {
        new UpgradeProgression
        {
            stat = UpgradeStat.Climbing,
            effects = new[]
            {
                new UpgradeEffectProgression { target = UpgradeEffectTarget.MaxClimbTime },
                new UpgradeEffectProgression { target = UpgradeEffectTarget.ClimbSpeed },
            }
        },
        new UpgradeProgression
        {
            stat = UpgradeStat.Vision,
            effects = new[]
            {
                new UpgradeEffectProgression { target = UpgradeEffectTarget.ZoomOutAmount },
                new UpgradeEffectProgression { target = UpgradeEffectTarget.ScentTrailLength },
                new UpgradeEffectProgression { target = UpgradeEffectTarget.ScentDetectionRange },
            }
        },
        new UpgradeProgression
        {
            stat = UpgradeStat.Gliding,
            effects = new[]
            {
                new UpgradeEffectProgression { target = UpgradeEffectTarget.MaxGlideSpeed },
                new UpgradeEffectProgression { target = UpgradeEffectTarget.EnergyShiftSpeed },
                new UpgradeEffectProgression { target = UpgradeEffectTarget.MaxVelocityZoom },
                new UpgradeEffectProgression { target = UpgradeEffectTarget.MinimumZoom },
            }
        },
    };

    private static readonly int StatCount = Enum.GetValues(typeof(UpgradeStat)).Length;
    private readonly int[] statLevels = new int[StatCount];
    private const string SaveKey = "UpgradeLevel";

    // ── Segment / acorn collection ────────────────────────────────────────────

    [Tooltip("Acorn cost for each successive segment. The last entry is reused once all explicit costs are exhausted.")]
    [SerializeField] private int[] segmentCosts = { 5 };

    [SerializeField] private AcornCollectionBar acornCollectionBarPrefab = null!;
    [Tooltip("Gold glow attached to the squirrel when the upgrade energy lands, kept until a stat is chosen.")]
    [SerializeField] private PlayerUpgradeGlow upgradeGlowPrefab = null!;

    [Tooltip("Lines that grow out of the glow toward each upgrade's icon.")]
    [SerializeField] private UpgradeLineBurst lineBurstPrefab = null!;
    [Tooltip("Where the squirrel sits in the frame during the upgrade, as a fraction of screen height from the bottom.")]
    [Range(0f, 0.5f)] [SerializeField] private float upgradeFrameHeight = 1f / 3f;

    private PlayerUpgradeGlow activeGlow;
    private UpgradeLineBurst activeBurst;
    private CameraClaim upgradeFrameClaim;

    private int segmentAcorns;
    private int segmentIndex;
    private bool processingSegment;

    public int SegmentAcorns => segmentAcorns;
    public int CurrentSegmentCost => segmentCosts.Length > 0
        ? segmentCosts[Mathf.Min(segmentIndex, segmentCosts.Length - 1)]
        : 1;

    /// <summary>Fires on every acorn collection that does not complete the segment.</summary>
    public event Action<int, int> OnSegmentChanged;

    // ── Player component refs ─────────────────────────────────────────────────

    private PlayerMoveManager playerMoveManager;
    private PlayerEffectsManager playerEffects;
    private PlayerCameraManager playerCamera;
    private PlayerSenses playerSenses;

    /// <summary>Fires after a stat upgrade is applied. Parameters: (stat, new level).</summary>
    public event Action<UpgradeStat, int> OnStatUpgraded;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicate, being destroyed

        for (int i = 0; i < StatCount; i++)
            statLevels[i] = PlayerPrefs.GetInt(SaveKey + i, 0);
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        PlayerStateManager.OnPlayerRegistered += HandlePlayerRegistered;
    }

    private void OnDisable()
    {
        PlayerStateManager.OnPlayerRegistered -= HandlePlayerRegistered;
    }

    private void Start()
    {
        // Catch a player that already came online before this persistent manager subscribed
        // (first scene). Subsequent scenes arrive via the event.
        if (Instance == this && PlayerStateManager.Instance != null)
            HandlePlayerRegistered(PlayerStateManager.Instance.playerGameObject);
    }

    // No longer a component on the player: bind to the current player body and re-apply persisted
    // upgrades so each new scene's player picks them up.
    private void HandlePlayerRegistered(GameObject player)
    {
        if (Instance != this || player == null) return;

        playerMoveManager = player.GetComponent<PlayerMoveManager>();
        playerEffects = player.GetComponent<PlayerEffectsManager>();
        playerCamera = player.GetComponentInChildren<PlayerCameraManager>();
        playerSenses = player.GetComponentInChildren<PlayerSenses>();
        ApplyToPlayer();
    }

    // ── Acorn / segment handling ──────────────────────────────────────────────

    /// <summary>
    /// Called by Collector when an acorn is picked up. Advances segment progress
    /// and triggers an upgrade choice when the segment is complete.
    /// </summary>
    public void NotifyAcornCollected(int amount)
    {
        if (amount <= 0) return;
        segmentAcorns += amount;
        HandleSegmentProgress().Forget();
    }

    private async UniTaskVoid HandleSegmentProgress()
    {
        if (processingSegment) return;
        processingSegment = true;

        try
        {
            int currentSegmentCost = CurrentSegmentCost;
            bool segmentComplete = segmentAcorns >= currentSegmentCost;

            // Lock before the bar fills, not after: the upgrade view follows straight on and the
            // player shouldn't be able to wander off while the bar animates.
            if (segmentComplete && playerMoveManager != null)
                playerMoveManager.DisableMove();

            await ViewManager.Instance.PushView(acornCollectionBarPrefab);

            if (segmentComplete)
            {
                segmentAcorns = 0;
                segmentIndex++;

                await StartUpgradeSelection();
            }
            else
            {
                OnSegmentChanged?.Invoke(segmentAcorns, currentSegmentCost);
            }
        }
        finally
        {
            processingSegment = false;
        }
    }

    public async UniTask StartUpgradeSelection()
    {
        playerMoveManager.DisableMove();
        ClaimUpgradeFrame();
        ViewManager.Instance.ClearViewsInstant();

        // The energy orb has just landed: the squirrel lights up as the world dims a little around it,
        // then the upgrade lines grow out of the glow.
        await UniTask.WhenAll(
            OverlayCameraController.Current.RequestPlayerDim(),
            BeginGlow());
        await GrowLines();

        UpgradeStat? chosen = null;
        if (activeBurst != null && activeBurst.HasChoice)
            chosen = await activeBurst.Choose(activeBurst.destroyCancellationToken);
        else
            Debug.LogWarning($"[{nameof(PlayerUpgradeManager)}] No upgrade could be offered (none assigned, or every stat locked or maxed).", this);

        await FinishUpgrade(chosen);
    }

    /// <summary>Applies a chosen stat and ends the upgrade. Kept for the old UpgradePlayerView until it is deleted.</summary>
    public async UniTask CompleteUpgradeSelection(UpgradeStat stat)
    {
        if (!CanAfford(stat)) return;

        await FinishUpgrade(stat);
    }

    private async UniTask FinishUpgrade(UpgradeStat? stat)
    {
        if (stat.HasValue && CanAfford(stat.Value))
            ApplyUpgrade(stat.Value);

        await ViewManager.Instance.ClearViews();
        await UniTask.WhenAll(
            EndLines(),
            EndGlow(),
            OverlayCameraController.Current.ReleasePlayerDim());
        ReleaseUpgradeFrame();

        if (playerMoveManager != null)
            playerMoveManager.EnableMove();
    }

    /// <summary>
    /// True when a stat should be offered in the upgrade choice: not maxed, and its ability is unlocked
    /// (Vision needs Zoom, Gliding needs Glide). Stats that are not offered get a broken line.
    /// </summary>
    public bool IsStatOffered(UpgradeStat stat)
    {
        if (IsMaxed(stat)) return false;

        PlayerAbilityManager.Abilities? required = stat switch
        {
            UpgradeStat.Vision => PlayerAbilityManager.Abilities.Zoom,
            UpgradeStat.Gliding => PlayerAbilityManager.Abilities.Glide,
            _ => null,
        };

        return required == null || PlayerAbilityManager.Instance.IsAbilityUnlocked(required.Value);
    }

    // Frames the camera above the squirrel so it sits low in the shot, leaving the upper frame for the
    // upgrade lines and icons. The zoom is held too, so the fan sized from it stays on screen.
    private void ClaimUpgradeFrame()
    {
        CameraDirector director = CameraDirector.For(playerMoveManager.gameObject.scene);
        if (director == null)
        {
            CameraRig.Current.SetTrackingTarget(playerMoveManager.transform);
            return;
        }

        float size = CameraRig.Current.Foreground.orthographicSize;
        float lift = (0.5f - upgradeFrameHeight) * 2f * size;

        upgradeFrameClaim = director.Request(CameraPriority.Cinematic)
            .WithTarget(playerMoveManager.transform)
            .WithOffset(Vector3.up * lift)
            .WithOrthographicSize(size);
    }

    private void ReleaseUpgradeFrame()
    {
        if (upgradeFrameClaim == null)
        {
            CameraRig.Current.ResetTrackingTarget();
            return;
        }

        upgradeFrameClaim.Release();
        upgradeFrameClaim = null;
    }

    private async UniTask BeginGlow()
    {
        if (upgradeGlowPrefab == null || playerEffects == null || playerEffects.GraphicSprite == null) return;

        activeGlow = Instantiate(upgradeGlowPrefab);
        await activeGlow.Begin(playerEffects.GraphicSprite, activeGlow.destroyCancellationToken);
    }

    private async UniTask EndGlow()
    {
        if (activeGlow == null) return;

        PlayerUpgradeGlow glow = activeGlow;
        activeGlow = null;
        await glow.End(glow.destroyCancellationToken);
    }

    private async UniTask GrowLines()
    {
        if (lineBurstPrefab == null || playerEffects == null || playerEffects.GraphicSprite == null) return;

        float size = upgradeFrameClaim?.OrthographicSize ?? CameraRig.Current.Foreground.orthographicSize;
        activeBurst = Instantiate(lineBurstPrefab);
        await activeBurst.Grow(playerEffects.GraphicSprite, size, IsStatOffered, activeBurst.destroyCancellationToken);
    }

    private async UniTask EndLines()
    {
        if (activeBurst == null) return;

        UpgradeLineBurst burst = activeBurst;
        activeBurst = null;
        await burst.End(burst.destroyCancellationToken);
    }

    // ── Upgrade affordability / application ───────────────────────────────────

    /// <summary>The acorn cost for the current segment.</summary>
    public int GetUpgradeCost() => CurrentSegmentCost;

    /// <summary>True when the stat is not maxed. Cost is already paid by completing the segment.</summary>
    public bool CanAfford(UpgradeStat stat) => !IsMaxed(stat);

    // ── Stat queries ──────────────────────────────────────────────────────────

    /// <summary>Current upgrade level for a stat (0 = base, no upgrades applied).</summary>
    public int GetLevel(UpgradeStat stat)
    {
        // Read through the debug override rather than copying it into statLevels, so the real saved
        // progress is never touched and switching the override off restores it exactly.
        if (DebugSettings.Instance != null && DebugSettings.Instance.OverrideUpgradeLevels)
            return Mathf.Clamp(DebugSettings.Instance.GetUpgradeLevel(stat), 0, GetMaxLevel(stat));

        return statLevels[(int)stat];
    }

    /// <summary>
    /// Highest reachable level for a stat. Shared across every stat; the per-stat lookup remains so
    /// a stat with nothing configured still reports 0 and is never offered as an upgrade.
    /// </summary>
    public int GetMaxLevel(UpgradeStat stat)
    {
        if (!TryGetUpgradeProgression(stat, out var upgradeProgression)
            || upgradeProgression.effects == null
            || upgradeProgression.effects.Length == 0)
            return 0;

        return MAXUPGRADELEVEL;
    }

    /// <summary>True when a stat has reached its final configured level.</summary>
    public bool IsMaxed(UpgradeStat stat) => GetLevel(stat) >= GetMaxLevel(stat);

    /// <summary>The stats that can still be upgraded (not yet maxed).</summary>
    public List<UpgradeStat> AvailableStats()
    {
        var result = new List<UpgradeStat>(StatCount);
        for (int i = 0; i < StatCount; i++)
        {
            var stat = (UpgradeStat)i;
            if (!IsMaxed(stat)) result.Add(stat);
        }
        return result;
    }

    /// <summary>The value the stat currently resolves to, given its level.</summary>
    public float GetCurrentValue(UpgradeStat stat)
    {
        if (!TryGetPrimaryProgression(stat, out var progression)) return 0f;
        return EvaluateProgression(progression, GetLevel(stat));
    }

    /// <summary>The value the stat would have if upgraded one more level, or the current value if maxed.</summary>
    public float GetNextValue(UpgradeStat stat)
    {
        if (!TryGetPrimaryProgression(stat, out var progression)) return 0f;
        return EvaluateProgression(progression, GetLevel(stat) + 1);
    }

    /// <summary>
    /// Advances a stat one upgrade level (no-op if maxed), persists, and applies the
    /// new value to the player. Cost is paid upstream by completing a segment.
    /// </summary>
    public void ApplyUpgrade(UpgradeStat stat)
    {
        if (!CanAfford(stat)) return;

        statLevels[(int)stat]++;
        Save();
        ApplyToPlayer();

        OnStatUpgraded?.Invoke(stat, statLevels[(int)stat]);
    }

    // ── Apply to player ───────────────────────────────────────────────────────

    /// <summary>
    /// Pushes every configured effect for every stat onto the relevant player component.
    /// Safe to call on load so persisted upgrades take effect each session.
    /// </summary>
    public void ApplyToPlayer()
    {
        // Resolve from the current player body (this manager is persistent and not on the player).
        if ((playerMoveManager == null || playerCamera == null || playerSenses == null) && PlayerStateManager.Instance != null)
        {
            GameObject player = PlayerStateManager.Instance.playerGameObject;
            if (player != null)
            {
                if (playerMoveManager == null) playerMoveManager = player.GetComponent<PlayerMoveManager>();
                if (playerCamera == null) playerCamera = player.GetComponentInChildren<PlayerCameraManager>();
                if (playerSenses == null) playerSenses = player.GetComponentInChildren<PlayerSenses>();
            }
        }

        if (upgradeProgressions == null) return;

        for (int i = 0; i < upgradeProgressions.Length; i++)
        {
            var upgradeProgression = upgradeProgressions[i];
            if (upgradeProgression.effects == null) continue;

            int level = GetLevel(upgradeProgression.stat);
            for (int j = 0; j < upgradeProgression.effects.Length; j++)
            {
                var effect = upgradeProgression.effects[j];
                float value = EvaluateProgression(effect.progression, level);
                ApplyEffect(effect.target, value);
            }
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private bool TryGetPrimaryProgression(UpgradeStat stat, out ValueProgression progression)
    {
        if (TryGetUpgradeProgression(stat, out var upgradeProgression)
            && upgradeProgression.effects != null
            && upgradeProgression.effects.Length > 0)
        {
            progression = upgradeProgression.effects[0].progression;
            return true;
        }

        progression = default;
        return false;
    }

    private bool TryGetUpgradeProgression(UpgradeStat stat, out UpgradeProgression progression)
    {
        if (upgradeProgressions == null)
        {
            progression = default;
            return false;
        }

        for (int i = 0; i < upgradeProgressions.Length; i++)
        {
            if (upgradeProgressions[i].stat == stat)
            {
                progression = upgradeProgressions[i];
                return true;
            }
        }

        progression = default;
        return false;
    }

    private void ApplyEffect(UpgradeEffectTarget target, float value)
    {
        switch (target)
        {
            case UpgradeEffectTarget.MaxClimbTime:
                if (playerMoveManager != null) playerMoveManager.SetMaxClimbTime(value);
                break;
            case UpgradeEffectTarget.ClimbSpeed:
                if (playerMoveManager != null) playerMoveManager.SetClimbSpeed(value);
                break;
            case UpgradeEffectTarget.MaxGlideSpeed:
                if (playerMoveManager != null) playerMoveManager.SetMaxGlideSpeed(value);
                break;
            case UpgradeEffectTarget.EnergyShiftSpeed:
                if (playerMoveManager != null) playerMoveManager.SetEnergyShiftSpeed(value);
                break;
            case UpgradeEffectTarget.ZoomOutAmount:
                if (playerCamera != null) playerCamera.SetZoomOutAmount(value);
                break;
            case UpgradeEffectTarget.MaxVelocityZoom:
                if (playerCamera != null) playerCamera.SetMaxVelocityZoom(value);
                break;
            case UpgradeEffectTarget.MinimumZoom:
                if (playerCamera != null) playerCamera.SetMinimumZoom(value);
                break;
            case UpgradeEffectTarget.ScentTrailLength:
                if (playerSenses != null) playerSenses.SetFullTrailLength(value);
                break;
            case UpgradeEffectTarget.ScentDetectionRange:
                if (playerSenses != null) playerSenses.SetDetectionRange(value);
                break;
        }
    }

    private static float EvaluateProgression(ValueProgression progression, int level)
    {
        float progress = Mathf.Clamp01((float)level / MAXUPGRADELEVEL);
        if (progression.interpolationCurve != null && progression.interpolationCurve.length > 0)
            progress = Mathf.Clamp01(progression.interpolationCurve.Evaluate(progress));

        return Mathf.Lerp(progression.minValue, progression.maxValue, progress);
    }

    private void Save()
    {
        if (DebugSettings.Instance != null && DebugSettings.Instance.DisablePersistence)
            return;

        for (int i = 0; i < StatCount; i++)
            PlayerPrefs.SetInt(SaveKey + i, statLevels[i]);
        PlayerPrefs.Save();
    }

    [Button("Reset Upgrades")]
    private void ResetUpgrades()
    {
        for (int i = 0; i < StatCount; i++)
            statLevels[i] = 0;
        Save();
        ApplyToPlayer();
    }
}
}
