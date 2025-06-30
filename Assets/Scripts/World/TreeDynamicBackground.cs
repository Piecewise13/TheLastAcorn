using UnityEngine;

/// <summary>
/// Matches color transitions with DynamicBackground.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TreeDynamicBackground : MonoBehaviour
{
    [Header("Colour Settings")]
    [Tooltip("Must match colourSpeed from DynamicBackground.")]
    [SerializeField, Min(0f)] private float colourSpeed = 0.5f;

    [Tooltip("Colours to cycle through, must match DynamicBackground.")]
    [SerializeField] private Color[] colours = { Color.white, Color.black };

    [Tooltip("Use PingPong instead of a looping gradient.")]
    [SerializeField] private bool usePingPong = false;

    private SpriteRenderer _sr;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (_sr == null || colours == null || colours.Length == 0) return;

        if (colours.Length == 1)
        {
            _sr.color = colours[0];
            return;
        }

        float maxIndex = colours.Length - 1;
        float cPos = usePingPong
            ? Mathf.PingPong(Time.time * colourSpeed, maxIndex)
            : (Time.time * colourSpeed) % maxIndex;

        int idx = Mathf.FloorToInt(cPos);
        float lerpT = cPos - idx;

        _sr.color = Color.Lerp(colours[idx], colours[idx + 1], lerpT);
    }
}
