using System;
using System.Collections.Generic;
using UnityEngine;
using NekoLib.Logger;

#if UNITY_EDITOR
using System.Linq;
#endif

namespace NekoSignal
{
    [DisallowMultipleComponent]
    internal sealed class SignalReceiverMonitor : MonoBehaviour
    {
        private readonly Dictionary<Delegate, SignalReceiver> _receivers = new();

        public void AddReceiver(Delegate callback, SignalReceiver receiver)
        {
            if (receiver == null)
            {
                Log.Warn("[SignalReceiverMonitor] Cannot add null receiver.");
                return;
            }

            if (_receivers.ContainsKey(callback))
            {
                _receivers[callback]?.Dispose();
                _receivers.Remove(callback);
            }

            _receivers.Add(callback, receiver);
        }

        public void RemoveReceiver(Delegate callback)
        {
            if (callback == null)
            {
                Log.Warn("[SignalReceiverMonitor] Cannot remove null receiver.");
                return;
            }

            if (_receivers.TryGetValue(callback, out var receiver))
            {
                receiver?.Dispose();
                _receivers.Remove(callback);
            }
        }

        public void DisposeAllReceiversOfType<T>() where T : ISignal
        {
            var keysToRemove = new List<Delegate>();

            foreach (var kvp in _receivers)
            {
                if (kvp.Value.SignalType == typeof(T))
                {
                    kvp.Value?.Dispose();
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _receivers.Remove(key);
            }
        }

        public void DisposeAllReceivers()
        {
            foreach (var receiver in _receivers.Values)
            {
                receiver?.Dispose();
            }
            _receivers.Clear();
        }

        private void OnDisable()
        {
            DisposeAllReceivers();
        }

#if UNITY_EDITOR
        public int ActiveReceiversCount => _receivers.Count;

        public IEnumerable<(string CallbackInfo, bool IsActive)> GetReceiversInfo()
        {
            return _receivers.Select(kvp =>
            {
                var callback = kvp.Key;
                var receiver = kvp.Value;

                string callbackInfo = GetCallbackDisplayName(callback);
                bool isActive = receiver?.IsActive ?? false;

                return (callbackInfo, isActive);
            });
        }

        /// <summary>
        /// Detailed subscriber info for this monitor's GameObject, matching SignalBroadcaster data.
        /// </summary>
        public IEnumerable<SignalSubscriberInfo> GetDetailedSubscriberInfo()
        {
            var result = new List<SignalSubscriberInfo>();
            if (_receivers.Count == 0) return result;

            // Group receivers by signal type for efficient lookup
            var byType = _receivers
                .Where(kvp => kvp.Value != null)
                .GroupBy(kvp => kvp.Value.SignalType);

            foreach (var g in byType)
            {
                var type = g.Key;
                if (type == null) continue;
                var subs = SignalBroadcaster.GetSubscriberInfoByType(type);
                if (subs == null) continue;

                foreach (var kvp in g)
                {
                    var del = kvp.Key;
                    if (del == null) continue;
                    var methodName = del.Method.Name;
                    var target = del.Target;

                    // Try to match subscriber info by method name + target object if possible
                    SignalSubscriberInfo matched = null;
                    foreach (var si in subs)
                    {
                        if (si == null) continue;
                        if (si.MethodName != methodName) continue;
                        if (target == null)
                        {
                            // Static method case: accept first matching method name
                            matched = si; break;
                        }
                        if (target is UnityEngine.Object uo && si.TargetObject == uo)
                        {
                            matched = si; break;
                        }
                        if (target is MonoBehaviour mb && si.OwnerGameObject == mb.gameObject)
                        {
                            matched = si; break;
                        }
                    }
                    if (matched != null) result.Add(matched);
                }
            }
            return result;
        }

        private string GetCallbackDisplayName(Delegate callback)
        {
            if (callback == null) return "null";

            var method = callback.Method;
            var target = callback.Target;

            if (target != null)
            {
                // Instance method
                var targetName = target is UnityEngine.Object unityObj && unityObj != null
                    ? unityObj.name
                    : target.GetType().Name;
                return $"{targetName}.{method.Name}()";
            }
            else
            {
                // Static method
                return $"{method.DeclaringType?.Name}.{method.Name}()";
            }
        }
#endif
    }
}
