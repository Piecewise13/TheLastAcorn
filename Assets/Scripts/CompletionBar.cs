using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CompletionBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] RectTransform fillRect;

    [Header("Progress")]
    [SerializeField] int targetScore = 5;

    [Header("Optional override")]
    [Tooltip("Leave at 0 to use fillRect's width. Set > 0 to force a pixel length.")]
    [SerializeField] float fullWidth = 0f;
    
    int currentScore;
    float maxWidth;

    public bool  IsComplete => currentScore >= targetScore;
    public static bool AllCollected { get; private set; }

    void Awake()
    {
        if (!fillRect)
        {
            Debug.LogError($"{name}: fillRect missing.");
            enabled = false;
            return;
        }
        
        maxWidth = fullWidth > 0 ? fullWidth : fillRect.rect.width;
        SetWidth(0);
    }

    void OnEnable()  => StartCoroutine(SubscribeWhenReady());
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
        SetWidth(maxWidth * ratio);
        AllCollected = IsComplete;
    }
    
    void SetWidth(float w)
    {
        var s= fillRect.sizeDelta;
        s.x = w;
        fillRect.sizeDelta = s;
    }
}