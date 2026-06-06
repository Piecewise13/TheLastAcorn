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


    }

    // Update is called once per frame
    void Update()
    {
        
    }



    private void HandleAbilityUnlocked(PlayerAbilityManager.Abilities ability)
    {

    }
}
