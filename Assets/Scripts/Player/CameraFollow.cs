using UnityEngine;

/// <summary>
/// Minimal 2-D platformer camera:
/// • Soft-zone only (no look-ahead unless you turn it on)
/// • Separate smoothing, so X can stay snappy while Y is cushioned
/// </summary>
[DisallowMultipleComponent]
public class SimplePlatformerCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform target;
    [SerializeField] Vector2   offset = new(0f, 1.5f);

    [Header("Soft-Zone (dead zone)")]
    [Tooltip("Width/height of the box the player can move in before the camera follows.")]
    [SerializeField] Vector2   focusAreaSize = new(5f, 3f);   // bigger → less camera motion

    [Header("Smoothing (lower = snappier)")]
    [SerializeField, Min(0f)] float smoothX = 0.10f;
    [SerializeField, Min(0f)] float smoothY = 0.20f;

    [Header("Optional Look-Ahead")]
    [SerializeField] bool  enableLookAhead   = false;
    [SerializeField] float lookAheadDist     = 1.2f;
    [SerializeField] float lookAheadEaseTime = 0.25f;
    [SerializeField] float lookAheadReturn   = 0.40f;
    
    Vector2 _focusCenter;
    Vector2 _focusVelocity;
    float   _velX, _velY;               // SmoothDamp refs

    float _lookX, _lookVelX;
    bool  _lookingAhead;

    void OnEnable()
    {
        if (target) _focusCenter = target.position;
    }

    void LateUpdate()
    {
        if (!target) return;

        UpdateFocusArea();

        if (enableLookAhead) UpdateLookAhead();

        Vector2 desired = _focusCenter + offset + Vector2.right * _lookX;
        Vector3 pos     = transform.position;
        pos.x = Mathf.SmoothDamp(pos.x, desired.x, ref _velX, smoothX);
        pos.y = Mathf.SmoothDamp(pos.y, desired.y, ref _velY, smoothY);
        transform.position = pos;
    }
    
    void UpdateFocusArea()
    {
        Vector2 pos = target.position;
        Vector2 min = _focusCenter - focusAreaSize * 0.5f;
        Vector2 max = _focusCenter + focusAreaSize * 0.5f;

        Vector2 shift = Vector2.zero;
        if      (pos.x < min.x) shift.x = pos.x - min.x;
        else if (pos.x > max.x) shift.x = pos.x - max.x;

        if      (pos.y < min.y) shift.y = pos.y - min.y;
        else if (pos.y > max.y) shift.y = pos.y - max.y;

        _focusCenter   += shift;
        _focusVelocity  = shift / Mathf.Max(Time.deltaTime, 0.0001f);
    }

    void UpdateLookAhead()
    {
        if (Mathf.Abs(_focusVelocity.x) > 0.01f)
        {
            _lookingAhead = true;
            _lookX = Mathf.SmoothDamp(
                _lookX,
                Mathf.Sign(_focusVelocity.x) * lookAheadDist,
                ref _lookVelX,
                lookAheadEaseTime);
        }
        else if (_lookingAhead)
        {
            _lookX = Mathf.SmoothDamp(
                _lookX,
                0f,
                ref _lookVelX,
                lookAheadReturn);

            if (Mathf.Abs(_lookX) < 0.05f) _lookingAhead = false;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 size = new(focusAreaSize.x, focusAreaSize.y, 0);
        Gizmos.DrawWireCube(_focusCenter, size);
    }
#endif
}
