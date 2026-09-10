using System;
using Player;
using System.Collections;
using UnityEngine;

public class AbilityUnlockUIController : MonoBehaviour
{

    public AcornCollectionBar completionBar;

    public UnlockZoomView unlockZoomView;

    private PlayerMoveManager playerMoveManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {


    }

    // Update is called once per frame
    void Update()
    {
        
    }



    private void HandleAbilityUnlocked(PlayerAbilityManager.Abilities ability)
    {

    }
}
