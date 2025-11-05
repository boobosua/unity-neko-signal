using System;
using System.Runtime.CompilerServices;
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
        public static void Publish<T>(this MonoBehaviour owner, T signal, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0) where T : ISignal
        {
            if (owner == null)
            {
                SignalBroadcaster.Publish(signal);
                return;
            }
#if UNITY_EDITOR
            using (SignalLogStore.Publisher(owner, file, line))
            {
                SignalBroadcaster.Publish(signal);
            }
#else
            SignalBroadcaster.Publish(signal);
#endif
        }

        /// <summary>
        /// Publishes a signal of the specified type with filters.
        /// </summary>
        public static void Publish<T>(this MonoBehaviour owner, T signal, params ISignalFilter[] filters) where T : ISignal
        {
            if (owner == null)
            {
                SignalBroadcaster.Publish(signal, filters);
                return;
            }
#if UNITY_EDITOR
            // Capture caller info automatically via attributes on the overload without filters if needed
            string file = null; int line = 0;
            try
            {
                var st = new System.Diagnostics.StackTrace(true);
                // Find first frame outside library to estimate caller
                for (int i = 0; i < st.FrameCount; i++)
                {
                    var f = st.GetFrame(i);
                    var m = f.GetMethod();
                    var dt = m?.DeclaringType;
                    var ns = dt?.Namespace ?? string.Empty;
                    if (!string.IsNullOrEmpty(f.GetFileName()) && !ns.StartsWith("NekoSignal"))
                    {
                        file = f.GetFileName();
                        line = f.GetFileLineNumber();
                        break;
                    }
                }
            }
            catch { }

            using (SignalLogStore.Publisher(owner, file, line))
            {
                SignalBroadcaster.Publish(signal, filters);
            }
#else
            SignalBroadcaster.Publish(signal, filters);
#endif
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
