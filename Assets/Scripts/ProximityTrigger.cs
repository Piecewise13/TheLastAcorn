using UnityEngine;


[RequireComponent(typeof(CircleCollider2D))]
public class ProximityTrigger : MonoBehaviour
{


    private IProximityAlert proximityAlert;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        proximityAlert = GetComponentInParent<IProximityAlert>();
    }


    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && proximityAlert != null)
        {
            proximityAlert.PlayerInProximity(collision.gameObject);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && proximityAlert != null)
        {
            proximityAlert.PlayerOutOfProximity(other.gameObject);
        }
    }
}


public interface IProximityAlert
{
    public void PlayerInProximity(GameObject player);
    public void PlayerOutOfProximity(GameObject player);
}
