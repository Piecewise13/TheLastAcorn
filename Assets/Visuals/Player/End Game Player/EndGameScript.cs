using System.Collections;
using UnityEngine;

public class EndGameScript : MonoBehaviour
{

    PlayerMove playerMove;
    PlayerCamera playerCamera;

    public GameObject endCredits;

    public Animator playerAnim;
    public Animator cameraAnim;

    public GameObject playerCanvas;

    [SerializeField] private AudioPlayer ambience;
    [SerializeField] private AudioPlayer music;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerMove = FindFirstObjectByType<PlayerMove>();
        playerCamera = FindFirstObjectByType<PlayerCamera>();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.transform.root.CompareTag("Player")) return;
        
        playerMove.DisableMove();
        FadeAudio();
        playerCamera.enabled = false;
        playerAnim.keepAnimatorStateOnDisable = false;
        playerAnim.Play("Idle", 0, 0f); // Replace "EntryStateName" with your actual entry animation state name
        playerAnim.playbackTime = 0f; // Reset playback time
        playerAnim.Update(0f); // Force an update at time 0

        playerCanvas.SetActive(false);

        playerAnim.enabled = false;
        cameraAnim.enabled = true;
        endCredits.SetActive(true);
    }

    private void FadeAudio()
    {
        ambience?.FadeOut(1f);
        music?.FadeOut(1f);
    }
    
}
