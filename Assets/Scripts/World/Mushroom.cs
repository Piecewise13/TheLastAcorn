using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using UnityEngine;

public class Mushroom : MonoBehaviour
{
    [SerializeField] private Animator animator = null!;
    [SerializeField] private float bounceForce = 15f;

    [Header("Glow Effects")] 
    [SerializeField]private float glowDuration;
    private float glowTimer;
    private bool isGlowLocked;

    [Header("Feedback")] 
    [SerializeField] private MMF_Player onBounceFeedback = null!;
    [SerializeField] private MMF_Player enterGlowFeedback = null!;
    [SerializeField] private MMF_Player onExitGlow;
    [SerializeField] private AudioPlayer mushroomSFX;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError("Animator component not found on Mushroom object.");
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
       // mushroomSFX?.Play();
       
       onBounceFeedback.PlayFeedbacks();
       //StartGlow();
       LaunchPlayer(collision.gameObject.transform.root.gameObject);
    }

    private void Update()
    {
        if (isGlowLocked)
        {
           // EvaulateGlow();
        }
    }

    private async UniTask EvaulateGlow()
    {
        if (glowTimer <= 0)
        {
            await onExitGlow.PlayFeedbacksAsync(destroyCancellationToken);
            animator.SetTrigger("Normal");
            isGlowLocked = false;
            return;
        }

        glowTimer -= Time.deltaTime;
    }

    private void StartGlow()
    {
        if (!isGlowLocked)
        {
            isGlowLocked = true;
            enterGlowFeedback.PlayFeedbacks();
            return;
        }
        
        glowTimer = glowDuration;
    }

    private void LaunchPlayer(GameObject player)
    {
        var rb = player.GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);
    }
}
