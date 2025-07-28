using System;
using System.Collections;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class LilyPad : MonoBehaviour
{


    private Rigidbody2D rb;

    private SpriteRenderer spriteRenderer;

    private Collider2D collider;

    private Vector2 initialPosition;

    [SerializeField] private float bounceForce = 2f;


    [SerializeField] private float sinkTime = 2f; // Time it takes for the lily pad to sink
    [SerializeField] private float sinkDistance = 0.5f; // Distance to sink down

    [SerializeField] private float disabledTime = 2f; // Time the lily pad remains disabled after sinking

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        collider = GetComponent<Collider2D>();
        initialPosition = transform.position;
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
            StartCoroutine(SinkLilyPad(sinkTime, disabledTime));
        }
    }


    IEnumerator SinkLilyPad(float sinkTime, float disabledTime)
    {

        collider.enabled = false;
        float elapsedTime = 0f;


        while (elapsedTime < sinkTime)
        {
            float t = elapsedTime / sinkTime;
            transform.position = Vector2.Lerp(initialPosition, initialPosition + Vector2.down * sinkDistance, t);
            elapsedTime += Time.deltaTime;
            spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1 - t); // Fade out
            yield return null;
        }

        yield return new WaitForSeconds(disabledTime);

        spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f); // Ensure fully transparent
        transform.position = initialPosition;
        collider.enabled = true;
    }
}
