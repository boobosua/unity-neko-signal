using System;
using NekoLib.Logger;
using UnityEngine;

namespace NekoSignal
{
    public static class SignalExtensions
    {
        /// <summary>
        /// Subscribes to a signal of the specified type.
        /// </summary>
        public static void Subscribe<T>(this MonoBehaviour owner, Action<T> callback) where T : ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback);
        }

        /// <summary>
        /// Subscribes to a signal of the specified type with a priority. Higher values are invoked earlier.
        /// </summary>
        public static void Subscribe<T>(this MonoBehaviour owner, Action<T> callback, int priority) where T : ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback, priority);
        }

        /// <summary>
        /// Unsubscribes from a signal.
        /// </summary>
        public static void Unsubscribe<T>(this MonoBehaviour owner, Action<T> callback) where T : ISignal
        {
            SignalBroadcaster.Unsubscribe<T>(owner, callback);
        }

        /// <summary>
        /// Publishes a signal of the specified type.
        /// </summary>
        public static void Publish<T>(this MonoBehaviour owner, T signal) where T : ISignal
        {
            SignalBroadcaster.Publish(signal);
        }

        /// <summary>
        /// Publishes a signal of the specified type with filters.
        /// </summary>
        public static void Publish<T>(this MonoBehaviour owner, T signal, params ISignalFilter[] filters) where T : ISignal
        {
            SignalBroadcaster.Publish(signal, filters);
        }

        /// <summary>
        /// Subscribes dynamically and returns an Action you can call to unsubscribe.
        /// </summary>
        public static Action Listen<T>(this MonoBehaviour owner, Action<T> callback) where T : ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback);
            return () => SignalBroadcaster.Unsubscribe(owner, callback);
        }

        /// <summary>
        /// Subscribes dynamically with priority and returns an Action you can call to unsubscribe.
        /// </summary>
        public static Action Listen<T>(this MonoBehaviour owner, Action<T> callback, int priority) where T : ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback, priority);
            return () => SignalBroadcaster.Unsubscribe(owner, callback);
        }

        /// <summary>
        /// Manually unsubscribes all receivers for a specific signal type.
        /// </summary>
        public static void UnsubscribeAllOfType<T>(this MonoBehaviour owner) where T : ISignal
        {
            var allMonitors = UnityEngine.Object.FindObjectsByType<SignalReceiverMonitor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var monitor in allMonitors)
            {
                monitor.DisposeAllReceiversOfType<T>();
            }
        }

        /// <summary>
        /// Unsubscribes from all signals.
        /// </summary>
        public static void UnsubscribeAll(this MonoBehaviour owner)
        {
            var allMonitors = UnityEngine.Object.FindObjectsByType<SignalReceiverMonitor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var monitor in allMonitors)
            {
                monitor.DisposeAllReceivers();
            }
        }

        /// <summary>
        /// Gets the number of active subscribers for a specific signal type.
        /// </summary>
        public static int GetSubscriberCount<T>(this MonoBehaviour owner) where T : ISignal
        {
            return SignalBroadcaster.GetSubscriberCount<T>();
        }

        /// <summary>
        /// Disposes all signal receivers attached to the MonoBehaviour.
        /// </summary>
        public static void DisposeAllSignalReceivers(this MonoBehaviour owner)
        {
            if (owner == null)
            {
                Log.Warn("Cannot dispose receivers for null MonoBehaviour.");
                return;
            }

            if (owner.TryGetComponent(out SignalReceiverMonitor monitor))
            {
                monitor.DisposeAllReceivers();
            }
        }
    }
}
