using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnlockAbilityView : UIView
{

    


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UnlockAbility(int ability)
    {
        PlayerAbilityManager.Instance.UnlockAbility((PlayerAbilityManager.Abilities)ability);
        ViewManager.Instance.ResetToHUD();
    }
}
