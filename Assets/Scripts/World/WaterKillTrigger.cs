using UnityEngine;


public class WaterKillTrigger : MonoBehaviour
{

    [SerializeField] private float killDelay = 0.5f;

    [SerializeField] private Transform respawnPoint;


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
        var root = collision.transform.root;
        var playerLifeManager = root.GetComponent<PlayerLifeManager>();
        playerLifeManager.RespawnPlayer(respawnPoint.position);
    }


}
