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
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback) where T : ISignal
        {
            Subscribe(owner, callback, 0);
        }

        /// <summary>Subscribes to a signal with an explicit priority. Higher values are invoked earlier.</summary>
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback, int priority) where T : ISignal
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
        public static void Unsubscribe<T>(MonoBehaviour owner, Action<T> callback) where T : ISignal
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

        private static void Unsubscribe<T>(Action<T> callback) where T : ISignal
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

        /// <summary>Publishes a new signal of the specified type.</summary>
        public static void Publish<T>(T signal) where T : ISignal
        {
            if (signal == null)
            {
                Log.Warn($"[SignalBroadcaster] Cannot publish null signal of type {typeof(T).Name.Colorize(Swatch.VR)}.");
                return;
            }

            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).Publish(signal);
            }
        }

        /// <summary>Publishes a signal only to subscribers whose owner passes all filters.</summary>
        public static void Publish<T>(T signal, params ISignalFilter[] filters) where T : ISignal
        {
            if (signal == null)
            {
                Log.Warn($"[SignalBroadcaster] Cannot publish null signal of type {typeof(T).Name.Colorize(Swatch.VR)}.");
                return;
            }

            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).PublishFiltered(signal, filters);
            }
        }

        /// <summary>Manually unsubscribes all receivers for a specific signal type.</summary>
        public static void UnsubscribeAllOfType<T>() where T : ISignal
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
        public static int GetSubscriberCount<T>() where T : ISignal
        {
            var type = typeof(T);
            return _signalChannels.TryGetValue(type, out var channel) ? channel.SubscriberCount : 0;
        }
    }
}
