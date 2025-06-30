using UnityEngine;

public class RespawnTrigger : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.transform.root;
        if (player.CompareTag("Player") && respawnPoint != null)
        {
            player.transform.root.position = respawnPoint.position;
            player.transform.root.rotation = respawnPoint.rotation;
        }
    }
}
