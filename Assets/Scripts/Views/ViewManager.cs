using System;
using UnityEngine;
using System.Collections.Generic;


public enum ViewID
{
    HUD,
    PauseMenu,
    AbilityUnlockMenu,
    Settings
}



public class ViewManager : MonoBehaviour
{
    public static ViewManager Instance { get; private set; }

    private Stack<UIView> viewStack = new Stack<UIView>();
    
    [SerializeField] private UIView hudView;
    
    [SerializeField] private UIView abilityUnlockView;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        
        hudView.Hide();
        abilityUnlockView.Hide();

        PushView(hudView);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PushView(UIView view)
    {
        if (viewStack.Count > 0)
        {
            viewStack.Peek().Hide();
        }
        viewStack.Push(view);
        view.Show();
    }

    public void PopView()
    {
        if (viewStack.Count == 0) return;

        viewStack.Pop().Hide();
        if (viewStack.Count > 0)
        {
            viewStack.Peek().Show();
        }
        
    }

    public void ClearViews()
    {
        while (viewStack.Count > 0)
        {
            viewStack.Pop().Hide();
        }
    }

    public void PushAbilityUnlockView(){
        ClearViews();
        PushView(abilityUnlockView);
    }

    public void ResetToHUD(){
        ClearViews();
        PushView(hudView);
    }

}
