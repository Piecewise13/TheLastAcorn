using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CompletionBar : MonoBehaviour
{
    [SerializeField] private Image  barImage;
    [SerializeField] private Sprite[] stages = new Sprite[4];
    [SerializeField] private int targetScore = 100;

    private int currentScore;
    public bool IsComplete => currentScore >= targetScore;

    private void OnEnable()  => StartCoroutine(SubscribeWhenReady());
    private void OnDisable()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (ScoreManager.Instance == null) yield return null;
        ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
        HandleScoreChanged(ScoreManager.Instance.CurrentScore);
    }

    private void HandleScoreChanged(int newScore)
    {
        currentScore = newScore;
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (barImage == null || stages == null || stages.Length != 4) return;
        float ratio = Mathf.Clamp01((float)currentScore / targetScore);
        int index = Mathf.Clamp(Mathf.CeilToInt(ratio * 3f), 0, 3);
        barImage.sprite = stages[index];
    }
}
