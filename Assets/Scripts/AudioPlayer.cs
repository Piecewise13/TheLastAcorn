using UnityEngine;
using System.Collections;

/// <summary>
/// Play an AudioClip with optional delay, fade-in, per-play randomisation,
/// looping, and runtime fade-out.
/// </summary>
public class AudioPlayer : MonoBehaviour
{
    //  Audio clip & base values
    [Header("Audio Settings")]
    [Tooltip("Audio clip to be played.")]
    public AudioClip audioClip;

    [Range(0f, 1f)] public float volume = 1f;
    [Range(-3f, 3f)] public float pitch = 1f;
    public bool loop = false;

    //  Randomisation
    [Header("Randomisation")]
    public bool randomise = false;
    public Vector2 volumeRange = new Vector2(0.9f, 1.0f);
    public Vector2 pitchRange  = new Vector2(0.95f, 1.05f);

    //  Fade-in
    [Header("Fade-in")]
    public bool fadeIn = false;
    public float fadeInDuration = 1f;

    //  Delay
    [Header("Delay")]
    public bool playWithDelay = false;
    public float delayDuration = 0f;

    //  Misc
    public bool playOnEnable = false;

    //  Internal
    private AudioSource currentSource;

    // Public Methods
    public void Play()
    {
        if (audioClip == null)
        {
            Debug.LogError("AudioPlayer: No audio clip assigned.");
            return;
        }
        StartCoroutine(PlayAudioCoroutine());
    }

    public void FadeOut(float duration = 1f)
    {
        if (currentSource != null)
            StartCoroutine(FadeOutCoroutine(currentSource, duration));
    }

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    // Coroutines
    private IEnumerator PlayAudioCoroutine()
    {
        if (playWithDelay && delayDuration > 0f)
            yield return new WaitForSeconds(delayDuration);

        float chosenVolume = volume;
        float chosenPitch  = pitch;

        if (randomise)
        {
            float volMin = Mathf.Min(volumeRange.x, volumeRange.y);
            float volMax = Mathf.Max(volumeRange.x, volumeRange.y);
            float pitMin = Mathf.Min(pitchRange.x,  pitchRange.y);
            float pitMax = Mathf.Max(pitchRange.x,  pitchRange.y);

            chosenVolume = Random.Range(volMin, volMax);
            chosenPitch  = Random.Range(pitMin, pitMax);
        }

        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.clip = audioClip;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.pitch = chosenPitch;
        src.loop = loop;
        src.volume = (fadeIn && fadeInDuration > 0f) ? 0f : chosenVolume;
        currentSource = src;

        src.Play();

        if (fadeIn && fadeInDuration > 0f)
            StartCoroutine(FadeIn(src, chosenVolume, fadeInDuration));

        if (!loop)
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

    private static IEnumerator FadeOutCoroutine(AudioSource src, float duration)
    {
        float startVol = src.volume;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (src == null) yield break;
            src.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }
        if (src == null) yield break;
        src.volume = 0f;
        src.Stop();
        Object.Destroy(src);
    }

    private static IEnumerator RemoveAudioSourceAfterPlay(AudioSource src, float clipLen)
    {
        yield return new WaitForSeconds(clipLen + 0.1f);
        Object.Destroy(src);
    }
}
