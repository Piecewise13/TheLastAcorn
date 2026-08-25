using System;
using UnityEngine;

/// <summary>
/// Priority order for camera claims. The highest active priority wins; ties break toward the most
/// recently requested claim, which is what makes overlapping room zones resolve without any
/// authoring rules.
/// </summary>
public enum CameraPriority
{
    /// <summary>Speed-based zoom while gliding. Deliberately below room zones so a cave room's
    /// authored framing is never stolen by how fast the player happens to be moving.</summary>
    GlideZoom = 0,
    RoomZone = 10,
    PlayerZoom = 20,
    EventZone = 30,
    Cinematic = 40
}

public enum CameraBlendMode
{
    /// <summary>Fixed duration shaped by a curve. Arrives, rather than approaching forever.</summary>
    Duration,
    /// <summary>Frame-rate-independent exponential smoothing. Never formally arrives.</summary>
    Exponential,
    Instant
}

/// <summary>
/// A request to control the camera's tracking target and zoom, held for as long as its owner needs
/// it. Releasing falls back to whatever claim sits underneath, so "nothing is claiming the camera"
/// is an empty stack rather than a state anyone has to handle.
///
/// Values stay mutable while the claim is held because some callers update every frame — the glide
/// zoom recomputes its size from the player's speed continuously.
/// </summary>
public sealed class CameraClaim : IDisposable
{
    private readonly CameraDirector director;

    internal CameraClaim(CameraDirector director, CameraPriority priority, long sequence)
    {
        this.director = director;
        Priority = priority;
        Sequence = sequence;
        BlendMode = CameraBlendMode.Duration;
    }

    public CameraPriority Priority { get; }

    /// <summary>Request order, used to break priority ties toward the newest claim.</summary>
    internal long Sequence { get; }

    public bool Released { get; private set; }

    /// <summary>Where the camera should sit. Null leaves the position to whatever is underneath.</summary>
    public Transform Target { get; private set; }

    /// <summary>Orthographic half-height. Null leaves the zoom to whatever is underneath.</summary>
    public float? OrthographicSize { get; private set; }

    public CameraBlendMode BlendMode { get; private set; }

    /// <summary>Used by <see cref="CameraBlendMode.Duration"/>.</summary>
    public float Duration { get; private set; }

    /// <summary>Used by <see cref="CameraBlendMode.Duration"/>. Null falls back to the director's default.</summary>
    public AnimationCurve Curve { get; private set; }

    /// <summary>Used by <see cref="CameraBlendMode.Exponential"/>.</summary>
    public float Rate { get; private set; }

    public CameraClaim WithTarget(Transform target)
    {
        Target = target;
        return this;
    }

    public CameraClaim WithOrthographicSize(float size)
    {
        OrthographicSize = size;
        return this;
    }

    public CameraClaim WithDurationBlend(float duration, AnimationCurve curve = null)
    {
        BlendMode = CameraBlendMode.Duration;
        Duration = duration;
        Curve = curve;
        return this;
    }

    public CameraClaim WithExponentialBlend(float rate)
    {
        BlendMode = CameraBlendMode.Exponential;
        Rate = rate;
        return this;
    }

    public CameraClaim WithInstantBlend()
    {
        BlendMode = CameraBlendMode.Instant;
        return this;
    }

    /// <summary>Retargets a held claim. Safe to call every frame.</summary>
    public void SetTarget(Transform target) => Target = target;

    /// <summary>Resizes a held claim. Safe to call every frame.</summary>
    public void SetOrthographicSize(float size) => OrthographicSize = size;

    public void Release()
    {
        if (Released) return;
        Released = true;
        director.Release(this);
    }

    public void Dispose() => Release();
}
