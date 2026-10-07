using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tells the scene Play started in apart from scenes reached through the game (a level trigger, the
/// menu, a restart). Debug spawn shortcuts check it so they only ever apply to the scene you hit
/// Play in, and a scene the player travels to always starts them at its start point.
///
/// Remembers the first scene itself rather than counting scene loads: with Enter Play Mode Options
/// on (as this project has), Play does not reload the open scene, so sceneLoaded never fires for it.
/// </summary>
public static class SceneEntry
{
    private static Scene playStartScene;

    /// <summary>
    /// True while the scene Play started in is still the loaded one. A reload of that same scene
    /// counts as travelled to, since loading gives it a new handle. Valid from Start onward.
    /// </summary>
    public static bool IsPlayStartScene => SceneManager.GetActiveScene() == playStartScene;

    // Statics survive between Play sessions when domain reload is off, so clear at every Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => playStartScene = default;

    // Runs once per Play, after the first scene's Awake and before its Start.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CapturePlayStartScene() => playStartScene = SceneManager.GetActiveScene();
}
