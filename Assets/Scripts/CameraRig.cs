using Unity.Cinemachine;
using UnityEngine;

public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }
    
    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private Camera foregroundCamera;
    [SerializeField] private Camera backgroundCamera;
    [SerializeField] private Camera overlayCamera;
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private Transform boundedCameraTarget;
    
    public CinemachineCamera Vcam => vcam;
    public Camera Foreground => foregroundCamera;
    public Camera Background => backgroundCamera;
    public Camera Overlay => overlayCamera;
    public Transform BoundedCameraTarget => boundedCameraTarget;

    /// <summary>
    /// The claim backing <see cref="SetTrackingTarget"/>. These two methods predate
    /// <see cref="CameraDirector"/> and are kept as a bridge so their existing callers — the ability
    /// unlock, the upgrade flow, and the unlock view — keep their set/reset shape while still going
    /// through the arbiter. New code should request its own claim instead.
    /// </summary>
    private CameraClaim cinematicClaim;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SetTrackingTarget(Transform target)
    {
        CameraDirector director = CameraDirector.Instance;
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
        if (CameraDirector.Instance == null)
        {
            vcam.Target.TrackingTarget = boundedCameraTarget;
            return;
        }

        cinematicClaim?.Release();
        cinematicClaim = null;
    }
}
