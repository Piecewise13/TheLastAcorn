using NaughtyAttributes;
using UnityEngine;

namespace Player
{
/// <summary>
/// The player's sense of smell. Once the zoom (senses) ability is unlocked, nearby acorns always
/// show a scent trail toward the player: a short one (<see cref="passiveLengthFraction"/> of the
/// full length) normally, and the full length while the zoom-out is held. How far away an acorn can
/// be sensed (<see cref="detectionRange"/>) and the full length are both set by the Vision upgrade.
/// The trails themselves live in the world, managed by
/// <see cref="ScentTrailManager"/>, not on the player.
/// </summary>
public class PlayerSenses : MonoBehaviour
{
    [Tooltip("The player's Graphics transform the trails point at. Falls back to the root transform if unset.")]
    [SerializeField] private Transform graphicsOrigin;

    [Tooltip("Distance in world units from the player within which acorns get a scent trail. " +
             "Overwritten by the Vision upgrade (UpgradeEffectTarget.ScentDetectionRange).")]
    [SerializeField] private float detectionRange = 40f;

    [Tooltip("Trail length in world units while sensing, measured from the acorn toward the player. " +
             "Overwritten by the Vision upgrade (UpgradeEffectTarget.ScentTrailLength).")]
    [SerializeField] private float fullTrailLength = 25f;

    [Tooltip("Fraction of the full length the trails reach while not sensing. 0.25 = a quarter.")]
    [SerializeField, Range(0f, 1f)] private float passiveLengthFraction = 0.25f;

    [Tooltip("Seconds between rescans for acorns entering or leaving range.")]
    [SerializeField] private float refreshInterval = 0.25f;

    private PlayerCameraManager playerCamera;
    private bool sensing;
    private float nextRefreshTime;
    private bool warnedMissingManager;

    /// <summary>Where scent trails point: the player's graphics.</summary>
    public Transform TrailOrigin => graphicsOrigin;

    private void Awake()
    {
        if (graphicsOrigin == null)
        {
            graphicsOrigin = transform.root;
        }
    }

    private void Start()
    {
        playerCamera = GetComponentInParent<PlayerCameraManager>();

        if (playerCamera != null)
        {
            playerCamera.OnZoomStarted += HandleZoomStarted;
            playerCamera.OnZoomEnded += HandleZoomEnded;
        }
        else
        {
            Debug.LogError("PlayerSenses: no PlayerCameraManager found in parents.", this);
        }
    }

    private void OnDestroy()
    {
        if (playerCamera != null)
        {
            playerCamera.OnZoomStarted -= HandleZoomStarted;
            playerCamera.OnZoomEnded -= HandleZoomEnded;
        }

        HideTrails();
    }

    private void Update()
    {
        if (Time.time < nextRefreshTime) return;
        RefreshTrails();
    }

    private void HandleZoomStarted()
    {
        sensing = true;
        RefreshTrails();
    }

    private void HandleZoomEnded()
    {
        sensing = false;
        RefreshTrails();
    }

    /// <summary>Sets how far from the player acorns can be sensed. Called by the Vision upgrade.</summary>
    public void SetDetectionRange(float worldUnits)
    {
        detectionRange = Mathf.Max(0f, worldUnits);
        RefreshTrails();
    }

    /// <summary>Sets the trail length while sensing. Called by the Vision upgrade.</summary>
    public void SetFullTrailLength(float worldUnits)
    {
        fullTrailLength = worldUnits;
        RefreshTrails();
    }

    private float CurrentTrailLength => sensing ? fullTrailLength : fullTrailLength * passiveLengthFraction;

    private void RefreshTrails()
    {
        nextRefreshTime = Time.time + refreshInterval;

        // No scent until the senses are taught. Covers the debug unlock-all toggle too.
        PlayerAbilityManager abilities = PlayerAbilityManager.Instance;
        if (abilities == null || !abilities.IsAbilityUnlocked(PlayerAbilityManager.Abilities.Zoom))
        {
            HideTrails();
            return;
        }

        if (ScentTrailManager.Instance == null)
        {
            if (warnedMissingManager) return;
            warnedMissingManager = true;
            Debug.LogWarning("PlayerSenses: no ScentTrailManager in the session; is it in GameManager's persistent list?", this);
            return;
        }

        ScentTrailManager.Instance.RefreshTrails(graphicsOrigin, detectionRange, CurrentTrailLength);
    }

    private void HideTrails()
    {
        if (ScentTrailManager.Instance != null)
        {
            ScentTrailManager.Instance.HideTrails();
        }
    }

    [Button("Refresh Trails (Debug)")]
    private void DebugRefreshTrails()
    {
        RefreshTrails();
    }

    [Button("Clear Trails (Debug)")]
    private void DebugClearTrails()
    {
        HideTrails();
    }
}
}
