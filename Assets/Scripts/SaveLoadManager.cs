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

    // Track acorns collected in current scene session since scene load or checkpoint reached
    private static HashSet<string> pendingAcorns = new HashSet<string>();

    // total acorn value that the player has collected since scene load or checkpoint reached
    private static int pendingAcornValue = 0;

    // total acorns collected for the scene across all sessions
    private static HashSet<string> sceneCollectedAcorns = new HashSet<string>();

    // total acorn value for the scene across all sessions
    private static int sceneTotalAcornValue = 0;

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
    public static void MarkAcornCollected(string levelName, Acorn acorn)
    {

        if (currentSceneName == levelName)
        {
            pendingAcorns.Add(acorn.AcornId);
            pendingAcornValue += acorn.Value;
        }

        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log($"[SaveLoadManager] DEBUG MODE: Skipping mark acorn collected: {levelName}/{acorn.AcornId}");
            // Still track in current scene session for scene restart functionality

            return;
        }

        string key = $"Acorn_{levelName}_{acorn.AcornId}";
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
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

    public static void ClearLevelAcorns(string levelName) { PlayerPrefs.Save(); }

    // Initialize scene state tracking
    public static void InitializeSceneState(string levelName)
    {
        currentSceneName = levelName;
        pendingAcorns.Clear();
    }


    public static void SaveCollectedAcorns()
    {

        sceneCollectedAcorns.UnionWith(pendingAcorns);
        sceneTotalAcornValue += pendingAcornValue;

        pendingAcornValue = 0;
        pendingAcorns.Clear();
    }

    // Reset only current scene acorns (for scene restart)
    public static void ResetCurrentSceneAcorns()
    {
        if (string.IsNullOrEmpty(currentSceneName)) return;

        if (IsDebugMode)
        {
            if (ShowDebugLogs) Debug.Log($"[SaveLoadManager] DEBUG MODE: Skipping reset current scene acorns for: {currentSceneName}");
            // Still clear current session tracking for scene restart functionality
            pendingAcorns.Clear();
            return;
        }

        // Remove current scene acorns from persistent storage
        foreach (string acornId in pendingAcorns)
        {
            sceneCollectedAcorns.Remove(acornId);

            string key = $"Acorn_{currentSceneName}_{acornId}";
            PlayerPrefs.DeleteKey(key);
        }

        sceneTotalAcornValue -= pendingAcornValue;

        PlayerPrefs.Save();

        // Clear current session tracking
        pendingAcorns.Clear();
        pendingAcornValue = 0;
    }

    // Get count of acorns collected in current scene session
    public static int GetPendingSceneAcornCount()
    {
        return pendingAcornValue;
    }

    public static int GetTotalSceneAcornCount()
    {
        return sceneTotalAcornValue;
    }

    public static HashSet<string> GetSceneCollectedAcorns()
    {
        return sceneCollectedAcorns;
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
