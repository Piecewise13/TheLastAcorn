using System;
using System.Collections;
using UnityEngine;

public class PlayerAbilityManager : MonoBehaviour
{
    public static PlayerAbilityManager Instance { get; private set; }

    public enum Abilities { Zoom, Glide, Leap }
    
    private Animator animator;
    private PlayerMove playerMove;
    private PlayerCamera playerCamera;

    [Serializable]
    struct AbilityUnlockStep
    {
        public Abilities ability;
        [Tooltip("Acorns needed to unlock this ability, counted fresh from the previous unlock.")]
        public int segmentCost;
    }

    [SerializeField] AbilityUnlockStep[] unlockSteps = new AbilityUnlockStep[]
    {
        new AbilityUnlockStep { ability = Abilities.Zoom,  segmentCost = 1 },
        new AbilityUnlockStep { ability = Abilities.Glide, segmentCost = 3 },
        new AbilityUnlockStep { ability = Abilities.Leap,  segmentCost = 5 }
    };

    private readonly bool[] abilityUnlocked = new bool[System.Enum.GetValues(typeof(Abilities)).Length];
    private const string SaveKey = "UnlockedAbilities";

    private int currentStepIndex; // index of the next ability to unlock
    private int segmentAcorns;    // acorns collected toward the current unlock
    private int lastScore;        // last total seen; used to compute per-collection delta

    /// Fires when any ability is unlocked.
    public event Action<Abilities> OnAbilityUnlocked;

    /// Fires on every acorn collection that does NOT cross an unlock threshold.
    /// Parameters: (acorns in current segment, cost of current segment)
    public event Action<int, int> OnSegmentChanged;

    public int SegmentAcorns => segmentAcorns;

    /// Total acorns needed for the current segment, or -1 if all unlocked.
    public int CurrentSegmentCost =>
        currentStepIndex < unlockSteps.Length ? unlockSteps[currentStepIndex].segmentCost : -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < abilityUnlocked.Length; i++)
            abilityUnlocked[i] = PlayerPrefs.GetInt(SaveKey + i, 0) == 1;

        currentStepIndex = 0;
        for (int i = 0; i < unlockSteps.Length; i++)
            if (abilityUnlocked[(int)unlockSteps[i].ability]) currentStepIndex++;
    }

    private void Start()
    {
        animator = GetComponent<Animator>();
        playerMove = GetComponent<PlayerMove>();
        playerCamera = GetComponentInChildren<PlayerCamera>();
        
        StartCoroutine(SubscribeToScore());
    }

    private IEnumerator SubscribeToScore()
    {
        while (ScoreManager.Instance == null) yield return null;
        lastScore = ScoreManager.Instance.CurrentScore;
        ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
    }

    private void OnDestroy()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
    }

    private void HandleScoreChanged(int newTotal)
    {
        if (currentStepIndex >= unlockSteps.Length) return;

        int delta = newTotal - lastScore;
        lastScore = newTotal;
        if (delta <= 0) return;

        segmentAcorns += delta;

        bool anyUnlocked = false;


        // Only broadcast progress when no unlock happened this frame.
        // Unlock frames are handled by OnAbilityUnlocked subscribers.
        if (!anyUnlocked)
            OnSegmentChanged?.Invoke(segmentAcorns, CurrentSegmentCost);

    }

    // private void ShowAbilityUnlockScreen()
    // {
    //     if (abilityUnlockView != null)
    //         abilityUnlockView.SetActive(true);
    // }

    public void UnlockAbility()
    {
        abilityUnlocked[currentStepIndex] = true;
        Debug.Log($"Unlocked ability: {(Abilities)currentStepIndex}");
        OnAbilityUnlocked?.Invoke((Abilities)currentStepIndex);
        animator.SetBool("isUnlockingAbility", false);
        ViewManager.Instance.ResetToHUD();
        playerCamera.ResetTrackingTarget();
        SaveAbilities();
    }

    public void StartUnlockAbility()
    {
        playerMove.DisableMove();
        
        animator.SetBool("isUnlockingAbility", true);
        ViewManager.Instance.ClearViews();
        playerCamera.SetCameraTarget(playerMove.gameObject);
    }

    private void SaveAbilities()
    {

        if (DebugSettings.Instance.DisablePersistence)
        {
            return;
        }

        for (int i = 0; i < abilityUnlocked.Length; i++)
            PlayerPrefs.SetInt(SaveKey + i, abilityUnlocked[i] ? 1 : 0);
    }

    public bool IsAbilityUnlocked(Abilities ability) => abilityUnlocked[(int)ability];
}
