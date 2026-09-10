using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one thing that ticks every <see cref="ScatterLineBase"/>'s parallax. Each line used to run its
/// own LateUpdate and resolve the camera itself; across two full levels that is dozens of update loops
/// and dozens of camera look-ups per frame. Instead every active line registers here, and this driver
/// resolves the foreground camera once per frame and pushes it to all of them.
///
/// It bootstraps itself: the first line to register creates a hidden, DontDestroyOnLoad host, so no
/// scene has to carry one and additively-loaded levels share the same driver. Runs at a late execution
/// order so Cinemachine has already moved the camera. Play mode only — nothing parallaxes in the editor.
/// </summary>
[DefaultExecutionOrder(1000)]
public class ScatterLineParallaxDriver : MonoBehaviour
{
    private static ScatterLineParallaxDriver instance;

    private readonly List<ScatterLineBase> lines = new List<ScatterLineBase>();
    private Camera foreground;

    public static void Register(ScatterLineBase line)
    {
        if (line == null)
            return;

        EnsureInstance();
        if (!instance.lines.Contains(line))
            instance.lines.Add(line);
    }

    public static void Unregister(ScatterLineBase line)
    {
        if (instance != null && line != null)
            instance.lines.Remove(line);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("ScatterLineParallaxDriver") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ScatterLineParallaxDriver>();
    }

    private void LateUpdate()
    {
        Camera view = ResolveCamera();
        if (view == null)
            return;

        Vector3 cameraPos = view.transform.position;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] != null)
                lines[i].ApplyParallax(cameraPos);
        }
    }

    /// <summary>
    /// The foreground camera, resolved late and re-resolved when it goes away. The rig can arrive
    /// after the lines it drives — an additive load, or a scene opened on its own — and a cached
    /// camera is destroyed when its scene unloads, which Unity's null check reports, so this keeps
    /// asking rather than holding a dead reference.
    /// </summary>
    private Camera ResolveCamera()
    {
        if (foreground != null)
            return foreground;

        CameraRig rig = CameraRig.Current;
        foreground = rig != null ? rig.Foreground : null;
        return foreground;
    }
}
