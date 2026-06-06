using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;

public class AcornCollectionBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject barContainer;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text acornCounter;
    [SerializeField] private GameObject acornLine;
    [SerializeField] private GameObject lineHolder;

    [Header("Animation")]
    [SerializeField] private float fillDuration = 0.4f;
    [SerializeField] private float hideDelay = 1.5f;
    [SerializeField] private float completeHoldDuration = 0.6f;
    [SerializeField] private float completePunchScale = 1.15f;
    [SerializeField] private float completePunchDuration = 0.25f;

    [Header("Optional override")]
    [Tooltip("Leave at 0 to use fillRect's width. Set > 0 to force a pixel length.")]
    [SerializeField] float fullWidth = 0f;

    public static bool AllCollected { get; private set; }

    private List<GameObject> indicatorInstances = new List<GameObject>();
    private Coroutine fillCoroutine;
    private Coroutine hideCoroutine;
    private bool subscribed;
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


    // Called on every collection that doesn't cross an unlock
    public void ShowCompletionBar(int segmentAcorns, int segmentRequired, Action onComplete = null)
    {
        if (completingAnimation) return;
        if (segmentRequired < 0) return;

        float target = segmentRequired > 0 ? Mathf.Clamp01((float)segmentAcorns / segmentRequired) : 0f;
        StartFill(target, onComplete: () =>
        {
            onComplete?.Invoke();
        });
    }

    void StartFill(float target, Action onComplete = null)
    {
        if (fillCoroutine != null) StopCoroutine(fillCoroutine);
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        fillCoroutine = StartCoroutine(AnimateFill(target, onComplete));
    }


    IEnumerator AnimateFill(float target, Action onComplete = null)
    {
        float start = progressSlider.value;
        float elapsed = 0f;
        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;
            progressSlider.value = Mathf.Lerp(start, target, elapsed / fillDuration);
            yield return null;
        }
        progressSlider.value = target;
        
        yield return new WaitForSeconds(hideDelay);
        
        onComplete?.Invoke();
    }


    IEnumerator CompleteAnimation()
    {
        // Fill to full
        float start = progressSlider.value;
        float elapsed = 0f;
        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;
            progressSlider.value = Mathf.Lerp(start, 1f, elapsed / fillDuration);
            yield return null;
        }
        progressSlider.value = 1f;

        // Punch-scale
        GameObject target = barContainer != null ? barContainer : progressSlider.gameObject;
        RectTransform rt = target.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector3 originalScale = rt.localScale;
            elapsed = 0f;
            while (elapsed < completePunchDuration)
            {
                elapsed += Time.deltaTime;
                float scale = Mathf.Lerp(1f, completePunchScale,
                    Mathf.Sin((elapsed / completePunchDuration) * Mathf.PI));
                rt.localScale = originalScale * scale;
                yield return null;
            }
            rt.localScale = originalScale;
        }

        yield return new WaitForSeconds(completeHoldDuration);

        progressSlider.value = 0f;
        SpawnIndicatorBars();
        gameObject.SetActive(false);
        completingAnimation = false;
    }


    public void SpawnIndicatorBars()
    {
        foreach (var obj in indicatorInstances)
            if (obj != null) Destroy(obj);
        indicatorInstances.Clear();

        int count = PlayerAbilityManager.Instance.CurrentSegmentCost;
        if (count < 0 || count <= 1 || acornLine == null ||
            progressSlider.fillRect == null || lineHolder == null)
            return;

        float width = fullWidth > 0f ? fullWidth : lineHolder.GetComponent<RectTransform>().rect.width;

        for (int i = 1; i < count; i++)
        {
            GameObject indicator = Instantiate(acornLine, lineHolder.transform);
            indicator.SetActive(true);
            indicatorInstances.Add(indicator);
            // Uncomment to reposition divider lines:
            RectTransform irt = indicator.GetComponent<RectTransform>();
            float x = (width * i / count) - width / 2f;
            irt.anchoredPosition = new Vector2(x, 0);
        }
    }
}