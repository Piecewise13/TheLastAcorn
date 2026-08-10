using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;


namespace Common
{
    [Serializable]
    public class NamedEvent
    {
        public string eventName;
        public UnityEvent unityEvent;
    }
    public class EventHandler : MonoBehaviour
    {
        // List of NamedEvents exposed in the Inspector
        [SerializeField]
        private List<NamedEvent> events = new List<NamedEvent>();

        // Method to trigger a specific event by name
        public void TriggerEvent(string name)
        {
            NamedEvent namedEvent = events.Find(e => e.eventName == name);
            if (namedEvent != null)
            {
                namedEvent.unityEvent.Invoke();
            }
            else
            {
                Debug.LogWarning($" Event with name " + name + " not found.");
            }
        }

        // Optional: Method to get the number of events
        public int GetEventCount()
        {
            return events.Count;
        }

        // Optional: Method to add an event programmatically
        public void AddEvent(string name, UnityEvent newEvent)
        {
            events.Add(new NamedEvent { eventName = name, unityEvent = newEvent });
        }

        // Optional: Method to clear all events
        public void ClearEvents()
        {
            events.Clear();
        }
    }
}