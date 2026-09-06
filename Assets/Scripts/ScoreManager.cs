using UnityEngine;
using System;

public class ScoreManager : PersistentSingleton<ScoreManager>
{
    [SerializeField] private int levelMinScore = 3;
    private static int score;
    public int CurrentScore => score;

    public event Action<int> OnScoreChanged;

    //Reorganize this to be somewhere else


    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicate, being destroyed

        // Load persistent score on first initialization
        score = SaveLoadManager.LoadTotalScore();
    }

    private void Start()
    {
        OnScoreChanged?.Invoke(score);
    }

    public void AddScore(int amount)
    {
        score += amount;
        OnScoreChanged?.Invoke(score);
        
        // Save the new total score persistently
        SaveLoadManager.SaveTotalScore(score);
    }

    public int GetMinimumScore(){return levelMinScore;}

    public int GetScore(){return score;}

    // Reset score to 0 and update UI
    public void ResetScore()
    {
        score = 0;
        OnScoreChanged?.Invoke(score);
        SaveLoadManager.SaveTotalScore(score);
    }
    
    // Reset current scene acorns and adjust total score
    public void ResetCurrentSceneScore()
    {
        int currentSceneAcornCount = SaveLoadManager.GetPendingSceneAcornCount();
        if (currentSceneAcornCount > 0)
        {
            score -= currentSceneAcornCount; // Subtract collected acorns from total
            if (score < 0) score = 0; // Ensure score doesn't go negative
            OnScoreChanged?.Invoke(score);
            SaveLoadManager.SaveTotalScore(score);
        }
    }
}
