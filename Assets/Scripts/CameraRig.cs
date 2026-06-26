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
    
    public CinemachineCamera Vcam => vcam;
    public Camera Foreground => foregroundCamera;
    public Camera Background => backgroundCamera;
    public Camera Overlay => overlayCamera;
    
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
}
