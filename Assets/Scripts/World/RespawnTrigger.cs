using UnityEngine;
using Player;

public class RespawnTrigger : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.transform.root.CompareTag("Player")) return;

        PlayerLifeManager.Instance?.DamagePlayerAndRelocate(respawnPoint.position);
    }
}
