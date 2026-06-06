using System;
using System.Collections;
using UnityEngine;

public class AbilityUnlockUIController : MonoBehaviour
{

    public AcornCollectionBar completionBar;

    public UnlockAbilityView unlockAbilityView;

    private PlayerMove playerMove;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        StartCoroutine(SubscribeWhenReady());

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator SubscribeWhenReady()
    {
        while (PlayerAbilityManager.Instance == null)
            yield return null;

        PlayerAbilityManager.Instance.OnSegmentChanged += HandleSegmentChanged;
        PlayerAbilityManager.Instance.OnAbilityUnlocked += HandleAbilityUnlocked;

        if (completionBar != null)
            completionBar.gameObject.SetActive(false);

        playerMove = GetComponentInParent<PlayerMove>();
    }

    private void HandleSegmentChanged(int segmentAcorns, int segmentRequired)
    {
        if (completionBar != null)
            completionBar.gameObject.SetActive(true);
            completionBar.ShowCompletionBar(segmentAcorns, segmentRequired, onComplete: () =>
            {
                if (segmentRequired > 0 && segmentAcorns >= segmentRequired) {
                    unlockAbilityView.gameObject.SetActive(true);
                    playerMove.DisableMove();
                }
            });
    }

    private void HandleAbilityUnlocked(PlayerAbilityManager.Abilities ability)
    {
        if (unlockAbilityView != null)
            unlockAbilityView.gameObject.SetActive(false);
            
        if (completionBar != null)
            completionBar.SpawnIndicatorBars();

        playerMove.EnableMove();
    }
}
