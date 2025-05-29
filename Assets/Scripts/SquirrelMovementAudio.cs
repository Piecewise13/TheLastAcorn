using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SquirrelMovementAudio : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private PlayerMove  player;
    [SerializeField] private Rigidbody2D rb;

    [System.Serializable]
    public struct StateAudio
    {
        public AudioClip clip;
        public Vector2 speedRange;
        public Vector2 volumeRange;
        public Vector2 pitchRange;
    }

    [Header("Grounded")]
    [SerializeField] private StateAudio grounded = new()
    {
        speedRange = new(0.2f, 6f),
        volumeRange = new(0f, 0.8f),
        pitchRange = new(0.95f, 1.15f)
    };

    [Header("Climb (constant)")]
    [SerializeField] private StateAudio climb = new()
    {
        volumeRange = new(0.7f, 0.7f),
        pitchRange = new(1f, 1f)
    };

    [Header("Glide")]
    [SerializeField] private StateAudio glide = new()
    {
        speedRange = new(2f, 12f),
        volumeRange = new(0f, 1f),
        pitchRange = new(0.95f, 1.15f)
    };

    [Header("Fall")]
    [SerializeField] private StateAudio fall = new()
    {
        speedRange = new(2f, 15f),
        volumeRange = new(0f, 1f),
        pitchRange = new(0.95f, 1.15f)
    };

    [Header("Stunned (constant)")]
    [SerializeField] private StateAudio stunned = new()
    {
        volumeRange = new(1f, 1f),
        pitchRange = new(1f, 1f)
    };

    [SerializeField] private bool enablePitchBend = true;

    private AudioSource src;
    private PlayerState lastState = (PlayerState)(-1);

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;

        if (!player) player = GetComponentInParent<PlayerMove>();
        if (!rb) rb = player.GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        var state = player.GetPlayerState();

        if (state != lastState)
        {
            lastState = state;
            src.clip  = GetSettings(state).clip;
            if (src.clip) src.Play(); else src.Stop();
        }

        if (!src.isPlaying) return;

        if (state == PlayerState.Climb || state == PlayerState.STUNNED)
        {
            var s = GetSettings(state);
            src.volume = s.volumeRange.y;
            src.pitch = s.pitchRange.y;
            return;
        }

        var set   = GetSettings(state);
        float vel = state == PlayerState.Fall ? Mathf.Abs(rb.linearVelocity.y)
                                              : Mathf.Abs(rb.linearVelocity.x);

        float vol = Remap(vel, set.speedRange, set.volumeRange);
        src.volume = vol;

        if (enablePitchBend)
            src.pitch = Remap(vol, set.volumeRange, set.pitchRange);
    }

    StateAudio GetSettings(PlayerState s) => s switch
    {
        PlayerState.Grounded => grounded,
        PlayerState.Climb => climb,
        PlayerState.Glide => glide,
        PlayerState.Fall => fall,
        PlayerState.STUNNED => stunned,
        _                    => default
    };

    static float Remap(float v, Vector2 inRange, Vector2 outRange)
    {
        float t = Mathf.InverseLerp(inRange.x, inRange.y, v);
        return Mathf.Lerp(outRange.x, outRange.y, t);
    }
}
