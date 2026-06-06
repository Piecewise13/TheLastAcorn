using UnityEngine;

public abstract class UIView : MonoBehaviour
{

    [SerializeField] private ViewID viewID;

    [SerializeField] private int layer;

    public int Layer => layer;

    public virtual void Show()
    {
        gameObject.SetActive(true);
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
    }
}
