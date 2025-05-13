using UnityEngine;

public class RespawnTrigger : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && respawnPoint != null)
        {
            other.transform.position = respawnPoint.position;
            other.transform.rotation = respawnPoint.rotation;
        }
    }
}
