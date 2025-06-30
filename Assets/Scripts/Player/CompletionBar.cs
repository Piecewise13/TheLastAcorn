using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CompletionBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Slider progressSlider;

    [SerializeField] GameObject acornLine;

    [SerializeField] GameObject lineHolder;

    [Header("Progress")]
    private int targetScore = 5;

    [Header("Optional override")]
    [Tooltip("Leave at 0 to use fillRect's width. Set > 0 to force a pixel length.")]
    [SerializeField] float fullWidth = 0f;
    
    int currentScore;
    float maxWidth;

    public bool  IsComplete => currentScore >= targetScore;
    public static bool AllCollected { get; private set; }

    void Awake()
    {
        var scoreManager = FindFirstObjectByType<ScoreManager>();

        targetScore = scoreManager.GetMaxScore();

        if (!progressSlider)
        {
            Debug.LogError($"{name}: fillRect missing.");
            enabled = false;
            return;
        }

        // Calculate the number of indicators to spawn (one less than targetScore)
        int indicatorCount = Mathf.Max(0, targetScore);
        if (indicatorCount > 0 && acornLine != null && progressSlider.fillRect != null && lineHolder != null)
        {
            RectTransform fillRect = progressSlider.fillRect;
            float width = fullWidth > 0f ? fullWidth : fillRect.rect.width;

            for (int i = 1; i <= indicatorCount; i++)
            {
                // Calculate normalized position along the slider (0=start, 1=end)
                float t = (float)i / targetScore;
                float xPos = Mathf.Lerp(0, width, t);

                // Instantiate a new indicator as a child of lineHolder
                GameObject indicator = Instantiate(acornLine, lineHolder.transform);
                indicator.SetActive(true);

                /*

                RectTransform rt = indicator.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(0, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                // Position relative to the fillRect's width
                rt.anchoredPosition = new Vector2(xPos, 0);
                */

            // Optionally set size if needed
            // rt.sizeDelta = new Vector2(acornLine.GetComponent<RectTransform>().rect.width, acornLine.GetComponent<RectTransform>().rect.height);
            }
        }

        progressSlider.value = 0f;
    }

    void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
        
    } 
    void OnDisable()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
    }

    IEnumerator SubscribeWhenReady()
    {
        while (ScoreManager.Instance == null) yield return null;
        ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
        HandleScoreChanged(ScoreManager.Instance.CurrentScore);
    }

    void HandleScoreChanged(int newScore)
    {
        currentScore = newScore;
        float ratio  = Mathf.Clamp01((float)currentScore / targetScore);
        progressSlider.value = ratio;
        AllCollected = IsComplete;
    }

    
}