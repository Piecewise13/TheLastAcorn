using UnityEngine;
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
    public async UniTask RunSegment(int acornsCollectedInSegment, int acornsRequiredForSegment, CancellationToken token)
    {
        if (acornsRequiredForSegment <= 0) return;

        progressSlider.maxValue = Mathf.Max(1, acornsRequiredForSegment);
        SpawnIndicatorBars(acornsRequiredForSegment);

        await ShowCompletionBar(acornsCollectedInSegment, acornsRequiredForSegment, token);
    }

    // Called on every collection that doesn't cross an unlock
    public async UniTask ShowCompletionBar(int acornsCollectedInSegment, int acornsRequiredForSegment, CancellationToken token)
    {
        if (completingAnimation) return;
        if (acornsRequiredForSegment < 0) return;

        //float target = acornsRequiredForSegment > 0 ? Mathf.Clamp01((float)acornsCollectedInSegment / acornsRequiredForSegment) : 0f;

        await AnimateReveal(token);

        await AnimateFill(acornsCollectedInSegment, token);

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
