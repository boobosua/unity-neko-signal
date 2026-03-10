using System;
using NekoLib.Logger;
using UnityEngine;

namespace NekoSignal
{
    public static class SignalExtensions
    {
        /// <summary>Emits a signal from this MonoBehaviour with no filters.</summary>
        public static void Emit<T>(this MonoBehaviour owner, T signal) where T : struct, ISignal
        {
            if (owner == null)
            {
                Log.Warn("[SignalExtensions] Cannot emit signal from null MonoBehaviour.");
                return;
            }
            SignalBroadcaster.EmitWithDebugContext(signal, owner, null);
        }

        /// <summary>Emits a signal from this MonoBehaviour applying the given filters.</summary>
        public static void Emit<T>(this MonoBehaviour owner, T signal, params ISignalFilter[] filters) where T : struct, ISignal
        {
            if (owner == null)
            {
                Log.Warn("[SignalExtensions] Cannot emit signal from null MonoBehaviour.");
                return;
            }
            SignalBroadcaster.EmitWithDebugContext(signal, owner, filters);
        }

        /// <summary>Subscribes manually and returns a <see cref="SignalReceiver"/> — call Dispose() to unsubscribe. Do not pass [OnSignal] methods here.</summary>
        public static SignalReceiver Listen<T>(this MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            return SignalBroadcaster.Subscribe(owner, callback, 0);
        }

        /// <summary>Subscribes manually with priority and returns a <see cref="SignalReceiver"/> — call Dispose() to unsubscribe. Do not pass [OnSignal] methods here.</summary>
        public static SignalReceiver Listen<T>(this MonoBehaviour owner, Action<T> callback, int priority) where T : struct, ISignal
        {
            return SignalBroadcaster.Subscribe(owner, callback, priority);
        }

        /// <summary>Gets the total number of active subscribers for signal type T.</summary>
        public static int GetSubscriberCount<T>(this MonoBehaviour owner) where T : struct, ISignal
        {
            return SignalBroadcaster.GetSubscriberCount<T>();
        }

        /// <summary>Starts a fluent filtered emit pipeline for this signal value.</summary>
        public static SignalEmitOptions<T> ConfigureFilters<T>(this T signal) where T : struct, ISignal
        {
            return new SignalEmitOptions<T>(signal);
        }
    }
}
