using UnityEngine;
using Unity.Cinemachine;

[ExecuteAlways]
public class CameraGhost : MonoBehaviour
{
    public Transform playerTransform;
    public PolygonCollider2D confiner; // Use CinemachineConfiner if in 3D

    void LateUpdate()
    {
        if (confiner == null) return;

        // In play mode we chase the player. In the editor (or whenever no
        // player is assigned) there is nothing to follow, so we instead clamp
        // wherever this object currently sits. This guarantees the camera
        // target can never leave the bounds, even while editing the scene.
        bool followingPlayer = Application.isPlaying && playerTransform != null;
        Vector3 targetPos = followingPlayer ? playerTransform.position : transform.position;

        // Ask the confiner to restrict this point to the collider shape.
        Vector3 constrainedPos = confiner.ClosestPoint(targetPos);

        // ClosestPoint works in 2D and drops Z, so preserve the existing depth.
        constrainedPos.z = transform.position.z;

        // Apply the safe, confined position to the ghost object.
        transform.position = constrainedPos;
    }
}
