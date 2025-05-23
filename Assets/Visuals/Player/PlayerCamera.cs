using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    /// <summary>
    /// Reference to the PlayerControls input action map.
    /// </summary>
    private PlayerKeyboardControls playerMovementMap;

    private InputAction zoomAction;

    private Camera cam;

    public PlayerMove playerMove;

    [SerializeField] private float zoomOutAmount;
    [SerializeField] private float zoomInAmount;

    private float targetZoom;

    [SerializeField] private float zoomSpeed;
    [SerializeField] private float zoomTime;
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
        if (context.performed)
        {
            playerMove.DisableMove();
            targetZoom = zoomOutAmount;
            zoomTimer = 0;
        }  else if (context.canceled)
        {
            playerMove.EnableMove();
            targetZoom = zoomInAmount;
            zoomTimer = 0;
        }
    }
}
