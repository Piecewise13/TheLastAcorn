using UnityEngine;

public class Mushroom : MonoBehaviour
{

    [SerializeField] private float bounceForce = 15f;

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
        if (!collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        var root = collision.gameObject.transform.root;
        var rb = root.GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero; // Reset velocity to prevent unwanted movement

        rb.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);
    }
}
