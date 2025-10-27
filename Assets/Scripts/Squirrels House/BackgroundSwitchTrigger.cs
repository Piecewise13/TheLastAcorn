using UnityEngine;

public class BackgroundSwitchTrigger : MonoBehaviour
{

    [SerializeField] private BackgroundSwitchManager.BackgroundType backgroundType;

    bool backgroundActive = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter2D(Collider2D other)
    {
        BackgroundSwitchManager.Instance.SetBackground(backgroundType);
        backgroundActive = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
            BackgroundSwitchManager.Instance.RemoveBackgrounds();
            backgroundActive = false;
    }
}
