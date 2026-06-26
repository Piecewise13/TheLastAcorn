using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class ViewBase : MonoBehaviour
{
    [SerializeField] private ViewID viewID;
    [SerializeField] private int layer;

    public ViewID ViewID => viewID;
    public int Layer => layer;

    public virtual async UniTask Setup() { }

    public virtual async UniTask Show(CancellationToken token)
    {
        gameObject.SetActive(true);
        await RunAsync(token);
    }

    public virtual UniTask RunAsync(CancellationToken token = default)
    {
        return UniTask.CompletedTask;
    }

    public virtual UniTask Hide(CancellationToken token = default)
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
        return UniTask.CompletedTask;
    }
}