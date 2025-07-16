using UnityEngine;

public class WindGust : MonoBehaviour
{

    [SerializeField] private float glideGustForce = 100f;

    [SerializeField] private float idleGustForce = 50f;

    private bool hasPlayer = false;
    private Rigidbody2D playerRb;
    private PlayerMove playerMove;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerMove = FindAnyObjectByType<PlayerMove>();
    }


    void FixedUpdate()
    {
        if (!hasPlayer)
        {
            return;
        }

        float gustForce = playerMove.GetPlayerState() == PlayerState.Glide ? glideGustForce : idleGustForce;

        //playerRb.AddForce(transform.right * gustForce, ForceMode2D.Force);
        playerRb.linearVelocity = playerRb.linearVelocity + (Vector2)transform.right * gustForce * Time.fixedDeltaTime;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
        {
            return;
        }

        hasPlayer = true;

        var root = collision.transform.root;

        playerRb = root.GetComponent<Rigidbody2D>();
        //playerRb.linearVelocity = Vector2.zero; // Reset velocity when entering gust

        playerMove.EnterGust();
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        hasPlayer = false;
        playerMove.ExitGust();
    }
}
