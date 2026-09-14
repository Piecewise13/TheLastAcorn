using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Player
{
/// <summary>
/// Game-wide owner of the player's lives: damage rules, the post-hit immunity window and the
/// death/respawn flow. It is no longer a component on the player body, so it binds to whatever
/// player is current via <see cref="PlayerStateManager.OnPlayerRegistered"/>.
///
/// Presentation lives elsewhere: the hurt animation, knockback and immunity flicker belong to
/// <see cref="PlayerEffectsManager"/>, the life icons to the HUD's LifeMeterDisplay, and stun
/// timing to <see cref="PlayerMoveManager"/>. This manager only decides what happens and raises
/// the events the others react to.
/// </summary>
public class PlayerLifeManager : PersistentSingleton<PlayerLifeManager>
{
    [Header("Lives")]
    [SerializeField] private int maxLives = 3;

    [Header("Damage Settings")]
    [Tooltip("How long the player is stunned after taking a hit.")]
    [SerializeField] private float stunTime = 0.75f;

    [Tooltip("How long the player cannot be damaged again after taking a hit.")]
    [SerializeField] private float immuneTime = 3f;

    [SerializeField] private float damageLaunchForce = 30f;

    [Tooltip("Delay between running out of lives and respawning at the last checkpoint.")]
    [SerializeField] private float respawnDelay = 0.75f;

    public int MaxLives => maxLives;
    public int CurrentLives { get; private set; }
    public bool IsImmune { get; private set; }

    /// <summary>Fires with (currentLives, maxLives) whenever the life count changes. The HUD listens to this.</summary>
    public event Action<int, int> OnLivesChanged;

    /// <summary>Fires when the player loses a life but survives.</summary>
    public event Action OnPlayerDamaged;

    /// <summary>Fires when the last life is lost, before the respawn delay.</summary>
    public event Action OnPlayerDied;

    private GameObject player;
    private Rigidbody2D playerBody;
    private PlayerMoveManager moveManager;
    private PlayerEffectsManager effects;

    private CancellationTokenSource immunityCts;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicate, being destroyed

        CurrentLives = maxLives;
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        PlayerStateManager.OnPlayerRegistered += HandlePlayerRegistered;
    }

    private void OnDisable()
    {
        PlayerStateManager.OnPlayerRegistered -= HandlePlayerRegistered;
    }

    private void Start()
    {
        // Catch a player that came online before this persistent manager subscribed (first scene,
        // if the player's Awake ran first). Later scenes arrive through the event.
        if (Instance == this && PlayerStateManager.Instance != null)
            HandlePlayerRegistered(PlayerStateManager.Instance.playerGameObject);

        OnLivesChanged?.Invoke(CurrentLives, maxLives);
    }

    protected override void OnDestroy()
    {
        CancelImmunity();
        base.OnDestroy();
    }

    private void HandlePlayerRegistered(GameObject registeredPlayer)
    {
        if (Instance != this || registeredPlayer == null) return;

        player = registeredPlayer;
        playerBody = player.GetComponent<Rigidbody2D>();
        moveManager = player.GetComponent<PlayerMoveManager>();
        effects = player.GetComponent<PlayerEffectsManager>();

        // A new body starts at full health; the old one's immunity window no longer applies.
        CancelImmunity();
        SetLives(maxLives);
    }

    /// <summary>Damages the player with the default upward knockback.</summary>
    public void DamagePlayer() => DamagePlayer(Vector2.up * damageLaunchForce);

    /// <summary>Damages the player and launches them along <paramref name="launchDir"/>.</summary>
    public void DamagePlayer(Vector2 launchDir)
    {
        if (IsImmune || player == null) return;

        SetLives(CurrentLives - 1);

        effects?.PlayDamageFeedback(launchDir);
        moveManager?.StunPlayer(stunTime);

        BeginImmunity();

        if (CurrentLives <= 0) Die();
        else OnPlayerDamaged?.Invoke();
    }

    /// <summary>
    /// Damages the player and teleports them to <paramref name="respawnPos"/> instead of knocking
    /// them back — used by pits, water and other "you fell out of the level" triggers.
    /// </summary>
    public void DamagePlayerAndRelocate(Vector2 respawnPos)
    {
        if (player == null) return;

        // Relocation happens even while immune: these triggers fire because the player is
        // somewhere they cannot get out of. Immunity only spares the life.
        if (!IsImmune)
        {
            SetLives(CurrentLives - 1);
            BeginImmunity();

            if (CurrentLives <= 0)
            {
                Die();
                return;
            }

            OnPlayerDamaged?.Invoke();
        }

        if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
        player.transform.position = respawnPos;
    }

    private void Die()
    {
        OnPlayerDied?.Invoke();
        RespawnAfterDelay(destroyCancellationToken).Forget();
    }

    private async UniTask RespawnAfterDelay(CancellationToken cancellationToken)
    {
        await UniTask.WaitForSeconds(respawnDelay, cancellationToken: cancellationToken);

        // Acorns collected since the last checkpoint do not count once the player dies.
        if (ScoreManager.Instance != null) ScoreManager.Instance.ResetCurrentSceneScore();
        SaveLoadManager.ResetCurrentSceneAcorns();

        if (player == null) return;

        CheckpointManager.For(player.scene)?.RespawnPlayer(player);

        CancelImmunity();
        moveManager?.StopStun();
        SetLives(maxLives);
    }

    private void SetLives(int lives)
    {
        CurrentLives = Mathf.Clamp(lives, 0, maxLives);
        OnLivesChanged?.Invoke(CurrentLives, maxLives);
    }

    private void BeginImmunity()
    {
        CancelImmunity();
        immunityCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        RunImmunity(immunityCts.Token).Forget();
    }

    private async UniTask RunImmunity(CancellationToken cancellationToken)
    {
        IsImmune = true;
        effects?.StartImmunityFlash();

        // A cancel means a newer immunity window (or a respawn) already took over the flash,
        // so only the natural timeout clears it here.
        bool canceled = await UniTask.WaitForSeconds(immuneTime, cancellationToken: cancellationToken)
                                     .SuppressCancellationThrow();
        if (canceled) return;

        IsImmune = false;
        effects?.StopImmunityFlash();
    }

    private void CancelImmunity()
    {
        immunityCts?.Cancel();
        immunityCts?.Dispose();
        immunityCts = null;

        if (IsImmune)
        {
            IsImmune = false;
            effects?.StopImmunityFlash();
        }
    }
}
}
