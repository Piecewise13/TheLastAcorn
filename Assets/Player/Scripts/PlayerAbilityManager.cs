using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;

namespace Player
{
/// <summary>
/// Tracks which gameplay abilities (Zoom, Glide, Leap) have been unlocked and
/// owns the cinematic unlock sequence. Segment/acorn counting has moved to
/// PlayerUpgradeManager.
/// </summary>
public class PlayerAbilityManager : PersistentSingleton<PlayerAbilityManager>
{
    public enum Abilities { Zoom = 0, Glide = 1, Leap = 2 }

    [Serializable]
    struct AbilityUnlockStep
    {
        public Abilities ability;
    }

    [SerializeField] private UnlockZoomView unlockZoomView = null!;

    [SerializeField] private AbilityUnlockStep[] unlockSteps = new AbilityUnlockStep[]
    {
        new AbilityUnlockStep { ability = Abilities.Zoom  },
        new AbilityUnlockStep { ability = Abilities.Glide },
        new AbilityUnlockStep { ability = Abilities.Leap  }
    };

    private readonly bool[] abilityUnlocked = new bool[Enum.GetValues(typeof(Abilities)).Length];
    private const string SaveKey = "UnlockedAbilities";

    private int currentStepIndex;

    private PlayerMoveManager playerMoveManager;

    /// <summary>Fires when any ability is unlocked.</summary>
    public event Action<Abilities> OnAbilityUnlocked;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicate, being destroyed

        for (int i = 0; i < abilityUnlocked.Length; i++)
            abilityUnlocked[i] = PlayerPrefs.GetInt(SaveKey + i, 0) == 1;

        currentStepIndex = 0;
        for (int i = 0; i < unlockSteps.Length; i++)
            if (abilityUnlocked[(int)unlockSteps[i].ability]) currentStepIndex++;
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
        // Catch a player that already came online before this persistent manager subscribed
        // (first scene, if the player's Awake ran first). Subsequent scenes arrive via the event.
        if (Instance == this && PlayerStateManager.Instance != null)
            HandlePlayerRegistered(PlayerStateManager.Instance.playerGameObject);
    }

    // This manager is no longer a component on the player, so it binds to whatever player body is
    // currently live rather than GetComponent on itself.
    private void HandlePlayerRegistered(GameObject player)
    {
        if (Instance != this || player == null) return;
        playerMoveManager = player.GetComponent<PlayerMoveManager>();
    }

    /// <summary>True when there is at least one ability still waiting to be unlocked.</summary>
    public bool HasPendingUnlock() => currentStepIndex < unlockSteps.Length;

    public bool IsAbilityUnlocked(Abilities ability) => abilityUnlocked[(int)ability];

    /// <summary>Shows the cinematic unlock animation. Call before UnlockAbility.</summary>
    public async UniTask StartUnlockAbility()
    {
        playerMoveManager.DisableMove();
        CameraRig.Current.SetTrackingTarget(playerMoveManager.transform);
        ViewManager.Instance.ClearViewsInstant();
        await ViewManager.Instance.PushView(unlockZoomView);
    }

    /// <summary>Marks the current pending ability as unlocked and fires the event.</summary>
    public void UnlockAbility(Abilities ability)
    {
        if (!HasPendingUnlock()) return;
        
        abilityUnlocked[(int)ability] = true;
        
        OnAbilityUnlocked?.Invoke(ability);
        SaveAbilities();
    }

    private void SaveAbilities()
    {
        if (DebugSettings.Instance.DisablePersistence) return;

        for (int i = 0; i < abilityUnlocked.Length; i++)
            PlayerPrefs.SetInt(SaveKey + i, abilityUnlocked[i] ? 1 : 0);
    }

    /// <summary>Debug: immediately unlocks the given ability without the cinematic sequence.</summary>
    private void DebugUnlockAbility(Abilities ability)
    {
        if (abilityUnlocked[(int)ability]) return;

        abilityUnlocked[(int)ability] = true;

        currentStepIndex = 0;
        for (int i = 0; i < unlockSteps.Length; i++)
            if (abilityUnlocked[(int)unlockSteps[i].ability]) currentStepIndex++;

        OnAbilityUnlocked?.Invoke(ability);
        SaveAbilities();
    }

    [Button("Debug: Unlock Zoom")]
    private void DebugUnlockZoom() => DebugUnlockAbility(Abilities.Zoom);

    [Button("Debug: Unlock Glide")]
    private void DebugUnlockGlide() => DebugUnlockAbility(Abilities.Glide);

    [Button("Debug: Unlock Leap")]
    private void DebugUnlockLeap() => DebugUnlockAbility(Abilities.Leap);
}
}
