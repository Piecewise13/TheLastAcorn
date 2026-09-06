using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;


public enum ViewID
{
    HUD,
    PauseMenu,
    AbilityUnlockMenu,
    Settings
}



// Persistent across scene loads: a single ViewManager (and the views on its stack) survives scene
// swaps, so a view pushed in one scene can outlive the load into the next. Any ViewManager placed in
// a later scene finds this one already alive and destroys itself (see PersistentSingleton).
public class ViewManager : PersistentSingleton<ViewManager>
{
    private CancellationTokenSource viewTransitionCts = new CancellationTokenSource();

    private Stack<ViewBase> viewStack = new Stack<ViewBase>();
    
    /// <summary>
    /// Pushes a view to the stack and immediately shows the view.
    /// </summary>
    /// <param name="viewBase">The view prefab</param>
    /// <returns>The spawned view</returns>
    public async UniTask<ViewBase> PushView(ViewBase viewBase)
    {
        var newView = Instantiate(viewBase, transform);
        await newView.Setup();
        
        viewStack.Push(newView);
        await newView.Show(CancellationToken.None);

        return newView;
    }

    public void PopView()
    {
        if (viewStack.Count == 0) return;

        viewStack.Pop().Hide(viewTransitionCts.Token);
        if (viewStack.Count > 0)
        {
            viewStack.Peek().Show(CancellationToken.None);
        }
        
    }

    public async UniTask ClearViews()
    {
        while (viewStack.Count > 0)
        {
            var view = viewStack.Pop();
            await view.Hide();
        }
    }
    
    public void ClearViewsInstant()
    {
        viewTransitionCts.Cancel();
        viewTransitionCts.Dispose();
        viewTransitionCts = new CancellationTokenSource();
        while (viewStack.Count > 0)
        {
            var view = viewStack.Pop();
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }
    }
    /* HUD Functions

    public void HardResetToHUD()
    {
        viewTransitionCts.Cancel();
        viewTransitionCts.Dispose();
        viewTransitionCts = new CancellationTokenSource();

        while (viewStack.Count > 0)
        {
            var view = viewStack.Pop();
            if (view != null)
                Destroy(view.gameObject);
        }

        PushView(hudViewBase);
    }
    */
}
