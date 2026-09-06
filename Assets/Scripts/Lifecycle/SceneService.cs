using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Base for managers that are scoped to a single loaded scene rather than the whole game. Where a
/// plain <c>static Instance</c> assumes exactly one live instance forever, this keeps one instance
/// <em>per scene</em> in a registry so additive loading — two scenes alive at once, each with its own
/// camera stack — resolves the right one instead of whichever woke up first.
///
/// Callers pick their resolution deliberately:
/// <list type="bullet">
/// <item><see cref="Current"/> — the instance in the active gameplay scene. For external callers
/// (player, views, world) that mean "whatever scene is live now".</item>
/// <item><see cref="For"/> — the instance in a specific scene. For a service wiring up to a sibling
/// in its own scene (<c>For(gameObject.scene)</c>), which must not drift to another scene mid-load.</item>
/// </list>
///
/// Generic statics are per-closed-type, so <c>SceneService&lt;CameraRig&gt;</c> and
/// <c>SceneService&lt;CameraDirector&gt;</c> each get their own registry.
/// </summary>
public abstract class SceneService<T> : MonoBehaviour where T : SceneService<T>
{
    private static readonly Dictionary<Scene, T> byScene = new();

    /// <summary>The instance belonging to the active gameplay scene, or null if none is registered.</summary>
    public static T Current => For(SceneManager.GetActiveScene());

    /// <summary>The instance belonging to <paramref name="scene"/>, or null if none is registered.</summary>
    public static T For(Scene scene)
    {
        // `s != null` uses Unity's overload, so a destroyed-but-not-yet-unregistered entry (e.g. when
        // domain reload is disabled and statics survive) reads as absent rather than handing back a
        // dead object.
        return byScene.TryGetValue(scene, out T s) && s != null ? s : null;
    }

    /// <summary>Every registered instance across all loaded scenes.</summary>
    public static IEnumerable<T> All => byScene.Values;

    protected virtual void OnEnable()
    {
        Scene scene = gameObject.scene;
        if (byScene.TryGetValue(scene, out T existing) && existing != null && existing != this)
        {
            Debug.LogWarning($"[{typeof(T).Name}] A second instance in scene '{scene.name}' was ignored. " +
                             "Scene services are one-per-scene.", this);
            return; // First registered in a scene wins; the newcomer simply stays unregistered.
        }

        byScene[scene] = (T)this;
    }

    protected virtual void OnDisable()
    {
        Scene scene = gameObject.scene;
        if (byScene.TryGetValue(scene, out T s) && s == this)
        {
            byScene.Remove(scene);
        }
    }
}
