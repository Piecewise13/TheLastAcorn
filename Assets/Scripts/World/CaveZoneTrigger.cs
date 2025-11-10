using UnityEngine;

public class CaveZoneTrigger : MonoBehaviour
{

    [SerializeField] private bool startInCave = false;

    [SerializeField] private GameObject forestVisuals;
    [SerializeField] private GameObject caveVisuals;

    [SerializeField] private GameObject caveOutsideBlackout;

    void Start()
    {
        forestVisuals.SetActive(!startInCave);
        caveVisuals.SetActive(startInCave);

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            forestVisuals.SetActive(false);
            caveVisuals.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            forestVisuals.SetActive(true);
            caveVisuals.SetActive(false);
        }
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
