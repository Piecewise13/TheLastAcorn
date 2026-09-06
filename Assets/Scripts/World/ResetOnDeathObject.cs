using UnityEngine;

public abstract class ResetOnDeathObject : MonoBehaviour
{
    private CheckpointManager checkpointManager;

    protected virtual void Start()
    {
        checkpointManager = CheckpointManager.For(gameObject.scene);
        if (checkpointManager != null)
            checkpointManager.OnPlayerRespawn += ResetObject;
    }

    protected virtual void OnDestroy()
    {
        if (checkpointManager != null)
            checkpointManager.OnPlayerRespawn -= ResetObject;
    }

    abstract public void ResetObject();
}
