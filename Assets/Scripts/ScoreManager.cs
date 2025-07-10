using UnityEngine;
using System;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [SerializeField] private int levelMaxScore = 3;
    private static int score;
    public int CurrentScore => score;
    public event Action<int> OnScoreChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddScore(int amount)
    {
        score += amount;
        OnScoreChanged?.Invoke(score);
    }

    public int GetMaxScore()
    {
        return levelMaxScore;
    }

    public int GetScore()
    {
        return score;
    }
}
