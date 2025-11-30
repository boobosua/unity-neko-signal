using System;
using UnityEngine;

namespace NekoSignal
{
    public static class SignalUtils
    {
        /// <summary>
        /// Subscribe without priority (default priority = 0).
        /// </summary>
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback) where T : ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback);
        }

        /// <summary>
        /// Subscribe with explicit priority (higher invoked earlier).
        /// </summary>
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback, int priority) where T : ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback, priority);
        }

        /// <summary>
        /// Unsubscribe a previously subscribed callback.
        /// </summary>
        public static void Unsubscribe<T>(MonoBehaviour owner, Action<T> callback) where T : ISignal
        {
            SignalBroadcaster.Unsubscribe(owner, callback);
        }

        /// <summary>
        /// Publish a signal with optional filters.
        /// </summary>
        public static void Publish<T>(T signal, params ISignalFilter[] filters) where T : ISignal
        {
            SignalBroadcaster.Publish(signal, filters);
        }

        /// <summary>
        /// Listen and get an action to unsubscribe later.
        /// </summary>
        public static Action Listen<T>(MonoBehaviour owner, Action<T> callback) where T : ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback);
            return () => SignalBroadcaster.Unsubscribe(owner, callback);
        }

        /// <summary>
        /// Listen with priority and get an action to unsubscribe later.
        /// </summary>
        public static Action Listen<T>(MonoBehaviour owner, Action<T> callback, int priority) where T : ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback, priority);
            return () => SignalBroadcaster.Unsubscribe(owner, callback);
        }

        /// <summary>
        /// Unsubscribe all receivers of specific signal type (manual bulk cleanup).
        /// </summary>
        public static void UnsubscribeAllOfType<T>() where T : ISignal
        {
            SignalBroadcaster.UnsubscribeAllOfType<T>();
        }

        /// <summary>
        /// Unsubscribe from all signals (manual bulk cleanup).
        /// </summary>
        public static void UnsubscribeAll()
        {
            SignalBroadcaster.UnsubscribeAll();
        }

        /// <summary>
        /// Query subscriber count.
        /// </summary>
        public static int GetSubscriberCount<T>() where T : ISignal
        {
            return SignalBroadcaster.GetSubscriberCount<T>();
        }
    }
}
