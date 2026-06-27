using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;

public class PlayerAbilityManager : MonoBehaviour
{
    
    [Serializable]
    struct AbilityUnlockStep
    {
        public Abilities ability;
        [Tooltip("Acorns needed to unlock this ability, counted fresh from the previous unlock.")]
        public int segmentCost;
    }
    
    public static PlayerAbilityManager Instance { get; private set; }

    public enum Abilities { Zoom, Glide, Leap }
    
    private PlayerMove playerMove;
    private PlayerCameraManager playerCamera;

    [SerializeField] private UnlockZoomView unlockZoomView = null!;
    [SerializeField] private AcornCollectionBar acornCollectionBarPrefab = null!;

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
    public int SegmentAcorns => segmentAcorns;

    /// Total acorns needed for the current segment, or -1 if all unlocked.
    public int CurrentSegmentCost =>
        currentStepIndex < unlockSteps.Length ? unlockSteps[currentStepIndex].segmentCost : -1;
    
    [SerializeField] private int unlockStepIndex = 0;

    /// Fires when any ability is unlocked.
    public event Action<Abilities> OnAbilityUnlocked;

    /// Fires on every acorn collection that does NOT cross an unlock threshold.
    /// Parameters: (acorns in current segment, cost of current segment)
    public event Action<int, int> OnSegmentChanged;


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
        playerMove = GetComponent<PlayerMove>();
        playerCamera = GetComponentInChildren<PlayerCameraManager>();
        
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

    private AcornCollectionBar activeBar;
    private bool processingSegment;

    private void HandleScoreChanged(int newTotal)
    {
        if (currentStepIndex >= unlockSteps.Length) return;

        int delta = newTotal - lastScore;
        lastScore = newTotal;
        if (delta <= 0) return;

        segmentAcorns += delta;

        HandleSegmentProgress().Forget();
    }

    private async UniTaskVoid HandleSegmentProgress()
    {
        if (processingSegment) return;
        processingSegment = true;

        try
        {
            int cost = CurrentSegmentCost;
            bool segmentComplete = segmentAcorns >= cost;

            // Always animate the fill, including the final acorn that completes the
            // segment. The bar is pushed/owned by the ViewManager and we await the
            // fill so it reaches full before the unlock sequence begins.
            await ViewManager.Instance.PushView(acornCollectionBarPrefab);

            if (segmentComplete)
            {
                await StartUnlockAbility();
            }
            else
            {
                OnSegmentChanged?.Invoke(segmentAcorns, cost);
            }
        }
        finally
        {
            processingSegment = false;
        }
    }
    
    public async UniTask UnlockAbility()
    {
        if (currentStepIndex >= unlockSteps.Length) return;

        Abilities unlockedAbility = unlockSteps[currentStepIndex].ability;
        abilityUnlocked[(int)unlockedAbility] = true;
        currentStepIndex++;
        segmentAcorns = 0;
        
        await ViewManager.Instance.ClearViews();
        activeBar = null; // bar was destroyed by ClearViews

        CameraRig.Instance.ResetTrackingTarget();
        OverlayCameraController.Instance.ReleasePlayerOverlay();

        OnAbilityUnlocked?.Invoke(unlockedAbility);
        SaveAbilities();
    }

    public async UniTask StartUnlockAbility()
    {
        playerMove.DisableMove();
       
        CameraRig.Instance.SetTrackingTarget(playerMove.transform);
        
        ViewManager.Instance.ClearViewsInstant();
        activeBar = null; // bar was destroyed by ClearViewsInstant
        await ViewManager.Instance.PushView(unlockZoomView);
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

    [Button("Test Acorn Collection")]
    private async UniTask TestAcornCollection()
    {
        await ViewManager.Instance.PushView(acornCollectionBarPrefab);
    }
}
