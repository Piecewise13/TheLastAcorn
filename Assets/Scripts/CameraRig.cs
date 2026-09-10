using Unity.Cinemachine;
using UnityEngine;

public class CameraRig : SceneService<CameraRig>
{
    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private Camera foregroundCamera;
    [SerializeField] private Camera backgroundCamera;
    [SerializeField] private Camera overlayCamera;
    [SerializeField] private CinemachineBrain brain;

    public CinemachineCamera Vcam => vcam;
    public Camera Foreground => foregroundCamera;
    public Camera Background => backgroundCamera;
    public Camera Overlay => overlayCamera;

    /// <summary>
    /// The claim backing <see cref="SetTrackingTarget"/>. These two methods predate
    /// <see cref="CameraDirector"/> and are kept as a bridge so their existing callers — the ability
    /// unlock, the upgrade flow, and the unlock view — keep their set/reset shape while still going
    /// through the arbiter. New code should request its own claim instead.
    /// </summary>
    private CameraClaim cinematicClaim;

    void Start()
    {
        // Reset in Start rather than Awake: the director resolves via the registry, so waiting a
        // step lets same-scene registrations settle before the rig asks for its director.
        ResetTrackingTarget();
    }

    public void SetTrackingTarget(Transform target)
    {
        CameraDirector director = CameraDirector.For(gameObject.scene);
        if (director == null)
        {
            vcam.Target.TrackingTarget = target;
            return;
        }

        cinematicClaim ??= director.Request(CameraPriority.Cinematic);
        cinematicClaim.SetTarget(target);
    }

    public void ResetTrackingTarget()
    {
        // The director owns the follow target now, so a reset just drops the cinematic claim and lets
        // the director fall back to its default player follow. With no director there is nothing to reset through.
        cinematicClaim?.Release();
        cinematicClaim = null;
    }
}
