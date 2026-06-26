using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using UnityEngine;

public static class MMFPlayerExtensions
{
public static async UniTask PlayFeedbacksAsync(
    this MMF_Player feedbacks,
    CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();

    feedbacks.PlayFeedbacks();
    
    if (feedbacks is {IsPlaying: true, TotalDuration: > 0})
    {
        try
        {
            await UniTask.WhenAny(
                UniTask.WaitWhile(() => feedbacks.IsPlaying, cancellationToken: cancellationToken),
                feedbacks.Events.OnComplete.OnInvokeAsync(cancellationToken)
            );
        }
        catch (OperationCanceledException)
        {
            if (feedbacks != null && feedbacks.IsPlaying)
            {
                feedbacks.StopFeedbacks();
            }
        }
    }
}
}
