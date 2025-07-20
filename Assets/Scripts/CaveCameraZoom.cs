using UnityEngine;

public class CaveCameraZoom : MonoBehaviour
{

    [SerializeField] private float cameraZoomAmount;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        var otherRoot = collision.transform.root;
        if (otherRoot.CompareTag("Player"))
        {
            var playerCamera = otherRoot.GetComponentInChildren<PlayerCamera>();
            if (playerCamera != null)
            {
                playerCamera.StartForceZoom(cameraZoomAmount, PlayerCamera.CameraState.CaveZoomed);
            }
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        var otherRoot = collision.transform.root;
        if (otherRoot.CompareTag("Player"))
        {
            var playerCamera = otherRoot.GetComponentInChildren<PlayerCamera>();
            if (playerCamera != null)
            {
                playerCamera.EndForceZoom(PlayerCamera.CameraState.CaveZoomed);
            }
        }
    }
}
