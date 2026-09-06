using UnityEngine;

/// <summary>
/// The destination half of the glide-unlock beat. Placed in the cave the player falls into, this
/// takes that scene's own player (each scene spawns its own) and drops it into the fall the moment
/// <see cref="GlideUnlockTransition"/> swaps scenes under the white cover.
///
/// It only sets the player up to fall; uncovering the white and handing control back stays with
/// <see cref="GlideUnlockTransition"/>, so the fall is revealed before the "Press A" prompt.
/// </summary>
public class GlideFallEntry : MonoBehaviour
{
    [Tooltip("Where the player is dropped in. Defaults to this object's position.")]
    [SerializeField] private Transform dropPoint;

    [Tooltip("Downward speed the player starts the fall with.")]
    [SerializeField] private float initialDownwardVelocity = 5f;

    [Tooltip("Optional falling loop/whoosh played as the fall begins.")]
    [SerializeField] private AudioPlayer fallingSfx;

    /// <summary>The player this entry positioned, for the orchestrator to re-enable afterwards.</summary>
    public PlayerMoveManager Player { get; private set; }

    public void BeginFall()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            Debug.LogWarning($"[{nameof(GlideFallEntry)}] No object tagged 'Player' in the scene.", this);
            return;
        }

        Player = playerObject.GetComponentInChildren<PlayerMoveManager>();
        if (Player == null)
        {
            Debug.LogWarning($"[{nameof(GlideFallEntry)}] No {nameof(PlayerMoveManager)} on the player.", this);
            return;
        }

        // Keep the player inert until the white is gone and control is handed back.
        Player.DisableMove();

        Transform root = Player.transform.root;
        root.position = dropPoint != null ? dropPoint.position : transform.position;

        Rigidbody2D rb = Player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, -Mathf.Abs(initialDownwardVelocity));
        }

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.ChangeState(PlayerStateManager.PlayerState.Fall);
        }

        if (CameraRig.Current != null)
        {
            CameraRig.Current.SetTrackingTarget(Player.transform);
        }

        fallingSfx?.Play();
    }
}
