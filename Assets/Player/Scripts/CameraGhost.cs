using UnityEngine;
using UnityEngine.SceneManagement;
using Player;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Drives the bounded follow target: clamps the followed point to its area's camera bounds. A ghost
/// living under a <see cref="CameraZoneManager"/> clamps to that area; the rig's fallback ghost clamps
/// to whatever area is active. With no bounds it follows directly.
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
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            return ResolveEditorPlayer();
#else
            return null;
#endif
        }

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

#if UNITY_EDITOR
    private static Transform ResolveEditorPlayer()
    {
        PlayerStateManager[] stateManagers = Resources.FindObjectsOfTypeAll<PlayerStateManager>();
        for (int i = 0; i < stateManagers.Length; i++)
        {
            if (IsSceneObject(stateManagers[i].gameObject)) return stateManagers[i].transform;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (IsSceneObject(tagged)) return tagged.transform;

        PlayerMoveManager[] movers = Resources.FindObjectsOfTypeAll<PlayerMoveManager>();
        for (int i = 0; i < movers.Length; i++)
        {
            if (IsSceneObject(movers[i].gameObject)) return movers[i].transform;
        }

        return null;
    }

    private static bool IsSceneObject(GameObject candidate)
    {
        return candidate != null
               && candidate.scene.IsValid()
               && !EditorUtility.IsPersistent(candidate);
    }
#endif

    private PolygonCollider2D ResolveConfiner(CameraZone editorPlayerZone = null)
    {
        if (confinerOverride != null) return confinerOverride;

#if UNITY_EDITOR
        if (!Application.isPlaying && editorPlayerZone != null)
        {
            CameraZoneManager editorManager = editorPlayerZone.GetComponentInParent<CameraZoneManager>(true);
            if (editorManager != null) return editorManager.Confiner;
        }
#endif

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
        Transform player = ResolvePlayer();
#if UNITY_EDITOR
        KeepTargetInPlayerScene(player);
        CameraZone editorPlayerZone = ResolveEditorPlayerZone(player);
#else
        CameraZone editorPlayerZone = null;
#endif

        // In edit mode, mirror the room-zone framing the player would trigger in play mode.
        bool followingPlayer = Application.isPlaying && player != null;
        Vector3 targetPos = editorPlayerZone != null
            ? editorPlayerZone.transform.position
            : followingPlayer
                ? player.position
                : transform.position;

        // No bounds authored for this area: follow the target directly rather than freezing.
        PolygonCollider2D confiner = ResolveConfiner(editorPlayerZone);
        Vector3 result = confiner != null ? confiner.ClosestPoint(targetPos) : targetPos;

        // ClosestPoint works in 2D and drops Z, so preserve the existing depth.
        result.z = transform.position.z;
        transform.position = result;
    }

#if UNITY_EDITOR
    private void KeepTargetInPlayerScene(Transform player)
    {
        if (Application.isPlaying || player == null || EditorUtility.IsPersistent(gameObject)) return;
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;

        Scene playerScene = player.gameObject.scene;
        GameObject targetRoot = transform.root.gameObject;
        if (!playerScene.IsValid() || !targetRoot.scene.IsValid() || targetRoot.scene == playerScene) return;
        if (EditorUtility.IsPersistent(targetRoot)) return;

        Scene previousScene = targetRoot.scene;
        // Scene membership follows the hierarchy, so move the target's root to keep prefab links intact.
        SceneManager.MoveGameObjectToScene(targetRoot, playerScene);
        if (previousScene.IsValid()) EditorSceneManager.MarkSceneDirty(previousScene);
        EditorSceneManager.MarkSceneDirty(playerScene);
    }

    private static CameraZone ResolveEditorPlayerZone(Transform player)
    {
        if (Application.isPlaying || player == null) return null;
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return null;

        CameraZoneManager[] managers = Resources.FindObjectsOfTypeAll<CameraZoneManager>();
        for (int managerIndex = 0; managerIndex < managers.Length; managerIndex++)
        {
            CameraZoneManager manager = managers[managerIndex];
            if (!IsSceneObject(manager != null ? manager.gameObject : null)) continue;
            if (manager.gameObject.scene != player.gameObject.scene) continue;

            CameraZone[] zones = manager.GetComponentsInChildren<CameraZone>(true);
            for (int zoneIndex = zones.Length - 1; zoneIndex >= 0; zoneIndex--)
            {
                CameraZone zone = zones[zoneIndex];
                if (zone != null && zone.AllowsTrigger && zone.ContainsPoint(player.position)) return zone;
            }
        }

        return null;
    }
#endif
}
