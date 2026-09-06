using UnityEngine;

/// <summary>
/// Drives the bounded follow target: clamps the followed point to its area's camera bounds. A ghost
/// living under a <see cref="CameraZoneManager"/> clamps to that area; the rig's fallback ghost clamps
/// to whatever area is active. The player is found at runtime. With no bounds it follows directly.
/// </summary>
[ExecuteAlways]
public class CameraGhost : MonoBehaviour
{
    [Tooltip("Optional explicit player target. Left empty, the scene's player is found at runtime.")]
    [SerializeField] private Transform playerOverride;

    [Tooltip("Optional explicit bounds. Left empty, the scene's CameraZoneManager provides it.")]
    [SerializeField] private PolygonCollider2D confinerOverride;

    private Transform resolvedPlayer;

    private Transform ResolvePlayer()
    {
        if (playerOverride != null) return playerOverride;
        if (!Application.isPlaying) return null; // no player to follow in edit mode

        if (resolvedPlayer != null) return resolvedPlayer;

        // Canonical source first, then fall back to the tag (matches GlideFallEntry / CameraZone).
        if (PlayerStateManager.Instance != null && PlayerStateManager.Instance.playerGameObject != null)
        {
            resolvedPlayer = PlayerStateManager.Instance.playerGameObject.transform;
        }
        else
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) resolvedPlayer = tagged.transform;
        }

        return resolvedPlayer;
    }

    private PolygonCollider2D ResolveConfiner()
    {
        if (confinerOverride != null) return confinerOverride;

        // A ghost that lives under an area clamps to that area's own bounds — so a per-area ghost stays
        // inside its area regardless of which one is active. The rig's fallback ghost (no owning area)
        // clamps to whatever area is active, or any manager found in edit mode where none is yet.
        CameraZoneManager manager = GetComponentInParent<CameraZoneManager>();
        if (manager == null)
        {
            manager = Application.isPlaying
                ? CameraZoneManager.Active
                : FindAnyObjectByType<CameraZoneManager>();
        }

        return manager != null ? manager.Confiner : null;
    }

    void LateUpdate() => UpdateBoundedPosition();

    /// <summary>
    /// Recomputes the clamped follow position immediately. The <see cref="CameraDirector"/> calls this
    /// on an area switch so the bounded target reflects the new confiner this frame rather than a frame
    /// later — otherwise a snap after the switch would read the stale position.
    /// </summary>
    public void SnapToTarget() => UpdateBoundedPosition();

    private void UpdateBoundedPosition()
    {
        // In play mode we chase the player. In the editor (or before the player exists) there is
        // nothing to follow, so we clamp wherever this object currently sits.
        Transform player = ResolvePlayer();
        bool followingPlayer = Application.isPlaying && player != null;
        Vector3 targetPos = followingPlayer ? player.position : transform.position;

        // No bounds authored for this area: follow the target directly rather than freezing.
        PolygonCollider2D confiner = ResolveConfiner();
        Vector3 result = confiner != null ? confiner.ClosestPoint(targetPos) : targetPos;

        // ClosestPoint works in 2D and drops Z, so preserve the existing depth.
        result.z = transform.position.z;
        transform.position = result;
    }
}
