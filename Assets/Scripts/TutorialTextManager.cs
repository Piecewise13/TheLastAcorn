using UnityEngine;
using UnityEngine.Rendering;


public class TutorialTextManager : MonoBehaviour
{

    private PlayerMove playerMove;

    public GameObject climbText;
    public GameObject climbMovementText;
    public GameObject climbMeterText;

    [SerializeField] private float climbMeterTextTime;
    private float climbMeterTextTimer = 0f;


    [SerializeField] private float climbMeterTextTimerShowTime = 4f;

    private int currentStep = 0;

    private void Start()
    {
        playerMove = FindFirstObjectByType<PlayerMove>();
        climbText.SetActive(true);
        climbMovementText.SetActive(false);
        climbMeterText.SetActive(false);
    }

    public void Update()
    {
        if(currentStep == 0)
        {
            if (playerMove.GetPlayerState() == PlayerState.Climb)
            {
                climbText.SetActive(false);
                climbMovementText.SetActive(true);
                currentStep++;
            }
        } else if(currentStep == 1){
            if(climbMeterTextTimer >= climbMeterTextTime)
            {
                climbMovementText.SetActive(false);
                climbMeterText.SetActive(true);
                currentStep++;
                climbMeterTextTimer = 0f;
            } else
            {
                climbMeterTextTimer += Time.deltaTime;

            }
        } else if(currentStep == 2)
        {
            if (climbMeterTextTimer >= climbMeterTextTimerShowTime)
            {
                climbMeterText.SetActive(false);
                currentStep++;
            } else 
            {
                climbMeterTextTimer += Time.deltaTime;
            }
        }
    }

}
