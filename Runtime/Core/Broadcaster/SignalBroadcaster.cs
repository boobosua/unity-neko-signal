using System;
using System.Collections.Generic;
using NekoLib.ColorPalette;
using NekoLib.Extensions;
using NekoLib.Logger;
using UnityEngine;

namespace NekoSignal
{
    [UnityEngine.Scripting.Preserve]
    internal static partial class SignalBroadcaster
    {
        private static readonly Dictionary<Type, ISignalChannel> _signalChannels = new();

        /// <summary>Subscribes to a signal of the specified type with MonoBehaviour owner for auto cleanup.</summary>
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            Subscribe(owner, callback, 0);
        }

        /// <summary>Subscribes to a signal with an explicit priority. Higher values are invoked earlier.</summary>
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback, int priority) where T : struct, ISignal
        {
            if (owner == null)
            {
                Log.Warn("[SignalBroadcaster] Cannot subscribe with null owner.");
                return;
            }

            if (callback == null)
            {
                Log.Warn($"[SignalBroadcaster] Cannot subscribe with null callback for signal type {typeof(T).Name.Colorize(Swatch.VR)}.");
                return;
            }

            // Get or create the signal channel.
            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).AddCallback(callback, owner, priority);
            }
            else
            {
                _signalChannels[type] = new SignalChannel<T>();
                ((SignalChannel<T>)_signalChannels[type]).AddCallback(callback, owner, priority);
            }

            // Handle monitor auto-unsubscription.
            var receiver = new SignalReceiver(() => Unsubscribe(callback), typeof(T));
            var monitor = owner.gameObject.GetOrAdd<SignalReceiverMonitor>();
            monitor.AddReceiver(callback, receiver);
        }

        /// <summary>Unsubscribes from a signal using the callback reference.</summary>
        public static void Unsubscribe<T>(MonoBehaviour owner, Action<T> callback) where T : struct, ISignal
        {
            if (owner == null)
            {
                Log.Warn("[SignalBroadcaster] Cannot unsubscribe with null owner.");
                return;
            }

            if (callback == null)
            {
                Log.Warn($"[SignalBroadcaster] Cannot unsubscribe with null callback for signal type {typeof(T).Name.Colorize(Swatch.VR)}.");
                return;
            }

            if (owner.TryGetComponent(out SignalReceiverMonitor monitor))
            {
                monitor.RemoveReceiver(callback);
            }
            else
            {
                Unsubscribe(callback);
            }
        }

        private static void Unsubscribe<T>(Action<T> callback) where T : struct, ISignal
        {
            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).RemoveCallback(callback);

                if (channel.SubscriberCount == 0)
                {
                    channel.Clear();
                    _signalChannels.Remove(type);
                    Log.Info($"[SignalBroadcaster] All subscribers removed for signal type {type.Name.Colorize(Swatch.VR)}.");
                }
            }
        }

        /// <summary>Emits a signal of the specified type.</summary>
        public static void Emit<T>(T signal) where T : struct, ISignal
        {
            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).Emit(signal);
            }
        }

        /// <summary>Emits a signal only to subscribers whose owner passes all filters.</summary>
        internal static void Emit<T>(T signal, List<ISignalFilter> filters) where T : struct, ISignal
        {
            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).EmitFiltered(signal, filters);
            }
        }

        /// <summary>Emits a signal only to subscribers whose owner passes all filters.</summary>
        public static void Emit<T>(T signal, params ISignalFilter[] filters) where T : struct, ISignal
        {
            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).EmitFiltered(signal, filters);
            }
        }

        /// <summary>Emits a signal with editor debug context (caller file/line + optional emitter owner).</summary>
        internal static void EmitWithDebugContext<T>(T signal, MonoBehaviour emitter, ISignalFilter[] filters) where T : struct, ISignal
        {
#if UNITY_EDITOR
            string file = null; int line = 0;
            try
            {
                var st = new System.Diagnostics.StackTrace(true);
                for (int i = 0; i < st.FrameCount; i++)
                {
                    var f = st.GetFrame(i);
                    var ns = f.GetMethod()?.DeclaringType?.Namespace ?? string.Empty;
                    if (!string.IsNullOrEmpty(f.GetFileName()) && !ns.StartsWith("NekoSignal"))
                    {
                        file = f.GetFileName();
                        line = f.GetFileLineNumber();
                        break;
                    }
                }
            }
            catch { }

            using (SignalLogStore.Emitter(emitter, file, line))
            {
                Emit(signal, filters);
            }
#else
            Emit(signal, filters);
#endif
        }

        /// <summary>Manually unsubscribes all receivers for a specific signal type.</summary>
        public static void UnsubscribeAllOfType<T>() where T : struct, ISignal
        {
            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                channel.Clear();
                _signalChannels.Remove(type);
            }
        }

        /// <summary>Manually unsubscribes all receivers for all signal types.</summary>
        public static void UnsubscribeAll()
        {
            foreach (var channel in _signalChannels.Values)
            {
                channel.Clear();
            }

            _signalChannels.Clear();
        }

        /// <summary>Gets the number of active subscribers for a specific signal type.</summary>
        public static int GetSubscriberCount<T>() where T : struct, ISignal
        {
            var type = typeof(T);
            return _signalChannels.TryGetValue(type, out var channel) ? channel.SubscriberCount : 0;
        }
    }
}
