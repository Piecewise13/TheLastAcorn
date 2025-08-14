using UnityEngine;
using System;

/// <summary>
/// Manages player respawn points (checkpoints) and triggers respawn when needed.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    [Tooltip("Starting position if no checkpoint has been reached yet.")]
    [SerializeField] private Transform initialSpawnPoint;
    [SerializeField] private Transform levelRevisitSpawnPoint;

    private Transform currentCheckpointTransform;
    private Checkpoint currentCheckpoint;

    /// <summary>
    /// Event fired when player respawns - passes respawn position.
    /// </summary>
    public event Action<Vector3> OnPlayerRespawn;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // Set default checkpoint
        currentCheckpointTransform = initialSpawnPoint;
    }

    /// <summary>
    /// Call this to update the active respawn location.
    /// </summary>
    /// <param name="checkpoint">Transform of the new checkpoint.</param>
    public void SetCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null) return;
        currentCheckpoint = checkpoint;
        currentCheckpointTransform = checkpoint.transform;
    }

    /// <summary>
    /// Respawns the player at the last checkpoint.
    /// </summary>
    /// <param name="player">Player GameObject to respawn.</param>
    public void RespawnPlayer(GameObject player)
    {
        if (player == null)
        {
            Debug.LogWarning("Attempted to respawn a null player reference.");
            return;
        }

        // Move player to checkpoint position
        player.transform.position = currentCheckpointTransform.position;

        // Optionally reset velocity if Rigidbody2D is present
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;


        OnPlayerRespawn?.Invoke(currentCheckpointTransform.position);
    }

    public void SpawnAtInitalLocation(GameObject player)
    {
        if (initialSpawnPoint != null)
        {
            currentCheckpointTransform = initialSpawnPoint;
            player.transform.position = initialSpawnPoint.position;
        }
    }

    public void SpawnAtEndingLocation(GameObject player)
    {
        if (levelRevisitSpawnPoint != null)
        {
            currentCheckpointTransform = levelRevisitSpawnPoint;
            player.transform.position = levelRevisitSpawnPoint.position;
        }
    }
}
