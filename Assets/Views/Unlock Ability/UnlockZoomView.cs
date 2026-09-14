using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


//TODO: Create a parent UnlockView class that contains a more standard approach to this
public class UnlockZoomView : ViewBase
{
    [SerializeField] private TextMeshProUGUI abilityNameText;
    [SerializeField] private CanvasGroup inputPromptGroup = null!;
    
    [Header("Feedbacks")]
    [SerializeField] private MMF_Player initalTextFeedback = null!;
    [SerializeField] private MMF_Player inputPrompt = null!;
    [SerializeField] private MMF_Player exitFeedback = null!;

    public override async UniTask Setup()
    {
        abilityNameText.alpha= 0f;
        inputPromptGroup.alpha = 0;

        await OverlayCameraController.Current.RequestPlayerOverlay();

    }

    public override async UniTask RunAsync(CancellationToken token)
    {

        await initalTextFeedback.PlayFeedbacksAsync(token);

        await inputPrompt.PlayFeedbacksAsync(token);

        OverlayCameraController.Current.ReleasePlayerOverlay();
        // trigger exit feedbacks. The camera zoom is now a scripted moment driven by a
        // scene trigger, so the view no longer waits for zoom input or performs the zoom.
        exitFeedback.PlayFeedbacksAsync(token);
    }

    private async UniTask ExitView()
    {
        exitFeedback.PlayFeedbacks();
        OverlayCameraController.Current.ReleasePlayerOverlay();
    }
}
