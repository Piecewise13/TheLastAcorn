using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveLoadManager
{
    private const string LevelKey = "CurrentLevel";
    private const string TotalScoreKey = "TotalScore";

    static Dictionary<string, LevelData> levelDataDictionary = new Dictionary<string, LevelData>();
    
    // Track acorns collected in current scene session (for scene restart)
    private static HashSet<string> currentSceneCollectedAcorns = new HashSet<string>();
    private static string currentSceneName = "";

    // Saves the current level number
    public static void SaveCurrentLevelName(string levelName)
    {
        PlayerPrefs.SetString(LevelKey, levelName);
        PlayerPrefs.Save();
    }

    // Loads the saved level number, returns 1 if not set
    public static void LoadLastLevel()
    {
        string levelName = PlayerPrefs.GetString(LevelKey, "Level1");
        SceneManager.LoadScene(levelName);
    }

    public static string GetLoadedLevel()
    {
        return PlayerPrefs.GetString(LevelKey, "Level1");
    }

    public static void SaveLevelData(string levelName, Vector3[] acornPositions, Vector3[] goldenAcornPositions)
    {
        if (!levelDataDictionary.ContainsKey(levelName))
        {
            levelDataDictionary[levelName] = new LevelData();
        }

        LevelData levelData = levelDataDictionary[levelName];
        levelData.acornPositions = new List<Vector3>(acornPositions);
        levelData.goldenAcornPositions = new List<Vector3>(goldenAcornPositions);
    }

    public static bool HasLevelBeenLoaded(string levelName)
    {
        return levelDataDictionary.ContainsKey(levelName);
    }

    public static bool IsLevelSaved()
    {
        return PlayerPrefs.HasKey(LevelKey);
    }

    // Save and load persistent total score
    public static void SaveTotalScore(int score)
    {
        PlayerPrefs.SetInt(TotalScoreKey, score);
        PlayerPrefs.Save();
    }

    public static int LoadTotalScore()
    {
        return PlayerPrefs.GetInt(TotalScoreKey, 0);
    }

    // Track collected acorns per level
    public static void MarkAcornCollected(string levelName, string acornId)
    {
        string key = $"Acorn_{levelName}_{acornId}";
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        
        // Also track in current scene session
        if (currentSceneName == levelName)
        {
            currentSceneCollectedAcorns.Add(acornId);
        }
    }

    public static bool IsAcornCollected(string levelName, string acornId)
    {
        string key = $"Acorn_{levelName}_{acornId}";
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    // Get all collected acorns for a level
    public static HashSet<string> GetCollectedAcorns(string levelName)
    {
        HashSet<string> collectedAcorns = new HashSet<string>();
        return collectedAcorns;
    }

    public static void ClearLevelAcorns(string levelName){PlayerPrefs.Save();}
    
    // Initialize scene state tracking
    public static void InitializeSceneState(string levelName)
    {
        currentSceneName = levelName;
        currentSceneCollectedAcorns.Clear();
    }
    
    // Reset only current scene acorns (for scene restart)
    public static void ResetCurrentSceneAcorns()
    {
        if (string.IsNullOrEmpty(currentSceneName)) return;
        
        // Remove current scene acorns from persistent storage
        foreach (string acornId in currentSceneCollectedAcorns)
        {
            string key = $"Acorn_{currentSceneName}_{acornId}";
            PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        
        // Clear current session tracking
        currentSceneCollectedAcorns.Clear();
    }
    
    // Get count of acorns collected in current scene session
    public static int GetCurrentSceneAcornCount()
    {
        return currentSceneCollectedAcorns.Count;
    }

    // Debug method to check persistence status
    public static void LogSaveStatus()
    {
        Debug.Log($"Total Score: {LoadTotalScore()}");
        Debug.Log($"Current Level: {GetLoadedLevel()}");
    }

    // Clear all save data (for testing or reset)
    public static void ClearAllSaveData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    [Serializable]
    public struct LevelData
    {
        public List<Vector3> acornPositions;
        public List<Vector3> goldenAcornPositions;
    }
}
