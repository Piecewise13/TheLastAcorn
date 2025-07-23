using UnityEngine;

public class VineMaster : MonoBehaviour
{

    [SerializeField] private GameObject[] vineSegments;

    private VineSegment[] vineSegmentArray;

    private Rigidbody2D[] vineSegmentRb;




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        vineSegmentArray = new VineSegment[vineSegments.Length];
        vineSegmentRb = new Rigidbody2D[vineSegments.Length];

        for (int i = 0; i < vineSegmentArray.Length; i++)
        {
            vineSegmentArray[i] = vineSegments[i].GetComponent<VineSegment>();
            vineSegmentRb[i] = vineSegmentArray[i].GetComponent<Rigidbody2D>();
        }
    }

    void PlayerAttachedToVine(VineSegment vineSegment)
    {
        // Handle player attachment logic here
        Debug.Log("Player attached to vine segment: " + vineSegment.name);
    }
    
    public void DetachPlayerFromVine(VineSegment vineSegment)
    {
        // Handle player detachment logic here
        Debug.Log("Player detached from vine segment: " + vineSegment.name);
    }
}
