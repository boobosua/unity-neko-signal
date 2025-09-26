using System;
using System.Collections.Generic;
using UnityEngine;
using NekoLib.Extensions;
using NekoLib.Core;

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace NekoSignal
{
    public static class SignalBroadcaster
    {
        private static readonly Dictionary<Type, ISignalChannel> _signalChannels = new();

        /// <summary>
        /// Subscribes to a signal of the specified type with MonoBehaviour owner for auto cleanup.
        /// </summary>
        public static void Subscribe<T>(MonoBehaviour owner, Action<T> callback) where T : ISignal
        {
            if (owner == null)
            {
                Debug.LogWarning("[SignalBroadcaster] Cannot subscribe with null owner.");
                return;
            }

            if (callback == null)
            {
                Debug.LogWarning($"[SignalBroadcaster] Cannot subscribe with null callback for signal type {typeof(T).Name.Colorize(Swatch.VR)}.");
                return;
            }

            // Get or create the signal channel.
            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).AddCallback(callback, owner);
            }
            else
            {
                _signalChannels[type] = new SignalChannel<T>();
                ((SignalChannel<T>)_signalChannels[type]).AddCallback(callback, owner);
            }

            // Handle monitor auto-unsubscription.
            var receiver = new SignalReceiver(() => Unsubscribe(callback), typeof(T));
            var monitor = owner.gameObject.GetOrAdd<SignalReceiverMonitor>();
            monitor.AddReceiver(callback, receiver);
        }

        /// <summary>
        /// Unsubscribes from a signal using the callback reference.
        /// </summary>
        public static void Unsubscribe<T>(MonoBehaviour owner, Action<T> callback) where T : ISignal
        {
            if (owner == null)
            {
                Debug.LogWarning("[SignalBroadcaster] Cannot unsubscribe with null owner.");
                return;
            }

            if (callback == null)
            {
                Debug.LogWarning($"[SignalBroadcaster] Cannot unsubscribe with null callback for signal type {typeof(T).Name.Colorize(Swatch.VR)}.");
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
                    Debug.Log($"[SignalBroadcaster] All subscribers removed for signal type {type.Name.Colorize(Swatch.VR)}.");
                }
            }
        }

        /// <summary>
        /// Publishes a new signal of the specified type.
        /// </summary>
        public static void Publish<T>(T signal) where T : ISignal
        {
            if (signal == null)
            {
                Debug.LogWarning($"[SignalBroadcaster] Cannot publish null signal of type {typeof(T).Name.Colorize(Swatch.VR)}.");
                return;
            }

            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).Publish(signal);
            }
        }

        /// <summary>
        /// Publish a signal only to subscribers whose owner passes all filters.
        /// </summary>
        public static void Publish<T>(T signal, params ISignalFilter[] filters) where T : ISignal
        {
            if (signal == null)
            {
                Debug.LogWarning($"[SignalBroadcaster] Cannot publish null signal of type {typeof(T).Name.Colorize(Swatch.VR)}.");
                return;
            }

            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                ((SignalChannel<T>)channel).PublishFiltered(signal, filters);
            }
        }

        /// <summary>
        /// Manually unsubscribes all receivers for a specific signal type.
        /// </summary>
        public static void UnsubscribeAllOfType<T>() where T : ISignal
        {
            var type = typeof(T);
            if (_signalChannels.TryGetValue(type, out var channel))
            {
                channel.Clear();
                _signalChannels.Remove(type);
            }
        }

        /// <summary>
        /// Manually unsubscribes all receivers for all signal types.
        /// </summary>
        public static void UnsubscribeAll()
        {
            foreach (var channel in _signalChannels.Values)
            {
                channel.Clear();
            }

            _signalChannels.Clear();
        }

        /// <summary>
        /// Gets the number of active subscribers for a specific signal type.
        /// </summary>
        public static int GetSubscriberCount<T>() where T : ISignal
        {
            var type = typeof(T);
            return _signalChannels.TryGetValue(type, out var channel) ? channel.SubscriberCount : 0;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitializeEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Clear all signals when exiting play mode
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Debug.Log("[SignalBroadcaster] Clearing all signal channels on play mode exit.");
                UnsubscribeAll();
            }
        }

        /// <summary>
        /// Gets debug information about all active signal channels.
        /// </summary>
        public static string GetDebugInfo()
        {
            if (_signalChannels.Count == 0)
                return "No active signal channels.";

            var info = $"Active signal channels ({_signalChannels.Count}):\n";
            foreach (var kvp in _signalChannels)
            {
                info += $"  • {kvp.Key.Name}: {kvp.Value.SubscriberCount} subscribers\n";
            }
            return info.TrimEnd('\n');
        }

        /// <summary>
        /// Gets the total number of active signal channels.
        /// </summary>
        public static int GetActiveChannelCount()
        {
            return _signalChannels.Count;
        }

        /// <summary>
        /// Gets the total number of subscribers across all channels.
        /// </summary>
        public static int GetTotalSubscriberCount()
        {
            return _signalChannels.Values.Sum(channel => channel.SubscriberCount);
        }

        /// <summary>
        /// Gets detailed information about all active signal channels for editor tools.
        /// </summary>
        public static IEnumerable<SignalChannelInfo> GetAllChannelInfo()
        {
            return _signalChannels.Select(kvp => new SignalChannelInfo
            {
                SignalType = kvp.Key,
                SubscriberCount = kvp.Value.SubscriberCount,
                Channel = kvp.Value
            });
        }

        /// <summary>
        /// Gets detailed subscriber information for a specific signal type.
        /// </summary>
        public static IEnumerable<SignalSubscriberInfo> GetSubscriberInfo<T>() where T : ISignal
        {
            var type = typeof(T);
            if (!_signalChannels.TryGetValue(type, out var channel) || channel.SubscriberCount == 0)
                return Enumerable.Empty<SignalSubscriberInfo>();

            var signalChannel = (SignalChannel<T>)channel;
            return signalChannel.GetSubscriberInfo();
        }

        /// <summary>
        /// Gets detailed subscriber information for a signal type by Type object.
        /// </summary>
        public static IEnumerable<SignalSubscriberInfo> GetSubscriberInfoByType(Type signalType)
        {
            if (!_signalChannels.TryGetValue(signalType, out var channel) || channel.SubscriberCount == 0)
                return Enumerable.Empty<SignalSubscriberInfo>();

            return channel.GetSubscriberInfo();
        }

        /// <summary>
        /// Force cleanup of all stale references across all signal channels.
        /// </summary>
        public static void CleanupStaleReferences()
        {
            foreach (var kvp in _signalChannels.ToList()) // ToList to avoid modification during enumeration
            {
                var channel = kvp.Value;
                var channelType = kvp.Key;

                // Trigger cleanup by calling GetSubscriberInfo which includes cleanup
                channel.GetSubscriberInfo().ToList(); // Force enumeration to trigger cleanup

                // Remove channel if it's now empty
                if (channel.SubscriberCount == 0)
                {
                    channel.Clear();
                    _signalChannels.Remove(channelType);
                }
            }
        }
#endif
    }

    internal interface ISignalChannel
    {
        int SubscriberCount { get; }
        void Clear();
#if UNITY_EDITOR
        IEnumerable<SignalSubscriberInfo> GetSubscriberInfo();
#endif
    }

    internal sealed class SignalChannel<T> : ISignalChannel where T : ISignal
    {
        private Action<T> _subscribers;
        private readonly Dictionary<Delegate, MonoBehaviour> _subscriberOwners = new();

        public void AddCallback(Action<T> callback)
        {
            _subscribers += callback;
        }

        public void AddCallback(Action<T> callback, MonoBehaviour owner)
        {
            _subscribers += callback;
            if (owner != null)
            {
                _subscriberOwners[callback] = owner;
            }
        }

        public void RemoveCallback(Action<T> callback)
        {
            _subscribers -= callback;
            _subscriberOwners.Remove(callback);
        }

        public void Publish(T signal)
        {
            if (_subscribers == null) return;

            try
            {
                _subscribers.Invoke(signal);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalChannel] Exception in signal subscriber for {typeof(T).Name.Colorize(Swatch.VR)}: {ex}");
            }
        }

        public void PublishFiltered(T signal, ISignalFilter[] filters)
        {
            if (_subscribers == null) return;

            // If you store subscribers as a multicast delegate:
            var invocationList = _subscribers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                if (invocationList[i] is Action<T> cb &&
                    _subscriberOwners.TryGetValue(cb, out var owner) &&
                    owner != null)
                {
                    bool pass = true;
                    if (filters != null)
                    {
                        for (int f = 0; f < filters.Length; f++)
                        {
                            if (!filters[f].Evaluate(owner))
                            {
                                pass = false;
                                break;
                            }
                        }
                    }

                    if (pass)
                    {
                        try
                        {
                            cb?.Invoke(signal);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[SignalChannel] Exception in filtered subscriber for {typeof(T).Name}: {ex}");
                        }
                    }
                }
            }
        }

        public int SubscriberCount
        {
            get
            {
                return _subscribers?.GetInvocationList().Length ?? 0;
            }
        }

        public void Clear()
        {
            _subscribers = null;
            _subscriberOwners.Clear();
        }

#if UNITY_EDITOR
        public IEnumerable<SignalSubscriberInfo> GetSubscriberInfo()
        {
            if (_subscribers == null)
                return Enumerable.Empty<SignalSubscriberInfo>();

            var invocationList = _subscribers.GetInvocationList();

            // Clean up stale owner references before creating subscriber info
            CleanupStaleOwnerReferences(invocationList);

            return invocationList.Select(handler => CreateSubscriberInfo(handler, typeof(T), _subscriberOwners.GetValueOrDefault(handler)));
        }

        private void CleanupStaleOwnerReferences(Delegate[] currentHandlers)
        {
            // Create a set of current handlers for fast lookup
            var currentHandlerSet = new HashSet<Delegate>(currentHandlers);

            // Find and remove stale entries (handlers that are no longer in the invocation list)
            var staleKeys = _subscriberOwners.Keys.Where(key => !currentHandlerSet.Contains(key)).ToList();

            foreach (var staleKey in staleKeys)
            {
                _subscriberOwners.Remove(staleKey);
            }

            // Also remove entries where the owner MonoBehaviour is null/destroyed
            var invalidOwners = _subscriberOwners.Where(kvp =>
                kvp.Value == null || // C# null
                !kvp.Value // Unity null check for destroyed objects
            ).Select(kvp => kvp.Key).ToList();

            foreach (var invalidKey in invalidOwners)
            {
                _subscriberOwners.Remove(invalidKey);
            }
        }

        private static SignalSubscriberInfo CreateSubscriberInfo(Delegate handler, Type signalType, MonoBehaviour owner)
        {
            var info = new SignalSubscriberInfo
            {
                SignalType = signalType,
                MethodName = handler.Method.Name,
                IsValid = handler.Target != null
            };

            // If we have owner information, use it first (but check if it's still valid)
            if (owner != null && owner) // Unity null check for destroyed objects
            {
                info.OwnerGameObject = owner.gameObject;
                info.TargetObject = owner;
                info.TargetName = $"{owner.gameObject.name}.{owner.GetType().Name}";
            }
            else if (handler.Target != null)
            {
                var target = handler.Target;
                info.TargetName = target.GetType().Name;

                if (target is UnityEngine.Object unityObj)
                {
                    // Check if Unity object is still valid (not destroyed)
                    if (unityObj)
                    {
                        info.TargetObject = unityObj;

                        if (target is MonoBehaviour monoBehaviour)
                        {
                            info.OwnerGameObject = monoBehaviour.gameObject;
                            info.TargetName = $"{monoBehaviour.gameObject.name}.{target.GetType().Name}";
                        }
                        else if (target is Component component)
                        {
                            info.OwnerGameObject = component.gameObject;
                            info.TargetName = $"{component.gameObject.name}.{target.GetType().Name}";
                        }
                        else if (target is GameObject gameObject)
                        {
                            info.OwnerGameObject = gameObject;
                            info.TargetName = gameObject.name;
                        }
                    }
                    else
                    {
                        // Unity object was destroyed, mark as invalid
                        info.IsValid = false;
                        info.TargetName = $"{target.GetType().Name} (Destroyed)";
                    }
                }
                else
                {
                    // Non-Unity object - could be lambda closure
                    // Try to extract MonoBehaviour from closure fields
                    var closureTarget = TryExtractMonoBehaviourFromClosure(target);
                    if (closureTarget != null && closureTarget)
                    {
                        info.OwnerGameObject = closureTarget.gameObject;
                        info.TargetObject = closureTarget;
                        info.TargetName = $"{closureTarget.gameObject.name}.{closureTarget.GetType().Name}";
                    }
                    else
                    {
                        info.TargetName = target.GetType().Name;
                    }
                }
            }
            else if (owner != null) // Owner was provided but is now destroyed
            {
                info.IsValid = false;
                info.TargetName = "MonoBehaviour (Destroyed)";
            }
            else
            {
                // Static method
                info.TargetName = $"{handler.Method.DeclaringType?.Name} (Static)";
                info.MethodName = handler.Method.Name;
            }

            return info;
        }

        private static MonoBehaviour TryExtractMonoBehaviourFromClosure(object closureTarget)
        {
            if (closureTarget == null) return null;

            try
            {
                // Lambda closures often have a field called "<>4__this" that contains the MonoBehaviour
                var fields = closureTarget.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (field.FieldType.IsSubclassOf(typeof(MonoBehaviour)))
                    {
                        var value = field.GetValue(closureTarget);
                        if (value is MonoBehaviour monoBehaviour && monoBehaviour)
                        {
                            return monoBehaviour;
                        }
                    }
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            return null;
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Information about a signal channel for editor tools.
    /// </summary>
    public class SignalChannelInfo
    {
        public Type SignalType { get; set; }
        public int SubscriberCount { get; set; }
        internal ISignalChannel Channel { get; set; }
    }

    /// <summary>
    /// Information about a signal subscriber for editor tools.
    /// </summary>
    public class SignalSubscriberInfo
    {
        public string MethodName { get; set; }
        public string TargetName { get; set; }
        public UnityEngine.Object TargetObject { get; set; }
        public GameObject OwnerGameObject { get; set; }
        public Type SignalType { get; set; }
        public bool IsValid { get; set; }
    }
#endif
}
