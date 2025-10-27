using UnityEngine;

/// <summary>
/// Vertical oscillation + colour interpolation for a SpriteRenderer.
/// </summary>
[RequireComponent(typeof(Transform), typeof(SpriteRenderer))]
public class DynamicBackground : MonoBehaviour
{
    /* ──────  Motion Settings  ────── */
    [Header("Motion Settings")]
    [Tooltip("Cycles per second (0.5 = one full up-and-down every 2 s).")]
    [SerializeField, Min(0f)] private float speed = 0.5f;

    [Tooltip("How far above the start position the object can rise.")]
    [SerializeField] private float distanceUp = 7f;

    [Tooltip("How far below the start position the object can sink.")]
    [SerializeField] private float distanceDown = 60f;

    [Tooltip("Use PingPong instead of a sine wave (sharper turns at extremes).")]
    [SerializeField] private bool usePingPong = false;

    //  Color Settings
    [Header("Colour Settings")]
    [Tooltip("Cycles per second for the colour gradient.")]
    [SerializeField, Min(0f)] private float colourSpeed = 0.5f;

    [Tooltip("Colours to cycle through (in order). Needs ≥ 2 for interpolation.")]
    [SerializeField] private Color[] colours = { Color.white, Color.black };

    // Internals
    float _startY;
    SpriteRenderer _sr;

    void Awake()
    {
        _startY = transform.position.y;
        _sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        /* Position */
        float t = usePingPong
            ? Mathf.PingPong(Time.time * speed, 1f)                       // 0‒1
            : (Mathf.Sin(Time.time * speed * Mathf.PI * 2f) * .5f + .5f); // 0‒1

        float newY = Mathf.Lerp(_startY - distanceDown, _startY + distanceUp, t);
        Vector3 pos = transform.position;
        pos.y = newY;
        transform.position = pos;

        /* Colour */
        if (_sr != null && colours != null && colours.Length > 0)
        {
            if (colours.Length == 1)
            {
                _sr.color = colours[0];
            }
            else
            {
                float maxIndex = colours.Length - 1; // last valid lerp start
                float cPos = usePingPong
                    ? Mathf.PingPong(Time.time * colourSpeed, maxIndex)
                    : (Time.time * colourSpeed) % maxIndex;

                int idx = Mathf.FloorToInt(cPos);
                float lerpT = cPos - idx; // 0‒1 within segment

                _sr.color = Color.Lerp(colours[idx], colours[idx + 1], lerpT);
            }
        }
    }

#if UNITY_EDITOR
    /* Visualise motion range in Scene view */
    void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(p + Vector3.up * distanceUp, p - Vector3.up * distanceDown);
    }
#endif
}
