using UnityEngine;

public class Spike : MonoBehaviour
{



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
            var root = other.transform.root;

            PlayerLifeManager lifeManager = root.GetComponent<PlayerLifeManager>();
            lifeManager.DamagePlayer(transform.up);

        }
    }
}
