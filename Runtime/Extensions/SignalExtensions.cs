using System;
using NekoLib.Logger;
using UnityEngine;

namespace NekoSignal
{
    public static class SignalExtensions
    {
        /// <summary>Subscribes to a signal of the specified type.</summary>
        public static void Subscribe<T>(this MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback);
        }

        /// <summary>Subscribes to a signal of the specified type with a priority. Higher values are invoked earlier.</summary>
        public static void Subscribe<T>(this MonoBehaviour owner, Action<T> callback, int priority) where T : struct, ISignal
        {
            SignalBroadcaster.Subscribe(owner, callback, priority);
        }

        /// <summary>Unsubscribes from a signal.</summary>
        public static void Unsubscribe<T>(this MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            SignalBroadcaster.Unsubscribe<T>(owner, callback);
        }

        /// <summary>Emits a signal of the specified type with no filters.</summary>
        public static void Emit<T>(this MonoBehaviour owner, T signal) where T : struct, ISignal
        {
            if (owner == null)
            {
                Log.Warn("[SignalExtensions] Cannot emit signal from null MonoBehaviour.");
                return;
            }
            SignalBroadcaster.EmitWithDebugContext(signal, owner, null);
        }

        /// <summary>Emits a signal of the specified type with filters.</summary>
        public static void Emit<T>(this MonoBehaviour owner, T signal, params ISignalFilter[] filters) where T : struct, ISignal
        {
            if (owner == null)
            {
                Log.Warn("[SignalExtensions] Cannot emit signal from null MonoBehaviour.");
                return;
            }
            SignalBroadcaster.EmitWithDebugContext(signal, owner, filters);
        }

        /// <summary>Subscribes dynamically and returns an IDisposable to unsubscribe.</summary>
        public static IDisposable Listen<T>(this MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            return SignalBroadcaster.SubscribeCore(owner, callback, 0);
        }

        /// <summary>Subscribes dynamically with priority and returns an IDisposable to unsubscribe.</summary>
        public static IDisposable Listen<T>(this MonoBehaviour owner, Action<T> callback, int priority) where T : struct, ISignal
        {
            return SignalBroadcaster.SubscribeCore(owner, callback, priority);
        }

        /// <summary>Manually unsubscribes all receivers for a specific signal type on this owner only.</summary>
        public static void UnsubscribeAllOfType<T>(this MonoBehaviour owner) where T : struct, ISignal
        {
            if (owner.TryGetComponent(out SignalReceiverMonitor monitor))
                monitor.DisposeAllReceiversOfType<T>();
        }

        /// <summary>Unsubscribes from all signals on this owner only.</summary>
        public static void UnsubscribeAll(this MonoBehaviour owner)
        {
            if (owner.TryGetComponent(out SignalReceiverMonitor monitor))
                monitor.DisposeAllReceivers();
        }

        /// <summary>Gets the number of active subscribers for a specific signal type.</summary>
        public static int GetSubscriberCount<T>(this MonoBehaviour owner) where T : struct, ISignal
        {
            return SignalBroadcaster.GetSubscriberCount<T>();
        }

        /// <summary>Starts a fluent filtered emit pipeline for the signal.</summary>
        public static SignalEmitOptions<T> ConfigureFilters<T>(this T signal) where T : struct, ISignal
        {
            return new SignalEmitOptions<T>(signal);
        }
    }
}
