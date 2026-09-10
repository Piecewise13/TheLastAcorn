using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using Player;
using TMPro;
using UnityEngine;

public class UnlockGlideView : ViewBase
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
    }

    public override async UniTask RunAsync(CancellationToken token)
    {
        await initalTextFeedback.PlayFeedbacksAsync(token);
        Debug.Log("[GlideView] Step 1");
        
        await inputPrompt.PlayFeedbacksAsync(token);
        Debug.Log("[GlideView] Step 2s");
    }
    

    public override async UniTask Hide(CancellationToken token)
    {
        CameraRig.Current.ResetTrackingTarget();
        exitFeedback.PlayFeedbacks();
    }
}
