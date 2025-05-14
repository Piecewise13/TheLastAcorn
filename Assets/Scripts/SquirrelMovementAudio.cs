using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SquirrelMovementAudio : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private PlayerMove   player;      // auto-found if empty
    [SerializeField] private Rigidbody2D rb;           // auto-found if empty

    [Header("State → Loop clips")]
    public AudioClip groundedClip;
    public AudioClip climbClip;
    public AudioClip glideClip;
    public AudioClip fallClip;
    public AudioClip stunnedClip;

    [Header("Volume curves")]
    [Tooltip("Grounded: |vx|  (min → max)")]
    [SerializeField] private Vector2 groundedSpeedRange = new(0.2f, 6f);
    [SerializeField] private float   groundedMaxVol = 0.8f;

    [Tooltip("Glide: |vx|  (min → max)")]
    [SerializeField] private Vector2 glideSpeedRange = new(2f, 12f);
    [SerializeField] private float   glideMaxVol = 1f;

    [Tooltip("Fall: |vy|  (min → max)")]
    [SerializeField] private Vector2 fallSpeedRange = new(2f, 15f);
    [SerializeField] private float   fallMaxVol = 1f;

    [Header("Pitch-bend (optional)")]
    [SerializeField] private bool    enablePitchBend = true;
    [SerializeField] private float   minPitch = 0.95f;
    [SerializeField] private float   maxPitch = 1.15f;


    private AudioSource _src;
    private PlayerState _lastState = (PlayerState)(-1);

    private void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.loop = true;
        _src.playOnAwake = false;

        if (!player) player = GetComponentInParent<PlayerMove>();
        if (!rb) rb = player.GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        var state = player.GetPlayerState();

        // 1) Switch clip when the state changes 
        if (state != _lastState)
        {
            _lastState = state;
            _src.clip  = GetClipFor(state);

            if (_src.clip) _src.Play();
            else _src.Stop();
        }

        // 2) Re-map velocity → volume (and pitch) every frame
        if (!_src.isPlaying) return;

        float vol = state switch
        {
            PlayerState.Grounded => Map(Mathf.Abs(rb.linearVelocity.x),
                                        groundedSpeedRange, groundedMaxVol),

            PlayerState.Glide => Map(Mathf.Abs(rb.linearVelocity.x),
                                        glideSpeedRange,    glideMaxVol),

            PlayerState.Fall => Map(Mathf.Abs(rb.linearVelocity.y),
                                        fallSpeedRange,     fallMaxVol),

            PlayerState.Climb => 0.7f,     // steady scratch
            PlayerState.STUNNED => 1f,       // always loud
            _                     => 0f
        };

        _src.volume = vol;

        if (enablePitchBend)
        {
            // simple: same 0-1 factor drives pitch
            float t = Mathf.InverseLerp(0f, 1f, vol / Mathf.Max(0.01f, groundedMaxVol));
            _src.pitch = Mathf.Lerp(minPitch, maxPitch, t);
        }
    }


    private AudioClip GetClipFor(PlayerState s) => s switch
    {
        PlayerState.Grounded => groundedClip,
        PlayerState.Climb    => climbClip,
        PlayerState.Glide    => glideClip,
        PlayerState.Fall     => fallClip,
        PlayerState.STUNNED  => stunnedClip,
        _                    => null
    };

    private static float Map(float v, Vector2 inRange, float outMax)
    {
        float t = Mathf.InverseLerp(inRange.x, inRange.y, v);
        return t * outMax;
    }
}
