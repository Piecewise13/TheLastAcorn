using System;
using UnityEngine;

public class LilyPad : MonoBehaviour, IProximityAlert
{


    private Rigidbody2D rb;



    [SerializeField] private float bounceForce = 2f;

    private float startingY;


    [SerializeField] private float horizontalFriction;


    [SerializeField] private LilyPadState lilyPadState = LilyPadState.Idle;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnCollisionEnter2D(Collision2D collision)
    {

        Vector2 colVelo = collision.relativeVelocity;

        var colGameObject = collision.gameObject.transform.root.gameObject;

        var colRb = colGameObject.GetComponent<Rigidbody2D>();
        if (colRb != null)
        {
            colRb.linearVelocity = new Vector2(colVelo.x, Math.Abs(colVelo.y * bounceForce));
        }
        
    }

    public void PlayerInProximity(GameObject player)
    {

        if (lilyPadState != LilyPadState.Idle)
        {
            return;
        }

        lilyPadState = LilyPadState.Contact;

    }

    public void PlayerOutOfProximity(GameObject player)
    {
        lilyPadState = LilyPadState.Idle;
    }

    private enum LilyPadState
    {
        Idle,
        Contact,
        Sinking
    }
}
