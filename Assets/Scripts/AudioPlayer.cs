using UnityEngine;
using System.Collections;

public class AudioPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Audio clip to be played.")]
    public AudioClip audioClip;
    
    [Tooltip("Playback volume (0 = silent, 1 = full volume).")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Playback pitch (0.5 = half speed, 1 = normal speed, 2 = double speed, etc.).")]
    [Range(0.5f, 3f)]
    public float pitch = 1f;
    
    [Header("Fade Settings")]
    [Tooltip("Check this box to fade in the audio from 0 to the target volume.")]
    public bool fadeIn = false;  // Set to false by default
    
    [Tooltip("Time (in seconds) to fade in from 0 to the specified volume.")]
    public float fadeInDuration = 1f;

    [Header("Delay Settings")]
    [Tooltip("Check this box to delay the start of the audio playback.")]
    public bool playWithDelay = false; // false by default

    [Tooltip("Time (in seconds) to delay the start of the audio playback.")]
    public float delayDuration = 0f;

    [Tooltip("Plays sound on enable")]
    public bool playOnEnable = false;

    /// <summary>
    /// Public method to play the audio clip. Instantiates an AudioSource component,
    /// optionally delays the start of playback, optionally fades in from 0 to the specified 
    /// volume, and removes the AudioSource after the clip finishes playing.
    /// </summary>
    public void Play()
    {
        if (audioClip == null)
        {
            Debug.LogError("AudioPlayer: No audio clip assigned.");
            return;
        }

        // Start a coroutine to handle delay, playback, fade in, and cleanup
        StartCoroutine(PlayAudioCoroutine());
    }

    public void OnEnable(){
        if(playOnEnable) Play();
    }

    /// <summary>
    /// Coroutine that optionally delays playback, plays the audio, optionally fades in,
    /// and schedules the removal of the AudioSource after the clip finishes.
    /// </summary>
    private IEnumerator PlayAudioCoroutine()
    {
        // If 'playWithDelay' is true and a valid delay is set, wait before playing.
        if (playWithDelay && delayDuration > 0f)
        {
            yield return new WaitForSeconds(delayDuration);
        }
        
        // Instantiate and configure the AudioSource component.
        AudioSource audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.pitch = pitch;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound (0 = 2D, 1 = 3D)
        
        // If fadeIn is true and duration > 0, start at 0 volume; otherwise, use the specified volume.
        audioSource.volume = (fadeIn && fadeInDuration > 0f) ? 0f : volume;
        
        // Start playback.
        audioSource.Play();

        // Start fade in if enabled.
        if (fadeIn && fadeInDuration > 0f)
        {
            StartCoroutine(FadeIn(audioSource, volume, fadeInDuration));
        }
        
        // Schedule removal of the AudioSource after it finishes playing.
        StartCoroutine(RemoveAudioSourceAfterPlay(audioSource, audioClip.length));
    }

    /// <summary>
    /// Coroutine that gradually increases the audio source's volume from 0 to the specified target volume
    /// over the given duration.
    /// </summary>
    private IEnumerator FadeIn(AudioSource source, float targetVolume, float duration)
    {
        float currentTime = 0f;
        while (currentTime < duration)
        {
            currentTime += Time.deltaTime;
            float newVolume = Mathf.Lerp(0f, targetVolume, currentTime / duration);
            source.volume = newVolume;
            yield return null;
        }
        source.volume = targetVolume;
    }

    /// <summary>
    /// Waits until the audio clip has finished playing, then removes the AudioSource component.
    /// </summary>
    /// <param name="audioSource">The AudioSource component to remove.</param>
    /// <param name="clipLength">Length of the audio clip in seconds.</param>
    private IEnumerator RemoveAudioSourceAfterPlay(AudioSource audioSource, float clipLength)
    {
        // Wait a little longer than the clip length to ensure complete playback.
        yield return new WaitForSeconds(clipLength + 0.1f);
        Destroy(audioSource);
    }
}
