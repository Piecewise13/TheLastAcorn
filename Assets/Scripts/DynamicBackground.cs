using UnityEngine;

[RequireComponent(typeof(Transform))]
public class DynamicBackground : MonoBehaviour
{
    [Header("Motion Settings")]
    [Tooltip("Cycles per second (0.5 = one full up-and-down every 2 s).")]
    [SerializeField, Min(0f)] private float speed = 0.5f;

    [Tooltip("How far above the start position the object can rise.")]
    [SerializeField] private float distanceUp = 7f;

    [Tooltip("How far below the start position the object can sink.")]
    [SerializeField] private float distanceDown = 60f;

    [Tooltip("Use PingPong instead of a sine wave (sharper turns at extremes).")]
    [SerializeField] private bool usePingPong = false;

    private float _startY;

    private void Awake() => _startY = transform.position.y;

    private void Update()
    {
        // Normalized oscillation factor in the range 0-1
        float t = usePingPong
            ? Mathf.PingPong(Time.time * speed, 1f)
            : (Mathf.Sin(Time.time * speed * Mathf.PI * 2f) * 0.5f + 0.5f);

        float newY = Mathf.Lerp(_startY - distanceDown, _startY + distanceUp, t);
        Vector3 pos = transform.position;
        pos.y = newY;
        transform.position = pos;
    }

#if UNITY_EDITOR
    // Draw the motion range in the Scene view
    private void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(p + Vector3.up * distanceUp, p - Vector3.up * distanceDown);
    }
#endif
}
