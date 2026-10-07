using System;
using Player;
using UnityEngine;

/// <summary>
/// Per-scene respawn: one authored start, a Vector3 for the last checkpoint.
/// Absence from a scene means that scene has no scripted start.
/// </summary>
public class CheckpointManager : SceneService<CheckpointManager>
{
    [Tooltip("Where the player appears on Play (if debug snaps to start) and after death before any checkpoint.")]
    [SerializeField] private Transform initialSpawnPoint;

    private Vector3 currentCheckpoint;

    public event Action OnPlayerRespawn;

    public bool HasStartPoint => initialSpawnPoint != null;

    private void Awake()
    {
        if (initialSpawnPoint != null)
            currentCheckpoint = initialSpawnPoint.position;
    }

    // Runs before CameraDirector's Start (execution order 100), so the camera frames the start
    // point rather than wherever the player was authored in the scene.
    private void Start()
    {
        // A scene the player travelled to always starts them at its start point. The scene Play
        // started in is left to DebugSettings, so its skip-spawn option still works there.
        if (SceneEntry.IsPlayStartScene) return;

        PlayerMoveManager move = FindAnyObjectByType<PlayerMoveManager>();
        if (move != null)
            SpawnAtStart(move.transform.root.gameObject);
    }

    public void SetCheckpoint(Vector3 checkpoint)
    {
        currentCheckpoint = checkpoint;
    }

    public void RespawnPlayer(GameObject player)
    {
        PlacePlayer(player, currentCheckpoint);
        OnPlayerRespawn?.Invoke();
    }

    /// <summary>Play-button snap and any explicit "start of this scene" placement. Does not change the active checkpoint.</summary>
    public void SpawnAtStart(GameObject player)
    {
        if (initialSpawnPoint == null) return;
        PlacePlayer(player, initialSpawnPoint.position);
    }

    private static void PlacePlayer(GameObject player, Vector3 position)
    {
        if (player == null)
        {
            Debug.LogWarning("Attempted to place a null player reference.");
            return;
        }

        player.transform.position = position;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Move the body too. The player's Rigidbody2D interpolates, so moving only the transform
            // leaves the body at the old spot and interpolation drags the player, and the camera
            // following them, back across the level until the next physics step catches up.
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
        }
    }
}
