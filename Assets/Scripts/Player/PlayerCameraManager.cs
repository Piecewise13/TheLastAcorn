using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class PlayerCameraManager : MonoBehaviour
{
    /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerGameControls playerMovementMap;
    
    public event Action OnZoomStarted;
    public event Action OnZoomEnded;

    private PlayerAbilityManager abilityManager;
    private InputAction zoomAction;

    private Camera backgroundCam;

    private CinemachineCamera cinemachineCam;
    [SerializeField] private Transform defaultTrackingTarget;

    [SerializeField] float zoomPerspectiveShift = 80f;

    [SerializeField] public PlayerMove playerMove;

    private Rigidbody2D rb;

    [SerializeField] private float zoomOutAmount;
    [SerializeField] private float zoomInAmount;

    private float targetZoom;

    [SerializeField] private float zoomSpeed;
    [SerializeField] private float zoomTime;

    [SerializeField] private float backgroundFOVMultiplier = 0.3f; // Controls how much the background FOV scales with zoom

    [SerializeField] private CameraState cameraState = CameraState.Default;

    [Header("Feedback")]
    [SerializeField] private AudioPlayer zoomInSFX;
    [SerializeField] private AudioPlayer zoomOutSFX;


    private float zoomTimer;

    void Awake()
    {
        playerMovementMap = new PlayerGameControls();
        
        zoomAction = playerMovementMap.Gameplay.CameraZoom;
        zoomAction.performed += Zoom;
        zoomAction.canceled += Zoom;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        targetZoom = zoomInAmount;

        var rig = CameraRig.Instance;
        cinemachineCam = rig.Vcam;
        backgroundCam = rig.Background;
        cinemachineCam.Target.TrackingTarget = defaultTrackingTarget;

        playerMove = GetComponentInParent<PlayerMove>();
        abilityManager = GetComponentInParent<PlayerAbilityManager>();
        rb = GetComponentInParent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        float currentSize = cinemachineCam.Lens.OrthographicSize;

        if (Mathf.Approximately(currentSize, targetZoom))
        {
            return;
        }

        // Frame-rate-independent exponential smoothing toward the target zoom.
        float t = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
        float newSize = Mathf.Lerp(currentSize, targetZoom, t);
        if (Mathf.Abs(newSize - targetZoom) < 0.01f)
        {
            newSize = targetZoom;
        }
        cinemachineCam.Lens.OrthographicSize = newSize;
        CameraRig.Instance.Overlay.orthographicSize = newSize;

        // Scale perspective camera FOV proportionally with orthographic size
        // Base FOV of 125 at default zoom level (zoomInAmount)
        float zoomRatio = newSize / zoomInAmount;
        float fovScale = 1f + (zoomRatio - 1f) * backgroundFOVMultiplier;
        backgroundCam.fieldOfView = zoomPerspectiveShift * fovScale;
    }

    #region Zoom Unlock

    public async UniTask WaitForZoomInput(CancellationToken token)
    {
        cameraState = CameraState.Default;
        var completionSource = new UniTaskCompletionSource();
        void OnPerformed(InputAction.CallbackContext _) => completionSource.TrySetResult();
        zoomAction.Enable();
        zoomAction.performed += OnPerformed;
        try
        {
            await completionSource.Task.AttachExternalCancellation(token);
        }
        finally
        {
            zoomAction.performed -= OnPerformed;
        }
    }
    
    public async UniTask PerformUnlockZoom(CancellationToken token)
    {
        targetZoom = zoomOutAmount;
        zoomTimer = 0;
        zoomOutSFX?.Play();
        await UniTask.WaitForSeconds(zoomTime, cancellationToken: token);
    }

    #endregion

    
    private void Zoom(InputAction.CallbackContext context)
    {

        if (cameraState == CameraState.Disabled || cameraState == CameraState.CaveZoomed)
        {
            return;
        }

        if (context.performed)
        {
            cameraState = CameraState.PlayerZoomed;

            OnZoomStarted?.Invoke();
            playerMove.DisableMove();
            targetZoom = zoomOutAmount;
            zoomTimer = 0;
            zoomOutSFX?.Play();
        }
        else if (context.canceled)
        {
            Debug.Log("Zoom Cancelled");
            cameraState = CameraState.Default;
            playerMove.EnableMove();
            targetZoom = zoomInAmount;
            zoomTimer = 0;

            OnZoomEnded?.Invoke();

            zoomInSFX?.Play();
        }
    }

    public void StartForceZoom(float newZoom, CameraState state)
    {
        if (cameraState == CameraState.Disabled)
        {
            return;
        }

        if (cameraState == state)
        {
            this.targetZoom = newZoom;
            zoomTimer = 0;
            return;
        }

        if (cameraState == CameraState.CaveZoomed)
        {
            return;
        }


        cameraState = state;
        this.targetZoom = newZoom;
        zoomTimer = 0;
    }

    public void SetCameraTarget(GameObject target)
    {
        cinemachineCam.Target.TrackingTarget = target.transform;
    }

    public void ResetTrackingTarget()
    {
        cinemachineCam.LookAt = defaultTrackingTarget;
    }
    

    public void EndForceZoom(CameraState state)
    {
        if(cameraState != state){
            return;
        }

        cameraState = CameraState.Default;
        targetZoom = zoomInAmount;
        zoomTimer = 0;
    }

    public void DisableZoom()
    {
        cameraState = CameraState.Disabled;
        zoomAction.Disable();
    }

    public void EnableZoom()
    {
        cameraState = CameraState.Default;
        zoomAction.Enable();
    }
    public float GetDefaultZoom()
    {
        return zoomInAmount;
    }

    public enum CameraState
    {
        Disabled,
        Default,
        PlayerZoomed,
        UnlockPending,
        CaveZoomed,
        GlideZoom
    }

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private UnlockZoomView zoomView;

    [NaughtyAttributes.Button("Ability Unlock Test")]
    private async void UnlockAbilityTest()
    {
        await OverlayCameraController.Instance.RequestPlayerOverlay();
        ViewManager.Instance.PushView(zoomView).Forget();
    }
#endif

}
