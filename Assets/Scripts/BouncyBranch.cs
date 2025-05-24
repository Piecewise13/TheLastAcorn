using UnityEngine;

public class BouncyBranch : MonoBehaviour
{
    
    public float launchMultiplier = 2.0f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Rigidbody2D rb = other.transform.root.GetComponent<Rigidbody2D>();
            if (rb != null)
            {

                float upwardVelocity = Mathf.Abs(rb.linearVelocity.y);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, upwardVelocity * launchMultiplier);
            }
        }
    }
}
