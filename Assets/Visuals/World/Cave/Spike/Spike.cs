using UnityEngine;
using Player;

public class Spike : MonoBehaviour
{

    [SerializeField] private float damageLaunch = 30f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerLifeManager.Instance?.DamagePlayer(transform.up * damageLaunch);

        }
    }
}
