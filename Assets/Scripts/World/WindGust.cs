using UnityEngine;

public class WindGust : MonoBehaviour
{

    [SerializeField] private float gustForce = 100f;

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

        playerRb.AddForce(transform.right * gustForce, ForceMode2D.Force);
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

        playerMove.EnterGust();
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        hasPlayer = false;
        playerMove.ExitGust();
    }
}
