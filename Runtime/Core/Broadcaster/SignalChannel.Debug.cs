#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NekoSignal
{
    internal sealed partial class SignalChannel<T>
    {
        /// <summary>Returns subscriber info for all current subscribers; cleans up stale owners first.</summary>
        public IEnumerable<SignalSubscriberInfo> GetSubscriberInfo()
        {
            if (_subs == null || _subs.Count == 0)
                return Array.Empty<SignalSubscriberInfo>();

            CleanupStaleSubscribers();

            var result = new SignalSubscriberInfo[_subs.Count];
            for (int i = 0; i < _subs.Count; i++)
                result[i] = CreateSubscriberInfo(_subs[i].Callback, typeof(T), _subs[i].Owner, _subs[i].Priority);
            return result;
        }

        private void CleanupStaleSubscribers()
        {
            if (_isInvoking || _subs == null || _subs.Count == 0) return;

            for (int i = _subs.Count - 1; i >= 0; i--)
            {
                if (!_subs[i].Owner)
                    _subs.RemoveAt(i);
            }
        }

        private static SignalSubscriberInfo CreateSubscriberInfo(Delegate handler, Type signalType, MonoBehaviour owner, int priority)
        {
            var info = new SignalSubscriberInfo
            {
                SignalType = signalType,
                MethodName = handler.Method.Name,
                IsValid = handler.Target != null,
                Priority = priority
            };

            if (owner)
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
                    if (unityObj)
                    {
                        info.TargetObject = unityObj;

                        if (target is MonoBehaviour mb)
                        {
                            info.OwnerGameObject = mb.gameObject;
                            info.TargetName = $"{mb.gameObject.name}.{target.GetType().Name}";
                        }
                        else if (target is Component comp)
                        {
                            info.OwnerGameObject = comp.gameObject;
                            info.TargetName = $"{comp.gameObject.name}.{target.GetType().Name}";
                        }
                        else if (target is GameObject go)
                        {
                            info.OwnerGameObject = go;
                            info.TargetName = go.name;
                        }
                    }
                    else
                    {
                        info.IsValid = false;
                        info.TargetName = $"{target.GetType().Name} (Destroyed)";
                    }
                }
                else
                {
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
            else if (owner != null)
            {
                info.IsValid = false;
                info.TargetName = "MonoBehaviour (Destroyed)";
            }
            else
            {
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
                var fields = closureTarget.GetType().GetFields(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (field.FieldType.IsSubclassOf(typeof(MonoBehaviour)))
                    {
                        var value = field.GetValue(closureTarget);
                        if (value is MonoBehaviour mb && mb)
                            return mb;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
#endif
