using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// Owns the player's tiered stat upgrades (climb time, zoom-out range, max glide speed).
/// Acorns drive these upgrades via the upgrade-choice flow; this manager stores the
/// chosen levels, persists them, and applies the resulting tier values to the player.
/// </summary>
public class PlayerUpgradeManager : MonoBehaviour
{
    public static PlayerUpgradeManager Instance { get; private set; }

    public enum UpgradeStat { ClimbTime, ZoomOut, GlideSpeed }

    [Serializable]
    struct StatTiers
    {
        public UpgradeStat stat;

        [Tooltip("Ordered tier values. Index 0 is the base value (level 0); each subsequent entry is the value granted at that upgrade level.")]
        public float[] tierValues;
    }

    [SerializeField]
    private StatTiers[] statTiers = new StatTiers[]
    {
        new StatTiers { stat = UpgradeStat.ClimbTime,  tierValues = new float[] { 0f } },
        new StatTiers { stat = UpgradeStat.ZoomOut,    tierValues = new float[] { 0f } },
        new StatTiers { stat = UpgradeStat.GlideSpeed, tierValues = new float[] { 0f } },
    };

    private static readonly int StatCount = Enum.GetValues(typeof(UpgradeStat)).Length;

    private readonly int[] statLevels = new int[StatCount];

    private const string SaveKey = "UpgradeLevel";

    private PlayerMove playerMove;
    private PlayerCameraManager playerCamera;

    /// <summary>Fires after a stat upgrade is applied. Parameters: (stat, new level).</summary>
    public event Action<UpgradeStat, int> OnStatUpgraded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < StatCount; i++)
            statLevels[i] = PlayerPrefs.GetInt(SaveKey + i, 0);
    }

    private void Start()
    {
        playerMove = GetComponent<PlayerMove>();
        playerCamera = GetComponentInChildren<PlayerCameraManager>();

        ApplyToPlayer();
    }

    /// <summary>Current upgrade level for a stat (0 = base, no upgrades applied).</summary>
    public int GetLevel(UpgradeStat stat) => statLevels[(int)stat];

    /// <summary>Highest reachable level for a stat (number of configured tiers minus the base).</summary>
    public int GetMaxLevel(UpgradeStat stat)
    {
        float[] tiers = GetTierValues(stat);
        return tiers != null && tiers.Length > 0 ? tiers.Length - 1 : 0;
    }

    /// <summary>True when a stat has reached its final configured tier.</summary>
    public bool IsMaxed(UpgradeStat stat) => GetLevel(stat) >= GetMaxLevel(stat);

    /// <summary>
    /// The acorn cost for any upgrade — equal to the current segment cost in
    /// PlayerAbilityManager so all upgrades share the same price.
    /// </summary>
    public int GetUpgradeCost() =>
        PlayerAbilityManager.Instance != null ? PlayerAbilityManager.Instance.CurrentSegmentCost : 0;

    /// <summary>True when the player has enough acorns and the stat is not maxed.</summary>
    public bool CanAfford(UpgradeStat stat)
    {
        if (IsMaxed(stat)) return false;
        int cost = GetUpgradeCost();
        return cost > 0 && ScoreManager.Instance != null && ScoreManager.Instance.CurrentScore >= cost;
    }

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

    /// <summary>The tier value the stat currently resolves to, given its level.</summary>
    public float GetCurrentValue(UpgradeStat stat)
    {
        float[] tiers = GetTierValues(stat);
        if (tiers == null || tiers.Length == 0) return 0f;
        int level = Mathf.Clamp(GetLevel(stat), 0, tiers.Length - 1);
        return tiers[level];
    }

    /// <summary>The value the stat would have if upgraded one more level, or the current value if maxed.</summary>
    public float GetNextValue(UpgradeStat stat)
    {
        float[] tiers = GetTierValues(stat);
        if (tiers == null || tiers.Length == 0) return 0f;
        int next = Mathf.Clamp(GetLevel(stat) + 1, 0, tiers.Length - 1);
        return tiers[next];
    }

    /// <summary>
    /// Advances a stat one upgrade level (no-op if maxed or unaffordable), deducts
    /// one segment's worth of acorns from ScoreManager, persists, and applies the
    /// new value to the player.
    /// </summary>
    public void ApplyUpgrade(UpgradeStat stat)
    {
        if (!CanAfford(stat)) return;

        int cost = GetUpgradeCost();
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(-cost);

        statLevels[(int)stat]++;
        Save();
        ApplyToPlayer();

        OnStatUpgraded?.Invoke(stat, statLevels[(int)stat]);
    }

    /// <summary>
    /// Pushes the current tier value of every stat onto the relevant player component.
    /// Safe to call on load so persisted upgrades take effect each session.
    /// </summary>
    public void ApplyToPlayer()
    {
        if (playerMove == null) playerMove = GetComponent<PlayerMove>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<PlayerCameraManager>();

        if (playerMove != null)
        {
            if (HasTiers(UpgradeStat.ClimbTime))
                playerMove.SetMaxClimbTime(GetCurrentValue(UpgradeStat.ClimbTime));
            if (HasTiers(UpgradeStat.GlideSpeed))
                playerMove.SetMaxGlideSpeed(GetCurrentValue(UpgradeStat.GlideSpeed));
        }

        if (playerCamera != null && HasTiers(UpgradeStat.ZoomOut))
        {
            playerCamera.SetZoomOutAmount(GetCurrentValue(UpgradeStat.ZoomOut));
        }
    }

    private bool HasTiers(UpgradeStat stat)
    {
        float[] tiers = GetTierValues(stat);
        return tiers != null && tiers.Length > 0;
    }

    private float[] GetTierValues(UpgradeStat stat)
    {
        for (int i = 0; i < statTiers.Length; i++)
            if (statTiers[i].stat == stat) return statTiers[i].tierValues;
        return null;
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
