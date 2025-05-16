using UnityEngine.SceneManagement;

public static class SceneLoader
{
    /// <summary>Load scene by build index.</summary>
    public static void Load(int buildIndex) =>
        SceneManager.LoadScene(buildIndex);

    /// <summary>Load scene by name.</summary>
    public static void Load(string sceneName) =>
        SceneManager.LoadScene(sceneName);

    /// <summary>Reload the currently active scene.</summary>
    public static void Reload() =>
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    /// <summary>Advance to the next scene in Build Settings.</summary>
    public static void LoadNext()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        int next    = current + 1;

        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
    }

    /// <summary>Go back to the previous scene (if any).</summary>
    public static void LoadPrevious()
    {
        int current  = SceneManager.GetActiveScene().buildIndex;
        int previous = current - 1;

        if (previous >= 0)
            SceneManager.LoadScene(previous);
    }
}
