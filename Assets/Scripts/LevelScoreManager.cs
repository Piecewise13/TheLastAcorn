using UnityEngine;
using System;

public class LevelScoreManager : MonoBehaviour
{
    private static LevelScoreManager _instance;
    public static LevelScoreManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Auto-create the LevelScoreManager if it doesn't exist
                GameObject go = new GameObject("LevelScoreManager");
                _instance = go.AddComponent<LevelScoreManager>();
            }
            return _instance;
        }
    }
    
    private int levelScore = 0;
    public int CurrentLevelScore => levelScore;
    public event Action<int> OnLevelScoreChanged;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Reset level score when level starts
        levelScore = 0;
        OnLevelScoreChanged?.Invoke(levelScore);
    }

    public void AddLevelScore(int amount)
    {
        levelScore += amount;
        OnLevelScoreChanged?.Invoke(levelScore);
    }

    public int GetLevelScore()
    {
        return levelScore;
    }
    
    public void ResetLevelScore()
    {
        levelScore = 0;
        OnLevelScoreChanged?.Invoke(levelScore);
    }
}