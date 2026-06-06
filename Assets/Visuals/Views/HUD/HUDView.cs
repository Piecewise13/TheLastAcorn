using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class HUDView : UIView
{
    
    public AcornCollectionBar completionBar;
    
    PlayerMove playerMove;

    private void Awake()
    {

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        completionBar = GetComponentInChildren<AcornCollectionBar>();

        StartCoroutine(SubscribeWhenReady());
        
        completionBar.gameObject.SetActive(false);
    }
    
    private void HandleSegmentChanged(int segmentAcorns, int segmentRequired)
    {
        if (completionBar != null)
            completionBar.gameObject.SetActive(true);
        
        completionBar.ShowCompletionBar(segmentAcorns, segmentRequired, onComplete: () =>
        {
            completionBar.gameObject.SetActive(false);
            
            if (segmentRequired > 0 && segmentAcorns >= segmentRequired) {
                playerMove.DisableMove();
                ViewManager.Instance.PushAbilityUnlockView(); 
                
                //TODO: Hook up to gameplay manager to disable play while in the upgrade view
            }
        });
    }

    private void HandleAbilityUnlocked(PlayerAbilityManager.Abilities ability)
    {
        ViewManager.Instance.PopView();
            
        if (completionBar != null)
            completionBar.SpawnIndicatorBars();

        playerMove.EnableMove();
    }
    
    IEnumerator SubscribeWhenReady()
    {
        while (PlayerAbilityManager.Instance == null)
            yield return null;
        
        print("Subscribed");

        PlayerAbilityManager.Instance.OnSegmentChanged += HandleSegmentChanged;
        PlayerAbilityManager.Instance.OnAbilityUnlocked += HandleAbilityUnlocked;

        if (completionBar != null)
            completionBar.gameObject.SetActive(false);

        playerMove = FindAnyObjectByType<PlayerMove>();
    }
}
