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

    private PlayerCameraManager playerCameraManager = null;
    
    public override async UniTask Setup()
    {
        abilityNameText.alpha= 0f;
        inputPromptGroup.alpha = 0;
        
        playerCameraManager = FindAnyObjectByType<PlayerCameraManager>();

        await OverlayCameraController.Instance.RequestPlayerOverlay();

    }

    public override async UniTask RunAsync(CancellationToken token)
    {
        
        await initalTextFeedback.PlayFeedbacksAsync(token);
        
        await inputPrompt.PlayFeedbacksAsync(token);
        Debug.Log("[UnlockZoomView] Waiting for inputPrompt]");
        // 2. allow + wait for the zoom input
        await playerCameraManager.WaitForZoomInput(token);
        Debug.Log("[UnlockZoomView] Exiting");

        OverlayCameraController.Instance.ReleasePlayerOverlay();
        // 3. trigger exit feedbacks
        exitFeedback.PlayFeedbacksAsync(token);
        Debug.Log("[UnlockZoomView] Perform Zoom");
        // 4. then start the actual zoom
        await playerCameraManager.PerformUnlockZoom(token);
    }

    private async UniTask ExitView()
    {
        exitFeedback.PlayFeedbacks();
        OverlayCameraController.Instance.ReleasePlayerOverlay();
    }

    private async UniTask WaitForZoomAsync(CancellationToken token)
    {
        playerCameraManager.EnableZoom();
        var tcs = new UniTaskCompletionSource();
        void Handler() => tcs.TrySetResult();
        playerCameraManager.OnZoomStarted += Handler;
        try
        {
            await tcs.Task.AttachExternalCancellation(token);
        }
        finally
        {
            playerCameraManager.OnZoomStarted -= Handler;
        }
    }
}
