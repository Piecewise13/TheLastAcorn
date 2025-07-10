using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveLoadManager
{
    private const string LevelKey = "CurrentLevel";

    static Dictionary<string, LevelData> levelDataDictionary = new Dictionary<string, LevelData>();



    // Saves the current level number
    public static void SaveCurrentLevelName(string levelName)
    {
        PlayerPrefs.SetString(LevelKey, levelName);
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

    [Serializable]
    public struct LevelData
    {
        public List<Vector3> acornPositions;
        public List<Vector3> goldenAcornPositions;
    }
}
