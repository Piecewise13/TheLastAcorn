using UnityEngine;
using System;

/// <summary>
/// Manages in-game time progression. Supports adjustable time speed, day/night cycle,
/// and broadcasting time updates to UI or other systems.
/// </summary>
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("Length of one in-game day in real seconds.")]
    [SerializeField] private float secondsPerDay = 120f;

    [Tooltip("Starting time of day, in [0,1], where 0 = midnight, 0.5 = noon.")]
    [Range(0f, 1f)]
    [SerializeField] private float startTimeOfDay = 0f;

    [Tooltip("Multiplier to speed up or slow down time.")]
    [SerializeField] private float timeScale = 1f;

    private float timeOfDay;  
    private float elapsedSeconds;

    /// <summary>
    /// Raised every frame with the current timeOfDay [0,1].
    /// </summary>
    public event Action<float> OnTimeChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        timeOfDay = Mathf.Clamp01(startTimeOfDay);
        elapsedSeconds = timeOfDay * secondsPerDay;
    }

    private void Update()
    {
        // Advance time
        elapsedSeconds += Time.deltaTime * timeScale;
        if (elapsedSeconds >= secondsPerDay)
            elapsedSeconds -= secondsPerDay;  // loop back

        timeOfDay = elapsedSeconds / secondsPerDay;
        OnTimeChanged?.Invoke(timeOfDay);

        // Example hooks to flesh out later:
        // UpdateLighting(timeOfDay);
        // UpdateUI(timeOfDay);
    }

    /// <summary>
    /// Provides hour and minute (24h format) based on timeOfDay.
    /// </summary>
    public void GetClockTime(out int hour, out int minute)
    {
        float totalHours = timeOfDay * 24f;
        hour = Mathf.FloorToInt(totalHours);
        minute = Mathf.FloorToInt((totalHours - hour) * 60f);
    }

    #region Public API

    /// <summary>Adjust the flow of time.</summary>
    public void SetTimeScale(float newScale)
    {
        timeScale = Mathf.Max(0f, newScale);
    }

    /// <summary>Jump instantly to a specific normalized time [0,1].</summary>
    public void JumpToTime(float normalizedTime)
    {
        timeOfDay = Mathf.Clamp01(normalizedTime);
        elapsedSeconds = timeOfDay * secondsPerDay;
        OnTimeChanged?.Invoke(timeOfDay);
    }

    #endregion

    #region Example Helpers

    // Customize these when your lighting or UI needs are defined:
    //
    // private void UpdateLighting(float t)
    // {
    //     // e.g. change directional light intensity/color
    // }
    //
    // private void UpdateUI(float t)
    // {
    //     // e.g. format a clock string and update a Text component
    // }

    #endregion
}
