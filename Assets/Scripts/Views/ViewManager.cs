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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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
}
