using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TutorialMaster : MonoBehaviour
{
    public static TutorialMaster Instance { get; private set; }
    
    //test

    [Serializable] public enum TutorialEvents
    {
        FirstMove,
        BasicClimb,
        BasicJump,
        RunToAcorn,
        AcornMessage
    }

    [Serializable]
    private struct TutorialStep
    {
        public GameObject tutorialObjects;
        bool completed;
    }

    [SerializeField] private List<TutorialStep> tutorialSteps;
    //currentStep is the step that is "active" meaning the player hasn't completed it yet but is curretly working on it. So when the player triggers the event for the current step, it will mark that step as completed and move on to the next one.
    private int currentStep = 0;

    public PlayableDirector playableDirector;

    private PlayerMove playerMove;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if(DebugSettings.Instance.BypassTutorial)
        {
            Debug.Log("[TutorialMaster] Bypassing tutorial due to debug settings.");
            return;
        }

        playerMove = FindAnyObjectByType<PlayerMove>();
        
        playerMove.DisableMove();
        
        HideAllSteps();
    }

    public void PauseTutorial()
    {
        StartNextStep();

        
        if (playableDirector != null)
        {
            playableDirector.playableGraph.GetRootPlayable(0).SetSpeed(0);
        }

    }

    public void ResumeTutorial()
    {
        currentStep++;

        if (playableDirector != null)
        {
            playableDirector.playableGraph.GetRootPlayable(0).SetSpeed(1);
        }
    }

    public void StepCompleted(TutorialEvents tutorialEvent)
    {
        if (currentStep > (int)tutorialEvent)
        {
            return;
        }

        HideStepObjects();
        
        currentStep = (int)tutorialEvent;
        ResumeTutorial();
    }
    
    private void StartNextStep()
    {
        if (currentStep == 0)
        {
                playerMove.EnableMove();
        }
        ShowStepObjects();
    }

    private void ShowStepObjects()
    {
        if (currentStep < tutorialSteps.Count)
        {
            tutorialSteps[currentStep].tutorialObjects.SetActive(true);
        }
    }

    private void HideStepObjects()
    {
        if (currentStep < tutorialSteps.Count)
        {
            tutorialSteps[currentStep].tutorialObjects.SetActive(false);
        }
    }

    private void HideAllSteps()
    {
        foreach (TutorialStep step in tutorialSteps)
        {
            step.tutorialObjects.SetActive(false);
        }
    }


}
