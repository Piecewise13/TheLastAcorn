using UnityEngine;

public class GateCharge : MonoBehaviour
{
    
    [SerializeField] private Gate gate;

    private bool isCollected = false;

    PlayerMove playerMove;

    SpriteRenderer spriteRenderer;

    [SerializeField] private Color originalColor;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void Collect(){
        spriteRenderer.color = new Color(1f, 1f, 1f, 0.5f);
        gate.Collect(this);
        isCollected = true;
    }

    public void ResetCharge()
    {
        print("Resetting gate charge.");
        spriteRenderer.color = originalColor;
        isCollected = false;
    }

}
