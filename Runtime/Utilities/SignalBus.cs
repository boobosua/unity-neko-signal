using System;
using UnityEngine;

namespace NekoSignal
{
    public static class SignalBus
    {
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

        /// <summary>Subscribes manually and returns a <see cref="SignalReceiver"/> — call Dispose() to unsubscribe. Do not pass [OnSignal] methods here.</summary>
        public static SignalReceiver Listen<T>(MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            return SignalBroadcaster.Subscribe(owner, callback, 0);
        }

        /// <summary>Subscribes manually with priority and returns a <see cref="SignalReceiver"/> — call Dispose() to unsubscribe. Do not pass [OnSignal] methods here.</summary>
        public static SignalReceiver Listen<T>(MonoBehaviour owner, Action<T> callback, int priority) where T : struct, ISignal
        {
            return SignalBroadcaster.Subscribe(owner, callback, priority);
        }

        /// <summary>Gets the number of active subscribers for a specific signal type.</summary>
        public static int GetSubscriberCount<T>() where T : struct, ISignal
        {
            return SignalBroadcaster.GetSubscriberCount<T>();
        }
    }
}
