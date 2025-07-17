using UnityEngine;
using System.Collections;

/// <summary>
/// Controls background-music layers as acorns are collected.
/// </summary>
public class AudioSceneManager : MonoBehaviour
{
    //  Audio players
    [Header("Layers")]
    public AudioPlayer ambiencePlayer;
    public AudioPlayer dronePlayer;
    [Tooltip("acornLayers[0] plays at 1st acorn, acornLayers[1] at 2nd, etc. Up to 5.")]
    public AudioPlayer[] acornLayers = new AudioPlayer[5];

    [Header("Fades")]
    [Range(0, 5)] [SerializeField] private float fadeTime = 2.5f;
    [Tooltip("If true, the previously-played acorn layer fades out when a new one starts.")]
    public bool fadePrevious = true;

    //  Progress tracking
    int targetScore;
    bool[] layerPlayed;
    int lastLayerIndex = -1;

    void Awake()
    {
        targetScore = FindFirstObjectByType<ScoreManager>()?.GetMaxScore() ?? 0;

        int layerCount = Mathf.Clamp(acornLayers.Length, 0, 5);
        layerPlayed = new bool[layerCount];

        if (ambiencePlayer != null) ambiencePlayer.Play();
        if (dronePlayer != null) dronePlayer.Play();
    }

    void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    void OnDisable()
    {
        if (LevelScoreManager.Instance != null)
            LevelScoreManager.Instance.OnLevelScoreChanged -= HandleScoreChanged;
    }

    IEnumerator SubscribeWhenReady()
    {
        while (LevelScoreManager.Instance == null) yield return null;
        LevelScoreManager.Instance.OnLevelScoreChanged += HandleScoreChanged;
        HandleScoreChanged(LevelScoreManager.Instance.CurrentLevelScore);
    }

    void HandleScoreChanged(int newScore)
    {
        for (int i = 0; i < layerPlayed.Length; i++)
        {
            int trigger = i + 1;
            if (!layerPlayed[i] && newScore >= trigger)
            {
                if (fadePrevious && lastLayerIndex >= 0 && acornLayers[lastLayerIndex] != null)
                    acornLayers[lastLayerIndex].FadeOut(fadeTime);

                if (acornLayers[i] != null) acornLayers[i].Play();
                layerPlayed[i] = true;
                lastLayerIndex = i;
            }
        }
    }

    public void FadeOutAudio()
    {
        if (ambiencePlayer != null) ambiencePlayer.FadeOut(fadeTime);
        if (dronePlayer    != null) dronePlayer.FadeOut(fadeTime);
        foreach (var p in acornLayers)
            if (p != null) p.FadeOut(fadeTime);
    }
}
