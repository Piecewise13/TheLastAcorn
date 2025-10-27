using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{

    private Animator anim;

    [Header("Success Feedback")]
    [SerializeField] private ParticleSystem frontFireFly;
    [SerializeField] private ParticleSystem backFireFly;
    [SerializeField] private GrowAndShrink growAndShrink;
    [SerializeField] private AudioPlayer sfxPlayer;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CheckpointManager.Instance.SetCheckpoint(transform.position);


            SaveLoadManager.SaveCollectedAcorns();

            anim.SetBool("isActive", true);
            backFireFly?.Play();
            frontFireFly?.Play();
            growAndShrink?.Grow();
            sfxPlayer?.Play();
        }
    }


    public void CheckpointSwitched()
    {
        anim.SetBool("isActive", false);
        frontFireFly?.Stop();
        backFireFly?.Stop();
        growAndShrink?.Shrink();
    }
}
