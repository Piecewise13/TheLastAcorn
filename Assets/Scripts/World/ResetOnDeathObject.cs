using UnityEngine;

public abstract class ResetOnDeathObject : MonoBehaviour
{

    protected virtual void Start()
    {
        CheckpointManager.Instance.OnPlayerRespawn += ResetObject;
    }

    abstract public void ResetObject();
    

}
