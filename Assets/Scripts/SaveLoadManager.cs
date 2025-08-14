using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveLoadManager
{
    private const string LevelKey = "CurrentLevel";
    private const string TotalScoreKey = "TotalScore";

    private const string VisitedLevelsPrefix = "VisitedLevels_";

    // Check if persistence is disabled for debugging
    private static bool IsDebugMode => DebugSettings.Instance != null && DebugSettings.Instance.DisablePersistence;
    private static bool ShowDebugLogs => DebugSettings.Instance != null && DebugSettings.Instance.ShowDebugLogs;
    
    // Track acorns collected in current scene session (for scene restart)
    private static HashSet<string> currentSceneCollectedAcorns = new HashSet<string>();

    private static int currentCollectedAcornValue = 0;

    private static string currentSceneName = "";

    // Saves the current level number
    public static void SaveCurrentLevelName(string levelName)
    {
        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log($"[SaveLoadManager] DEBUG MODE: Skipping save level name: {levelName}");
            return;
        }
        
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
        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log("[SaveLoadManager] DEBUG MODE: Returning default level (Level1)");
            return "Level1";
        }
        
        return PlayerPrefs.GetString(LevelKey, "Level1");
    }

    public static void SaveLevelData(string levelName)
    {

        PlayerPrefs.SetInt(VisitedLevelsPrefix + levelName, 1);
    }

    public static bool HasLevelBeenLoaded(string levelName)
    {
        return PlayerPrefs.GetInt(VisitedLevelsPrefix + levelName, 0) == 1;
    }

    public static bool IsLevelSaved(string levelName)
    {
        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log($"[SaveLoadManager] DEBUG MODE: Returning false for IsLevelSaved: {levelName}");
            return false;
        }
        return PlayerPrefs.HasKey(levelName);
    }

    public static bool HasGameSave()
    {
        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log("[SaveLoadManager] DEBUG MODE: Returning false for IsLevelSaved");
            return false;
        }

        return PlayerPrefs.HasKey(LevelKey);
    }

    // Save and load persistent total score
    public static void SaveTotalScore(int score)
    {
        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log($"[SaveLoadManager] DEBUG MODE: Skipping save total score: {score}");
            return;
        }

        PlayerPrefs.SetInt(TotalScoreKey, score);
        PlayerPrefs.Save();
    }

    public static int LoadTotalScore()
    {
        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log("[SaveLoadManager] DEBUG MODE: Returning 0 for total score");
            return 0;
        }
        
        return PlayerPrefs.GetInt(TotalScoreKey, 0);
    }

    // Track collected acorns per level
    public static void MarkAcornCollected(string levelName, string acornId, int acornValue)
    {
        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log($"[SaveLoadManager] DEBUG MODE: Skipping mark acorn collected: {levelName}/{acornId}");
            // Still track in current scene session for scene restart functionality
            if (currentSceneName == levelName)
            {
                currentSceneCollectedAcorns.Add(acornId);
                currentCollectedAcornValue += acornValue;
            }
            return;
        }
        
        string key = $"Acorn_{levelName}_{acornId}";
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        // Also track in current scene session
        if (currentSceneName == levelName)
        {
            currentSceneCollectedAcorns.Add(acornId);
            currentCollectedAcornValue += acornValue;
        }
    }

    public static bool IsAcornCollected(string levelName, string acornId)
    {
        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log($"[SaveLoadManager] DEBUG MODE: Returning false for acorn collected: {levelName}/{acornId}");
            return false;
        }
        
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


    public static void SaveCollectedAcorns()
    {
        currentSceneCollectedAcorns.Clear();
    }
    
    // Reset only current scene acorns (for scene restart)
    public static void ResetCurrentSceneAcorns()
    {
        if (string.IsNullOrEmpty(currentSceneName)) return;

        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log($"[SaveLoadManager] DEBUG MODE: Skipping reset current scene acorns for: {currentSceneName}");
            // Still clear current session tracking for scene restart functionality
            currentSceneCollectedAcorns.Clear();
            return;
        }

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
}
