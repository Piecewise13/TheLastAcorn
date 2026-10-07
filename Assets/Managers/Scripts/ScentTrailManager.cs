using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Game-wide owner of scent trails. Lives on its own persistent prefab, spawned by GameManager,
/// so every scene with acorns has one. It knows nothing about the player: whoever is sensing
/// passes in the transform the trails should point at.
///
/// Two ways to get a trail:
///  - <see cref="RefreshTrails"/> / <see cref="HideTrails"/>: the player's senses. One trail per
///    acorn in range, kept up to date by repeated refreshes and all cleared together.
///  - <see cref="CreateTrail"/>: a single trail the caller owns (e.g. a tutorial beat). Its acorn
///    is skipped by RefreshTrails so the two never double up.
///
/// Each trail is placed in its acorn's scene, so unloading a scene takes its trails with it.
/// </summary>
public class ScentTrailManager : PersistentSingleton<ScentTrailManager>
{
    [SerializeField] private ScentTrail trailPrefab;

    [Tooltip("Layer mask for acorns (the Acorn layer).")]
    [SerializeField] private LayerMask acornMask;

    // Trails kept up by RefreshTrails, one per acorn in range. A trail survives across refreshes
    // while its acorn stays in range, so a length change blends instead of respawning.
    // NOTE (future optimization): trails are instantiated as acorns come into range and destroyed
    // as they leave. If this causes GC pressure, pool ScentTrail instances instead.
    private readonly Dictionary<Acorn, ScentTrail> sensedTrails = new Dictionary<Acorn, ScentTrail>();
    private readonly HashSet<Acorn> inRange = new HashSet<Acorn>();
    private readonly List<Acorn> leftRange = new List<Acorn>();

    // Trails handed out by CreateTrail and owned by their caller.
    private readonly List<ScentTrail> ownedTrails = new List<ScentTrail>();

    // Applied to every trail, including ones spawned later, so a trail that appears mid-sense glows too.
    private float glow = 1f;

    /// <summary>
    /// Keeps one trail from every active acorn within <paramref name="detectionRange"/> of
    /// <paramref name="origin"/>, each <paramref name="trailLength"/> world units long (capped at
    /// the distance to <paramref name="origin"/>). Existing trails blend to the new length, acorns
    /// that just came into range get a trail already at that length, and trails whose acorn left
    /// range fade out. Call it repeatedly; it is cheap when nothing changed.
    /// </summary>
    public void RefreshTrails(Transform origin, float detectionRange, float trailLength)
    {
        if (trailPrefab == null || origin == null)
        {
            HideTrails();
            return;
        }

        inRange.Clear();
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin.position, detectionRange, acornMask);

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<Acorn>(out var acorn)) continue;
            if (!acorn.gameObject.activeInHierarchy) continue;
            if (HasOwnedTrail(acorn)) continue;

            inRange.Add(acorn);

            if (sensedTrails.TryGetValue(acorn, out ScentTrail trail) && trail != null)
            {
                trail.SetLength(trailLength);
                continue;
            }

            trail = InstantiateTrail(acorn, origin, 1f, 0f);
            trail.SetLength(trailLength, instant: true);
            sensedTrails[acorn] = trail;
        }

        // Anything not seen this pass has left range, been collected, or been destroyed.
        leftRange.Clear();
        foreach (var entry in sensedTrails)
        {
            if (entry.Key == null || !inRange.Contains(entry.Key))
            {
                leftRange.Add(entry.Key);
            }
        }

        foreach (var acorn in leftRange)
        {
            if (sensedTrails[acorn] != null)
            {
                sensedTrails[acorn].FadeOutAndDestroy();
            }

            sensedTrails.Remove(acorn);
        }
    }

    /// <summary>Removes every trail kept by <see cref="RefreshTrails"/>. Owned trails are untouched.</summary>
    public void HideTrails()
    {
        foreach (var trail in sensedTrails.Values)
        {
            if (trail != null)
            {
                Destroy(trail.gameObject);
            }
        }

        sensedTrails.Clear();
    }

    /// <summary>Sets the colour multiplier on every trail, sensed and owned. Above 1 glows.</summary>
    public void SetGlow(float value)
    {
        glow = value;

        foreach (var trail in sensedTrails.Values)
        {
            if (trail != null) trail.SetGlow(glow);
        }

        foreach (var trail in ownedTrails)
        {
            if (trail != null) trail.SetGlow(glow);
        }
    }

    /// <summary>
    /// Spawns one trail from the acorn to <paramref name="origin"/>. The caller owns it: it is not
    /// cleared by <see cref="HideTrails"/>, so whoever asked for it must destroy it (or fade it out).
    /// </summary>
    public ScentTrail CreateTrail(Acorn acorn, Transform origin, float initialStrength = 1f, float initialReach = 1f)
    {
        ScentTrail trail = InstantiateTrail(acorn, origin, initialStrength, initialReach);
        ownedTrails.Add(trail);
        return trail;
    }

    private ScentTrail InstantiateTrail(Acorn acorn, Transform origin, float initialStrength, float initialReach)
    {
        ScentTrail trail = Instantiate(trailPrefab, acorn.transform.position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(trail.gameObject, acorn.gameObject.scene);

        trail.SetGlow(glow);
        trail.SetEndpoints(origin, acorn, initialStrength, initialReach);
        return trail;
    }

    private bool HasOwnedTrail(Acorn acorn)
    {
        ownedTrails.RemoveAll(trail => trail == null);
        return ownedTrails.Exists(trail => trail.Target == acorn);
    }

    protected override void OnDestroy()
    {
        if (Instance == this)
        {
            HideTrails();
        }

        base.OnDestroy();
    }
}
