using UnityEngine;

public class CaveZoneTrigger : MonoBehaviour
{

    [SerializeField] private GameObject forestVisuals;
    [SerializeField] private GameObject caveVisuals;

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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
