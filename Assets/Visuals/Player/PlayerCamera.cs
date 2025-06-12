using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerKeyboardControls playerMovementMap;

    [SerializeField] private GameObject acornArrow;

    private InputAction zoomAction;

    private Camera cam;

    public PlayerMove playerMove;

    [SerializeField] private float zoomOutAmount;
    [SerializeField] private float zoomInAmount;

    private float targetZoom;

    [SerializeField] private float zoomSpeed;
    [SerializeField] private float zoomTime;

    private bool canZoom = true;

    [Header("Feedback")]
    [SerializeField] private AudioPlayer zoomInSFX;
    [SerializeField] private AudioPlayer zoomOutSFX;


    private float zoomTimer;

    void Awake()
    {
        playerMovementMap = new PlayerKeyboardControls();
        cam = GetComponent<Camera>();

        zoomAction = playerMovementMap.Keyboard.CameraZoom;
        zoomAction.performed += Zoom;
        zoomAction.canceled += Zoom;
        zoomAction.Enable();

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        targetZoom = zoomInAmount;
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

        if (!canZoom)
        {
            return;
        }

        if (context.performed)
        {
            acornArrow.SetActive(true);
            playerMove.DisableMove();
            targetZoom = zoomOutAmount;
            zoomTimer = 0;
            zoomOutSFX?.Play();
        }
        else if (context.canceled)
        {
            acornArrow.SetActive(false);
            playerMove.EnableMove();
            targetZoom = zoomInAmount;
            zoomTimer = 0;
            zoomInSFX?.Play();
        }
    }

    public void StartForceZoom(float newZoom)
    {
        canZoom = false;
        this.targetZoom = newZoom;
        zoomTimer = 0;
    }

    public void EndForceZoom()
    {
        canZoom = true;
        targetZoom = zoomInAmount;
        zoomTimer = 0;
    }

    public void DisableZoom()
    {
        canZoom = false;
        zoomAction.Disable();
    }

    public void EnableZoom()
    {
        canZoom = true;
        zoomAction.Enable();
    }
    
}
