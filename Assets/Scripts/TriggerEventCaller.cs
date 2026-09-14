using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[AddComponentMenu("The Last Acorn/Triggers/Trigger Event Caller")]
public class TriggerEventCaller : MonoBehaviour
{
    private enum TriggerFilterMode
    {
        Any,
        Tag,
        Layer
    }

    private enum FilterTarget
    {
        TriggeringCollider,
        RootObject
    }

    [Serializable]
    public class GameObjectEvent : UnityEvent<GameObject> { }

    [Header("Filter")]
    [SerializeField] private TriggerFilterMode filterMode = TriggerFilterMode.Tag;
    [SerializeField] private FilterTarget filterTarget = FilterTarget.TriggeringCollider;
    [SerializeField] private string requiredTag = "Player";
    [SerializeField] private LayerMask requiredLayers;
    [SerializeField] private bool triggerOnlyOnce;

    [Header("Trigger Enter")]
    [SerializeField] private UnityEvent onTriggerEnter = new UnityEvent();
    [SerializeField] private GameObjectEvent onTriggerEnterWithObject = new GameObjectEvent();

    [Header("Trigger Stay")]
    [SerializeField] private UnityEvent onTriggerStay = new UnityEvent();
    [SerializeField] private GameObjectEvent onTriggerStayWithObject = new GameObjectEvent();

    [Header("Trigger Exit")]
    [SerializeField] private UnityEvent onTriggerExit = new UnityEvent();
    [SerializeField] private GameObjectEvent onTriggerExitWithObject = new GameObjectEvent();

    private bool hasTriggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        InvokeTriggerEvents(other.gameObject, onTriggerEnter, onTriggerEnterWithObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        InvokeTriggerEvents(other.gameObject, onTriggerStay, onTriggerStayWithObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        InvokeTriggerEvents(other.gameObject, onTriggerExit, onTriggerExitWithObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        InvokeTriggerEvents(other.gameObject, onTriggerEnter, onTriggerEnterWithObject);
    }

    private void OnTriggerStay(Collider other)
    {
        InvokeTriggerEvents(other.gameObject, onTriggerStay, onTriggerStayWithObject);
    }

    private void OnTriggerExit(Collider other)
    {
        InvokeTriggerEvents(other.gameObject, onTriggerExit, onTriggerExitWithObject);
    }

    private void InvokeTriggerEvents(GameObject triggeringObject, UnityEvent triggerEvent, GameObjectEvent triggerEventWithObject)
    {
        if ((triggerOnlyOnce && hasTriggered) || !MatchesFilter(triggeringObject))
        {
            return;
        }

        hasTriggered = true;
        triggerEvent?.Invoke();
        triggerEventWithObject?.Invoke(triggeringObject);
    }

    private bool MatchesFilter(GameObject triggeringObject)
    {
        GameObject target = filterTarget == FilterTarget.RootObject
            ? triggeringObject.transform.root.gameObject
            : triggeringObject;

        return filterMode switch
        {
            TriggerFilterMode.Any => true,
            TriggerFilterMode.Tag => target.CompareTag(requiredTag),
            TriggerFilterMode.Layer => (requiredLayers.value & (1 << target.layer)) != 0,
            _ => false
        };
    }

    private void Reset()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }

        Collider collider3D = GetComponent<Collider>();
        if (collider3D != null)
        {
            collider3D.isTrigger = true;
        }
    }
}
