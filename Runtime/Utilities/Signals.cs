using System;
using UnityEngine;

namespace NekoSignal
{
    public static class Signals
    {
        /// <summary>Subscribes without priority (default priority = 0).</summary>
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback);
        }

        /// <summary>Subscribes with explicit priority. Higher values are invoked earlier.</summary>
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback, int priority) where T : struct, ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback, priority);
        }

        /// <summary>Unsubscribes a previously subscribed callback.</summary>
        public static void Unsubscribe<T>(MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            SignalBroadcaster.Unsubscribe(owner, callback);
        }

        /// <summary>Emits a signal with no filters.</summary>
        public static void Emit<T>(T signal) where T : struct, ISignal
        {
            SignalBroadcaster.EmitWithDebugContext(signal, null, null);
        }

        /// <summary>Emits a signal with filters.</summary>
        public static void Emit<T>(T signal, params ISignalFilter[] filters) where T : struct, ISignal
        {
            SignalBroadcaster.EmitWithDebugContext(signal, null, filters);
        }

        /// <summary>Subscribes and returns an IDisposable to unsubscribe later.</summary>
        public static IDisposable Listen<T>(MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            return SignalBroadcaster.SubscribeCore(owner, callback, 0);
        }

        /// <summary>Subscribes with priority and returns an IDisposable to unsubscribe later.</summary>
        public static IDisposable Listen<T>(MonoBehaviour owner, Action<T> callback, int priority) where T : struct, ISignal
        {
            return SignalBroadcaster.SubscribeCore(owner, callback, priority);
        }

        /// <summary>Unsubscribes all receivers of a specific signal type.</summary>
        public static void UnsubscribeAllOfType<T>() where T : struct, ISignal
        {
            SignalBroadcaster.UnsubscribeAllOfType<T>();
        }

        /// <summary>Unsubscribes from all signals.</summary>
        public static void UnsubscribeAll()
        {
            SignalBroadcaster.UnsubscribeAll();
        }

        /// <summary>Gets the number of active subscribers for a specific signal type.</summary>
        public static int GetSubscriberCount<T>() where T : struct, ISignal
        {
            return SignalBroadcaster.GetSubscriberCount<T>();
        }
    }
}
