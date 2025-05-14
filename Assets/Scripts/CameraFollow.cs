using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target; 
    [SerializeField] private Vector2  offset   = new(0, 1.5f);
    [SerializeField] private float    smoothTime = 0.25f; // lower = snappier
    [SerializeField] private float    lookAheadDist = 2f; // lead based on velocity
    [SerializeField] private float    lookAheadReturnSpeed = 2f;

    Vector3 _velocity;          // ref param for SmoothDamp
    Vector3 _currentLookAhead;  // smoothed look-ahead
    Vector3 _targetLastPos;

    void LateUpdate()
    {
        if (!target) return;

        // 1. Look-ahead based on target’s horizontal movement
        float xMoveDelta = (target.position - _targetLastPos).x;
        bool  moving = Mathf.Abs(xMoveDelta) > 0.01f;

        if (moving)
            _currentLookAhead = Vector3.right * Mathf.Sign(xMoveDelta) * lookAheadDist;
        else
            _currentLookAhead = Vector3.MoveTowards(
                _currentLookAhead, Vector3.zero, lookAheadReturnSpeed * Time.deltaTime);

        _targetLastPos = target.position;

        // 2. Desired position = target + offset + look-ahead
        Vector3 desired = target.position + (Vector3)offset + _currentLookAhead;
        desired.z = transform.position.z;         // keep camera’s Z

        // 3. Smooth-move camera
        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref _velocity, smoothTime);
    }
}
