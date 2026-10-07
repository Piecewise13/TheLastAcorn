using Player;
using UnityEngine;

/// <summary>
/// Breaks the branch when the player steps into this trigger. Goes on the Trigger child of the
/// Branch Break prefab and fires the Break trigger parameter on the Animator of the main object.
///
/// The player loses movement the moment the branch breaks, so they can't run or jump clear and
/// have to fall down the hole. Control comes back when they land.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BranchBreakTrigger : MonoBehaviour
{
    [Tooltip("Animator on the Branch Break object. Found in the parents if left empty.")]
    [SerializeField] private Animator animator;

    [Tooltip("Trigger parameter on the Animator that plays the break.")]
    [SerializeField] private string breakParameter = "Break";

    private int breakId;
    private bool broken;

    private PlayerMoveManager player;
    private PlayerCameraManager playerCamera;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;

        if (animator == null)
        {
            animator = GetComponentInParent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError("BranchBreakTrigger: no Animator found in parents.", this);
        }

        breakId = Animator.StringToHash(breakParameter);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Break once, even if the player steps out and back in.
        if (broken || animator == null || !other.CompareTag("Player")) return;

        broken = true;
        animator.SetTrigger(breakId);

        FreezePlayer(other.transform.root);
    }

    private void FreezePlayer(Transform playerRoot)
    {
        player = playerRoot.GetComponentInChildren<PlayerMoveManager>();
        if (player == null)
        {
            Debug.LogWarning("BranchBreakTrigger: no PlayerMoveManager on the player.", this);
            return;
        }

        player.DisableMove();

        // Drop straight down instead of carrying the run off the edge of the hole.
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        // Letting go of the zoom turns movement back on, so turn it off again straight after.
        playerCamera = playerRoot.GetComponentInChildren<PlayerCameraManager>();
        if (playerCamera != null)
        {
            playerCamera.OnZoomEnded += HandleZoomEnded;
        }

        // Grounded only fires on a change, so standing on the branch now doesn't count; the next
        // one is the landing.
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateChanged += HandleStateChanged;
        }
    }

    private void HandleZoomEnded() => player.DisableMove();

    private void HandleStateChanged(PlayerState previous, PlayerState next)
    {
        if (next != PlayerState.Grounded) return;

        player.EnableMove();
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (playerCamera != null)
        {
            playerCamera.OnZoomEnded -= HandleZoomEnded;
        }

        // PlayerStateManager outlives this branch, so drop the handler or it leaks.
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private void OnDestroy() => Unsubscribe();
}
