using UnityEngine;
using System.Collections;

public class AudioPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioClip audioClip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(-3f, 3f)] public float pitch = 1f;
    public bool loop = false;

    [Header("Randomisation")]
    public bool randomise = false;
    public Vector2 volumeRange = new Vector2(0.9f, 1f);
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    public AudioClip[] randomClips;

    [Header("Fade-in")]
    public bool fadeIn = false;
    public float fadeInSeconds = 1f;

    [Header("Delay")]
    public bool playWithDelay = false;
    public float delaySeconds = 0f;

    [Header("Fade Curves")]
    public FadeCurve fadeInCurve = FadeCurve.Smooth;
    public FadeCurve fadeOutCurve = FadeCurve.Smooth;

    [Header("3D Settings")]
    public bool spatialBlend = false;

    public bool playOnEnable = false;

    AudioSource currentSource;

    public enum FadeCurve { Linear, Smooth, Smoother, Exponential, Logarithmic }

    public void Play() => StartCoroutine(PlayAudio());

    public void FadeOut(float seconds = 1f)
    {
        if (currentSource == null) return;
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(currentSource, currentSource.volume, 0f, seconds, fadeOutCurve, () =>
        {
            currentSource.Stop();
            currentSource.volume = 0f;
        }));
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    IEnumerator PlayAudio()
    {
        if (audioClip == null && (randomClips == null || randomClips.Length == 0))
        {
            Debug.LogError("AudioPlayer: No audio clip assigned.");
            yield break;
        }

        if (playWithDelay && delaySeconds > 0f) yield return new WaitForSeconds(delaySeconds);

        float chosenVol = randomise ? Random.Range(volumeRange.x, volumeRange.y) : volume;
        float chosenPitch = randomise ? Random.Range(pitchRange.x, pitchRange.y) : pitch;

        AudioClip clip = (randomise && randomClips != null && randomClips.Length > 0)
            ? randomClips[Random.Range(0, randomClips.Length)]
            : audioClip;

        if (currentSource == null) currentSource = gameObject.AddComponent<AudioSource>();

        currentSource.clip = clip;
        currentSource.playOnAwake = false;
        currentSource.spatialBlend = spatialBlend ? 1f : 0f;
        currentSource.loop = loop;
        currentSource.pitch = chosenPitch;
        currentSource.volume = (fadeIn && fadeInSeconds > 0f) ? 0f : chosenVol;

        currentSource.Play();

        if (fadeIn && fadeInSeconds > 0f) yield return FadeRoutine(currentSource, 0f, chosenVol, fadeInSeconds, fadeInCurve);

        if (!loop && clip != null) StartCoroutine(StopAfterPlay(clip.length));
    }

    IEnumerator FadeRoutine(AudioSource src, float from, float to, float seconds, FadeCurve curve, System.Action onDone = null)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (src == null) yield break;
            float x = t / seconds;
            src.volume = Mathf.Lerp(from, to, ApplyCurve(x, curve));
            yield return null;
        }
        if (src != null) src.volume = to;
        onDone?.Invoke();
    }

    IEnumerator StopAfterPlay(float clipLen)
    {
        yield return new WaitForSeconds(clipLen + 0.1f);
        if (currentSource != null) currentSource.Stop();
    }

    static float ApplyCurve(float x, FadeCurve curve)
    {
        switch (curve)
        {
            case FadeCurve.Linear: return x;
            case FadeCurve.Smooth: return x * x * (3f - 2f * x);
            case FadeCurve.Smoother: return x * x * x * (x * (x * 6f - 15f) + 10f);
            case FadeCurve.Exponential: return 1f - Mathf.Pow(2f, -10f * x);
            case FadeCurve.Logarithmic: return Mathf.Pow(10f, (x - 1f) * 3f);
            default: return x;
        }
    }
}
