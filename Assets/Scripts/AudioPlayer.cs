using UnityEngine;
using System.Collections;

/// <summary>
/// Play an AudioClip with optional delay, fade-in, and per-play randomisation
/// of volume and pitch.
/// </summary>
public class AudioPlayer : MonoBehaviour
{
    //  Audio clip & base values
    [Header("Audio Settings")]
    [Tooltip("Audio clip to be played.")]
    public AudioClip audioClip;

    [Tooltip("Base playback volume (0 = silent, 1 = full volume).")]
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("Base playback pitch (0.5 = half speed, 1 = normal, 2 = double, …).")]
    [Range(0.5f, 3f)] public float pitch = 1f;


    //  Randomisation
    [Header("Randomisation")]
    [Tooltip("Enable to randomise volume & pitch each time Play() is called.")]
    public bool randomise = false;

    [Tooltip("Volume range [min, max] when randomise is on.")]
    public Vector2 volumeRange = new Vector2(0.9f, 1.0f);   // x = min, y = max

    [Tooltip("Pitch range [min, max] when randomise is on.")]
    public Vector2 pitchRange  = new Vector2(0.95f, 1.05f); // x = min, y = max

    //  Fade-in
    [Header("Fade-in")]
    [Tooltip("Fade in the audio from 0 → target volume.")]
    public bool fadeIn = false;

    [Tooltip("Seconds for fade-in.")]
    public float fadeInDuration = 1f;

    //  Delay
    [Header("Delay")]
    [Tooltip("Delay the start of playback.")]
    public bool playWithDelay = false;

    [Tooltip("Seconds to delay playback.")]
    public float delayDuration = 0f;

    //  Misc
    [Tooltip("Automatically play when the GameObject is enabled.")]
    public bool playOnEnable = false;

    //  Public API
    public void Play()
    {
        if (audioClip == null)
        {
            Debug.LogError("AudioPlayer: No audio clip assigned.");
            return;
        }

        StartCoroutine(PlayAudioCoroutine());
    }

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    //  Internals
    private IEnumerator PlayAudioCoroutine()
    {
        // Optional delay
        if (playWithDelay && delayDuration > 0f)
            yield return new WaitForSeconds(delayDuration);

        // Figure out per-play volume & pitch
        float chosenVolume = volume;
        float chosenPitch  = pitch;

        if (randomise)
        {
            // Clamp to sane ranges in case inspector values inverted
            float volMin = Mathf.Min(volumeRange.x, volumeRange.y);
            float volMax = Mathf.Max(volumeRange.x, volumeRange.y);
            float pitMin = Mathf.Min(pitchRange.x,  pitchRange.y);
            float pitMax = Mathf.Max(pitchRange.x,  pitchRange.y);

            chosenVolume = Random.Range(volMin, volMax);
            chosenPitch  = Random.Range(pitMin, pitMax);
        }

        // Create & configure AudioSource
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.clip         = audioClip;
        src.playOnAwake  = false;
        src.spatialBlend = 0f;          // 2D audio
        src.pitch        = chosenPitch;
        src.volume       = (fadeIn && fadeInDuration > 0f) ? 0f : chosenVolume;

        // Play
        src.Play();

        // Optional fade-in
        if (fadeIn && fadeInDuration > 0f)
            StartCoroutine(FadeIn(src, chosenVolume, fadeInDuration));

        // Cleanup
        StartCoroutine(RemoveAudioSourceAfterPlay(src, audioClip.length));
    }

    private static IEnumerator FadeIn(AudioSource src, float targetVol, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            src.volume = Mathf.Lerp(0f, targetVol, t / duration);
            yield return null;
        }
        src.volume = targetVol;
    }

    private static IEnumerator RemoveAudioSourceAfterPlay(AudioSource src, float clipLen)
    {
        yield return new WaitForSeconds(clipLen + 0.1f);
        Destroy(src);
    }
}
