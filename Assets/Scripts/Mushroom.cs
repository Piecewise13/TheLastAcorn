using UnityEngine;

public class Mushroom : MonoBehaviour
{

    private Animator animator;
    [SerializeField] private float bounceForce = 15f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator component not found on Mushroom object.");
        }
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

        animator.SetTrigger("Bounce");

        var root = collision.gameObject.transform.root;
        var rb = root.GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero; // Reset velocity to prevent unwanted movement

        rb.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);
    }
}
