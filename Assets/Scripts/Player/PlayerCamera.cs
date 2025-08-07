using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerGameControls playerMovementMap;

    [SerializeField] private GameObject acornArrow;

    private InputAction zoomAction;

    private Camera cam;

    public PlayerMove playerMove;

    private Rigidbody2D rb;

    [SerializeField] private float zoomOutAmount;
    [SerializeField] private float zoomInAmount;

    private float targetZoom;

    [SerializeField] private float zoomSpeed;
    [SerializeField] private float zoomTime;

    private bool canZoom = true;

    [SerializeField] private CameraState cameraState = CameraState.Default;

    [Header("Feedback")]
    [SerializeField] private AudioPlayer zoomInSFX;
    [SerializeField] private AudioPlayer zoomOutSFX;


    private float zoomTimer;

    void Awake()
    {
        playerMovementMap = new PlayerGameControls();
        cam = GetComponent<Camera>();

        zoomAction = playerMovementMap.Gameplay.CameraZoom;
        zoomAction.performed += Zoom;
        zoomAction.canceled += Zoom;
        zoomAction.Enable();

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        targetZoom = zoomInAmount;
        rb = GetComponentInParent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        if (zoomTimer > zoomTime)
        {
            return;
        }

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, zoomTimer / zoomTime);
        zoomTimer += Time.deltaTime;
    }

    private void Zoom(InputAction.CallbackContext context)
    {

        if (cameraState == CameraState.Disabled || cameraState == CameraState.CaveZoomed)
        {
            return;
        }

        if (context.performed)
        {
            cameraState = CameraState.PlayerZoomed;
            acornArrow.SetActive(true);
            playerMove.DisableMove();
            targetZoom = zoomOutAmount;
            zoomTimer = 0;
            zoomOutSFX?.Play();
        }
        else if (context.canceled)
        {
            cameraState = CameraState.Default;
            acornArrow.SetActive(false);
            playerMove.EnableMove();
            targetZoom = zoomInAmount;
            zoomTimer = 0;
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
        CaveZoomed,
        GlideZoom
    }

}
