using UnityEngine;
using Unity.Cinemachine;

[ExecuteAlways]
public class CameraGhost : MonoBehaviour
{
    public Transform playerTransform;
    public PolygonCollider2D confiner; // Use CinemachineConfiner if in 3D

    void Start()
    {
    }

    void LateUpdate()
    {
        if (playerTransform == null || confiner == null) return;

        // 1. Get the target player position
        Vector3 targetPos = playerTransform.position;

        // 2. Ask the confiner to restrict this point to the collider shape
        Vector3 constrainedPos = confiner.ClosestPoint(targetPos);

        // 3. Apply the safe, confined position to the ghost object
        transform.position = constrainedPos;
    }
}
