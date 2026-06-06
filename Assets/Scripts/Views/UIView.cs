using UnityEngine;

public abstract class UIView : MonoBehaviour
{

    [SerializeField] protected ViewID viewID;

    [SerializeField] protected int layer;

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
