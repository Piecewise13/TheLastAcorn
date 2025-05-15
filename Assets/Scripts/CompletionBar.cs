using UnityEngine;
using UnityEngine.UI;

public class CompletionBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image fillImage;
    [Header("Goal")]
    [SerializeField] private int targetScore = 100;

    public bool IsComplete => currentScore >= targetScore;

    int currentScore = 0;

    /// <summary>
    /// Call this every time the player gains points.
    /// </summary>
    public void Add(int amount)
    {
        currentScore += amount;
        float t = Mathf.Clamp01((float)currentScore / targetScore);
        if (fillImage != null) fillImage.fillAmount = t;
    }

    /// <summary>
    /// Resets bar to zero.
    /// </summary>
    public void ResetBar()
    {
        currentScore = 0;
        if (fillImage != null) fillImage.fillAmount = 0f;
    }
}
