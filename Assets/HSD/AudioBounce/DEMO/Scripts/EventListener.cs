using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HSD.AudioBounce.Demo
{
    public class EventListener : MonoBehaviour
    {
        [Tooltip("Name of the event to listen to")]
        public string eventName;

        // Example UnityEvent to show how you can set actions from the Unity Editor
        public UnityEvent OnEventReceived;

        private void Awake()
        {
            // Assuming you have a method to add listeners by string
            EventBroadcaster.AddListener(eventName, HandleEvent);
        }

        private void OnDestroy()
        {
            // Clean up the listener when this object is destroyed
            EventBroadcaster.RemoveListener(eventName, HandleEvent);
        }

        private void HandleEvent()
        {
            // This is just an example action. In a real-world scenario, you'd
            // probably want to do something more meaningful.
            Debug.Log($"Received event named: {eventName}");

            // Call the UnityEvent's invocation list (if any action is set from the Editor or via code)
            OnEventReceived.Invoke();
        }
    }
    
    public static class EventBroadcaster
    {
        // Assuming a simple delegate type for your events
        public delegate void SimpleEvent();

        private static Dictionary<string, SimpleEvent> events = new Dictionary<string, SimpleEvent>();

        public static void AddListener(string eventName, SimpleEvent listener)
        {
            if (!events.ContainsKey(eventName))
            {
                events[eventName] = null;
            }

            events[eventName] += listener;
        }

        public static void RemoveListener(string eventName, SimpleEvent listener)
        {
            if (events.ContainsKey(eventName))
            {
                events[eventName] -= listener;
            }
        }

        public static void Broadcast(string eventName)
        {
            if (events.ContainsKey(eventName) && events[eventName] != null)
            {
                events[eventName].Invoke();
            }
        }
    }
}