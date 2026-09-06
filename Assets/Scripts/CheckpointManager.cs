using System;
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
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }
}
