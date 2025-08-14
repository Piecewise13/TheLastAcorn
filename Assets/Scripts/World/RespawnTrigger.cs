using UnityEngine;

public class RespawnTrigger : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.transform.root;

        PlayerLifeManager playerLifeManager = player.GetComponent<PlayerLifeManager>();
        playerLifeManager.DamagePlayerAndRelocate(respawnPoint.position);
    }
}
