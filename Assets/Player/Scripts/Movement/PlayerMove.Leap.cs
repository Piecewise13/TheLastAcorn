using UnityEngine;

namespace Player
{
public partial class PlayerMoveManager: MonoBehaviour
{

    [Header("Tree Leap")]
    [Tooltip("Upward force applied when leaping from tree")]
    [SerializeField] private float leapUpwardForce = 8f;
    [Tooltip("Multiplier for horizontal leap velocity based on climb speed")]
    [SerializeField] private float leapHorizontalMultiplier = 2f;

     /// <summary>
    /// Handles leaping from a tree with horizontal momentum based on climb speed.
    /// </summary>
    private void LeapFromTree()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        
        // Determine leap direction from horizontal input
        float leapDirection = 0f;
        if (moveInput.x != 0)
        {
            leapDirection = Mathf.Sign(moveInput.x);
        }
        else
        {
            // If no input, leap away from tree based on current facing direction
            leapDirection = graphic.transform.eulerAngles.y == 0 ? 1f : -1f;
        }
        
        // Calculate horizontal leap velocity based on current climb speed
        float horizontalLeapVelocity = currentClimbSpeed * leapHorizontalMultiplier * leapDirection;
        
        // Stop climbing and enable physics
        StopClimb();
        
        // Apply leap forces
        rb.linearVelocity = new Vector2(horizontalLeapVelocity, 0f);
        rb.AddForce(Vector2.up * leapUpwardForce, ForceMode2D.Impulse);
        
        // Flip graphic based on leap direction
        if (moveInput.x != 0)
        {
            FlipGraphic(moveInput.x);
        }
        
        Jumped?.Invoke();
        
        // Trigger jump animation
        animator.SetTrigger("Jump");
        animator.SetBool("isFalling", true);
        
        jumpBufferTimer = jumpBufferTime;
        
        Debug.Log($"Leaped from tree! Horizontal velocity: {horizontalLeapVelocity:F1}, Climb speed was: {currentClimbSpeed:F1}");
    }
}
}
