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

    [Header("Segment Complete Energy")]
    [Tooltip("Image laid over the slider fill. Turns gold, then drains toward the right as the energy leaves. Not driven by the Slider.")]
    [SerializeField] private Image goldFill;
    [SerializeField] private Color goldColor = new Color(1f, 0.78f, 0.25f, 1f);
    [SerializeField] private float goldShiftDuration = 0.35f;
    [Tooltip("Pause on the full gold bar before the energy leaves.")]
    [SerializeField] private float holdBeforeLaunch = 0.15f;
    [SerializeField] private UpgradeEnergyOrb energyOrbPrefab;



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

    /// <summary>
    /// Full bar turns gold, then pours out of its right end as an orb that flies to the player.
    /// Awaited by the caller, so the upgrade view only opens once the energy has arrived.
    /// </summary>
    private async UniTask AnimateSegmentComplete(CancellationToken token)
    {
        if (goldFill == null || energyOrbPrefab == null)
        {
            await PlaySegmentCompleteFeedback(token);
            return;
        }

        // The full bar rests first (the feedback is a pause), so the player sees the segment complete before
        // anything else happens. Only then does it turn gold.
        await PlaySegmentCompleteFeedback(token);
        await ShiftFillToGold(token);
        await UniTask.WaitForSeconds(holdBeforeLaunch, cancellationToken: token);
        await LaunchEnergy(token);
    }

    private async UniTask PlaySegmentCompleteFeedback(CancellationToken token)
    {
        if (segmentCompleteFeedback == null) return;

        await segmentCompleteFeedback.PlayFeedbacksAsync(token);
    }

    private async UniTask ShiftFillToGold(CancellationToken token)
    {
        Image sliderFill = progressSlider.fillRect.GetComponent<Image>();
        Color startColor = sliderFill != null ? sliderFill.color : goldFill.color;

        // Stretched over the whole container here rather than trusting the prefab, since a copy of the
        // slider's Fill carries the zeroed anchors the Slider normally overwrites at runtime.
        RectTransform goldRect = goldFill.rectTransform;
        goldRect.anchorMin = Vector2.zero;
        goldRect.anchorMax = Vector2.one;
        goldRect.offsetMin = Vector2.zero;
        goldRect.offsetMax = Vector2.zero;
        goldFill.color = startColor;
        goldFill.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < goldShiftDuration)
        {
            goldFill.color = Color.Lerp(startColor, goldColor, elapsed / goldShiftDuration);
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        goldFill.color = goldColor;
        if (sliderFill != null) sliderFill.enabled = false;
    }

    private async UniTask LaunchEnergy(CancellationToken token)
    {
        Camera cam = CameraRig.Current.Foreground;
        Transform player = PlayerStateManager.Instance.playerGameObject.transform;

        UpgradeEnergyOrb orb = Instantiate(energyOrbPrefab, BarExitWorldPoint(cam, player.position.z), Quaternion.identity);

        // The orb runs on its own token: the bar is destroyed as soon as the upgrade view opens, and
        // the orb still has to finish fading. Draining over the trail's lifetime makes the trail's tail
        // leave the bar edge just as the bar empties, so bar and trail read as one stream.
        await UniTask.WhenAll(
            DrainGoldFill(orb.TrailTime, token),
            orb.FlyTo(player, orb.destroyCancellationToken));
    }

    private async UniTask DrainGoldFill(float duration, CancellationToken token)
    {
        RectTransform goldRect = goldFill.rectTransform;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            goldRect.anchorMin = new Vector2(elapsed / duration, goldRect.anchorMin.y);
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        goldFill.gameObject.SetActive(false);
    }

    /// <summary>
    /// World point at the vertical centre of the gold fill's right edge, on the player's depth plane.
    /// The bar canvas is Screen Space Overlay, so its world corners are already in screen pixels.
    /// </summary>
    private Vector3 BarExitWorldPoint(Camera cam, float depthZ)
    {
        var corners = new Vector3[4];
        goldFill.rectTransform.GetWorldCorners(corners);

        Vector3 screenPoint = (corners[2] + corners[3]) * 0.5f;
        screenPoint.z = Mathf.Abs(depthZ - cam.transform.position.z);
        return cam.ScreenToWorldPoint(screenPoint);
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
