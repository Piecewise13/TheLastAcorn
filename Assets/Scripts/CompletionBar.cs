using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CompletionBar : MonoBehaviour
{
    [SerializeField] Image   barImage;
    [SerializeField] Sprite[] stages = new Sprite[4];  // 0-empty, 1-⅓, 2-⅔, 3-full
    [SerializeField] int targetScore = 100;

    int currentScore;
    public bool  IsComplete   => currentScore >= targetScore;
    public static bool AllCollected { get; private set; }

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
        UpdateSprite();
        AllCollected = IsComplete;
    }

    void UpdateSprite()
    {
        if (barImage == null || stages.Length != 4) return;

        float ratio = Mathf.Clamp01((float)currentScore / targetScore);
        int index = 0;
        if      (ratio >= 1f)   index = 3;
        else if (ratio >= .66f) index = 2;
        else if (ratio >= .33f) index = 1;

        barImage.sprite = stages[index];
    }
}
