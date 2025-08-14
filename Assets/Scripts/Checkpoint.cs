using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{

    [Header("Success Feedback")]
    [SerializeField] private ParticleSystem confetti;
    [SerializeField] private GrowAndShrink growAndShrink;
    [SerializeField] private AudioPlayer sfxPlayer;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CheckpointManager.Instance.SetCheckpoint(this);


            SaveLoadManager.SaveCollectedAcorns();
            confetti?.Play();
            growAndShrink?.Grow();
            sfxPlayer?.Play();
        }
    }


    public void CheckpointSwitched()
    {
        confetti?.Play();
        growAndShrink?.Shrink();
    }
}
