using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(GrowAndShrink))]
public sealed class ButtonHover : MonoBehaviour,
                                      IPointerEnterHandler,
                                      IPointerExitHandler
{
    [SerializeField] private AudioPlayer hoverSFX;

    GrowAndShrink gs;
    Button btn;

    void Awake()
    {
        gs  = GetComponent<GrowAndShrink>();
        btn = GetComponent<Button>();
        
        Navigation nav = btn.navigation;
        nav.mode       = Navigation.Mode.None;
        btn.navigation = nav;
    }

    public void OnPointerEnter(PointerEventData _)
    {
        if (gs) gs.Grow();
        if (hoverSFX) hoverSFX.Play();
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (gs) gs.Shrink();
    }
    

    void OnDisable()
    {
        if (gs) gs.ResetScale(); 
        
        if (EventSystem.current &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            StartCoroutine(ClearSelectionNextFrame());
        }
    }

    IEnumerator ClearSelectionNextFrame()
    {
        yield return null;
        if (EventSystem.current &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
