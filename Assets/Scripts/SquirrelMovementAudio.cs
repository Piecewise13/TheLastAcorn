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
        speedRange  = new(0.2f, 6f),
        volumeRange = new(0f, 0.8f),
        pitchRange  = new(0.95f, 1.15f)
    };

    [Header("Climb (constant)")]
    [SerializeField] private StateAudio climb = new()
    {
        volumeRange = new(0.7f, 0.7f),
        pitchRange  = new(1f,    1f)
    };

    [Header("Glide")]
    [SerializeField] private StateAudio glide = new()
    {
        speedRange  = new(2f, 12f),
        volumeRange = new(0f, 1f),
        pitchRange  = new(0.95f, 1.15f)
    };

    [Header("Fall")]
    [SerializeField] private StateAudio fall = new()
    {
        speedRange  = new(2f, 15f),
        volumeRange = new(0f, 1f),
        pitchRange  = new(0.95f, 1.15f)
    };

    [Header("Stunned (constant)")]
    [SerializeField] private StateAudio stunned = new()
    {
        volumeRange = new(1f, 1f),
        pitchRange  = new(1f, 1f)
    };

    [Header("Jump (one-shot)")]
    [SerializeField] private AudioPlayer jumpPlayer;

    [SerializeField] private bool enablePitchBend = true;

    private AudioSource src;
    private PlayerMove.PlayerState lastState = (PlayerMove.PlayerState)(-1);

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;

        if (!player) player = GetComponentInParent<PlayerMove>();
        if (!rb)     rb     = player.GetComponent<Rigidbody2D>();
    }

    void OnEnable()  => PlayerMove.Jumped += OnJump;
    void OnDisable() => PlayerMove.Jumped -= OnJump;

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

        if (state == PlayerMove.PlayerState.Climb || state == PlayerMove.PlayerState.STUNNED)
        {
            var s = GetSettings(state);
            src.volume = s.volumeRange.y;
            src.pitch  = s.pitchRange.y;
            return;
        }

        var set   = GetSettings(state);
        float vel = state == PlayerMove.PlayerState.Fall ? Mathf.Abs(rb.linearVelocity.y)
                                              : Mathf.Abs(rb.linearVelocity.x);

        float vol = Remap(vel, set.speedRange, set.volumeRange);
        src.volume = vol;

        if (enablePitchBend)
            src.pitch = Remap(vol, set.volumeRange, set.pitchRange);
    }

    void OnJump()
    {
        if (jumpPlayer) jumpPlayer.Play();
    }

    StateAudio GetSettings(PlayerMove.PlayerState s) => s switch
    {
        PlayerMove.PlayerState.Grounded => grounded,
        PlayerMove.PlayerState.Climb    => climb,
        PlayerMove.PlayerState.Glide    => glide,
        PlayerMove.PlayerState.Fall     => fall,
        PlayerMove.PlayerState.STUNNED  => stunned,
        _                    => default
    };

    static float Remap(float v, Vector2 inR, Vector2 outR)
    {
        float t = Mathf.InverseLerp(inR.x, inR.y, v);
        return Mathf.Lerp(outR.x, outR.y, t);
    }
}
