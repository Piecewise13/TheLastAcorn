using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveLoadManager
{
    private const string LevelKey = "CurrentLevel";

    // Saves the current level number
    public static void SaveLevel(string levelName)
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


    public static bool IsLevelSaved()
    {
        return PlayerPrefs.HasKey(LevelKey);
    }
}
