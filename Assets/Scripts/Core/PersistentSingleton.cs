using UnityEngine;

/// <summary>
/// Game-wide service that survives scene loads; exactly one per session.
/// Not for scene-bound managers (camera rig, overlay, player body) — under additive
/// loading there can be several of those alive at once, so they use a scene registry instead.
/// </summary>
public abstract class PersistentSingleton<T> : MonoBehaviour where T : PersistentSingleton<T>
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        // First-wins: a later duplicate (e.g. a copy hand-placed in a scene) destroys itself.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = (T)this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        // Clear only if we own the static, so a dying duplicate can't null a live instance.
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
