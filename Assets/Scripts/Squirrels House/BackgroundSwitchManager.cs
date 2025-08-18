using UnityEngine;

public class BackgroundSwitchManager : MonoBehaviour
{

    public static BackgroundSwitchManager Instance { get; private set; }

    [Header("Backgrounds")]
    [SerializeField] private GameObject springBackground;
    [SerializeField] private GameObject summerBackground;
    [SerializeField] private GameObject fallBackground;


    [Header("Other Settings")]
    [SerializeField] private GameObject treeCover;



    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        springBackground.SetActive(false);
        summerBackground.SetActive(false);
        fallBackground.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {

    }


    public void SetBackground(BackgroundType backgroundType)
    {
        RemoveBackgrounds();

        switch (backgroundType)
        {
            case BackgroundType.Spring:
                springBackground.SetActive(true);
                break;
            case BackgroundType.Summer:
                summerBackground.SetActive(true);
                break;
            case BackgroundType.Fall:
                fallBackground.SetActive(true);
                break;
        }

        treeCover.SetActive(true);
    }

    public void RemoveBackgrounds()
    {
        springBackground.SetActive(false);
        summerBackground.SetActive(false);
        fallBackground.SetActive(false);
        treeCover.SetActive(false);
    }

    public enum BackgroundType
    {
        Spring,
        Summer,
        Fall
    }

}
