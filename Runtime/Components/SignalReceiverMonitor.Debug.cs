#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NekoSignal
{
    internal sealed partial class SignalReceiverMonitor
    {
        public int ActiveReceiversCount => _receivers.Count;

        public IEnumerable<(string CallbackInfo, bool IsActive)> GetReceiversInfo()
        {
            return _receivers.Select(kvp =>
            {
                string callbackInfo = GetCallbackDisplayName(kvp.Key);
                bool isActive = kvp.Value?.IsActive ?? false;
                return (callbackInfo, isActive);
            });
        }

        /// <summary>Detailed subscriber info for this monitor's GameObject, matching SignalBroadcaster data.</summary>
        public IEnumerable<SignalSubscriberInfo> GetDetailedSubscriberInfo()
        {
            var result = new List<SignalSubscriberInfo>();
            if (_receivers.Count == 0) return result;

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

                    SignalSubscriberInfo matched = null;
                    foreach (var si in subs)
                    {
                        if (si == null || si.MethodName != methodName) continue;
                        if (target == null) { matched = si; break; }
                        if (target is UnityEngine.Object uo && si.TargetObject == uo) { matched = si; break; }
                        if (target is MonoBehaviour mb && si.OwnerGameObject == mb.gameObject) { matched = si; break; }
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
                var targetName = target is UnityEngine.Object unityObj && unityObj != null
                    ? unityObj.name
                    : target.GetType().Name;
                return $"{targetName}.{method.Name}()";
            }
            return $"{method.DeclaringType?.Name}.{method.Name}()";
        }
    }
}
#endif
