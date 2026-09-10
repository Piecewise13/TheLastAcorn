using UnityEngine;
using Player;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;

public class AcornCollectionBar : ViewBase
{
    [Header("UI")]
    [SerializeField] private GameObject barContainer;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text acornCounter;
    [SerializeField] private GameObject acornLine;
    [SerializeField] private GameObject lineHolder;
    private RectTransform lineHolderRect => lineHolder.GetComponent<RectTransform>();

    [Header("Animation")]
    [SerializeField] private MMF_Player revealFeedback;
    [SerializeField] private MMF_Player fillFeedback;
    [Tooltip("Extra feedback played when this fill completes the whole segment (as opposed to adding a single acorn).")]
    [SerializeField] private MMF_Player segmentCompleteFeedback;
    [SerializeField] private MMF_Player exitFeedback;
    [SerializeField] private FloatController sliderFloatController;



    public static bool AllCollected { get; private set; }

    private List<GameObject> indicatorInstances = new List<GameObject>();

    private bool completingAnimation; // blocks segment updates while CompleteAnimation runs

    void Awake()
    {
        if (!progressSlider)
        {
            Debug.LogError($"{name}: progressSlider missing.");
            enabled = false;
            return;
        }
        
        progressSlider.value = 0f;
    }

    /// <summary>
    /// Reveals the bar (if needed) and animates the fill to the collected amount.
    /// Awaitable so callers can guarantee the bar is fully filled before continuing
    /// (e.g. before starting the ability unlock sequence).
    /// </summary>
    public async override UniTask RunAsync(CancellationToken token)
    {
        if (PlayerUpgradeManager.Instance.SegmentAcorns <= 0) return;
        
        progressSlider.maxValue = Mathf.Max(1, PlayerUpgradeManager.Instance.CurrentSegmentCost);
        SpawnIndicatorBars(PlayerUpgradeManager.Instance.CurrentSegmentCost);
        
        await AnimateReveal(token);
        
        int collected = PlayerUpgradeManager.Instance.SegmentAcorns;
        int cost = PlayerUpgradeManager.Instance.CurrentSegmentCost;
        bool segmentComplete = cost > 0 && collected >= cost;
        
        await AnimateFill(collected, token);

        if (segmentComplete)
        {
            await AnimateSegmentComplete(token);
        }
        else
        {
            await exitFeedback.PlayFeedbacksAsync(token);
        }
        

    }

    private async UniTask AnimateSegmentComplete(CancellationToken token)
    {
        if (segmentCompleteFeedback == null) return;

        await segmentCompleteFeedback.PlayFeedbacksAsync(token);
    }

    private async UniTask AnimateReveal(CancellationToken token)
    {
        if (revealFeedback == null) return;

        await revealFeedback.PlayFeedbacksAsync(token);
    }

    private async UniTask AnimateFill(int collected, CancellationToken token)
    {
        if (fillFeedback == null) return;

        int target = Mathf.Clamp(collected, 0, Mathf.RoundToInt(progressSlider.maxValue));
        float duration = fillFeedback.TotalDuration;
        
        sliderFloatController.ToDestinationDuration = duration;
        sliderFloatController.ToDestinationValue = target;
        sliderFloatController.ToDestination();
        
        await fillFeedback.PlayFeedbacksAsync(token);

        progressSlider.value = target;
        
        sliderFloatController.CurrentValue = target;
    }
    

    public void SpawnIndicatorBars(int requiredAcorns)
    {
        foreach (var obj in indicatorInstances)
            if (obj != null) Destroy(obj);
        indicatorInstances.Clear();

        int count = requiredAcorns;
        if (count < 0 || count <= 1 || acornLine == null ||
            progressSlider.fillRect == null || lineHolder == null)
            return;

        float width = lineHolderRect.rect.width;

        for (int i = 1; i < count; i++)
        {
            GameObject indicator = Instantiate(acornLine, lineHolder.transform);
            indicator.SetActive(true);
            indicatorInstances.Add(indicator);
            // Uncomment to reposition divider lines:
            var irt = indicator.GetComponent<RectTransform>();
            float x = (width * i / count) - width / 2f;
            irt.anchoredPosition = new Vector2(x, 0);
        }
    }

    public void Setup(int requiredAcorns)
    {

        progressSlider.maxValue = Mathf.Max(0, requiredAcorns);
        progressSlider.value = 0f;

        SpawnIndicatorBars(requiredAcorns);
    }
}
