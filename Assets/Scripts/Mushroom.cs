using System.Collections;
using UnityEngine;

public class Mushroom : MonoBehaviour
{

    private Animator animator;
    [SerializeField] private float bounceForce = 15f;
    
    [Header("Feedback")] 
    [SerializeField] private AudioPlayer mushroomSFX;
    [SerializeField] private GrowAndShrink growAndShrink;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError("Animator component not found on Mushroom object.");
    }
    
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        animator.SetTrigger("Bounce");

        mushroomSFX?.Play();
        var root = collision.gameObject.transform.root;
        var rb = root.GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;

        rb.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);
    }

    private IEnumerator ResizeShroom(float time)
    {
        growAndShrink?.Grow();
        yield return new WaitForSeconds(0.5f);
        growAndShrink?.Shrink();
        
    }
}
