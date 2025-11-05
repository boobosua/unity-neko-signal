using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NekoLib.Logger;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NekoSignal
{
    /// <summary>
    /// Binds/unbinds MonoBehaviour methods marked with [OnSignal].
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public static class SignalHub
    {
        private static readonly Dictionary<Type, List<HandlerInfo>> _cache = new();
        private static readonly HashSet<(int instanceId, Type type)> _activeBindings = new();

        private static readonly MethodInfo _subscribeGenericWithPriority =
            typeof(SignalBroadcaster).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == nameof(SignalBroadcaster.Subscribe)
                                     && m.IsGenericMethodDefinition
                                     && m.GetParameters().Length == 3);

        private static readonly MethodInfo _unsubscribeGeneric =
            typeof(SignalBroadcaster).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == nameof(SignalBroadcaster.Unsubscribe) && m.IsGenericMethodDefinition);

        private sealed class HandlerInfo
        {
            public Type SignalType;
            public MethodInfo Method;
            public int Priority;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void __EditorInit()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    _cache.Clear();
                    _activeBindings.Clear();
                }
            };
        }
#endif

        public static void Bind(MonoBehaviour target)
        {
            if (target == null) return;
            if (_subscribeGenericWithPriority == null) return;

            var type = target.GetType();
            var key = (target.GetInstanceID(), type);

            if (_activeBindings.Contains(key))
                return; // already bound

            if (!_cache.TryGetValue(type, out var handlers))
            {
                handlers = DiscoverHandlers(type);
                _cache[type] = handlers;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    var actionType = typeof(Action<>).MakeGenericType(handler.SignalType);
                    var del = Delegate.CreateDelegate(actionType, target, handler.Method, false);
                    if (del == null)
                    {
                        Log.Error($"[SignalHub] Failed to create delegate for {type.Name}.{handler.Method.Name}");
                        continue;
                    }

                    var subscribeClosed = _subscribeGenericWithPriority.MakeGenericMethod(handler.SignalType);
                    subscribeClosed.Invoke(null, new object[] { target, del, handler.Priority });
                }
                catch (Exception ex)
                {
                    Log.Error($"[SignalHub] Error binding {type.Name}.{handler.Method.Name}: {ex}");
                }
            }

            _activeBindings.Add(key);
        }

        public static void Unbind(MonoBehaviour target)
        {
            if (target == null) return;
            if (_unsubscribeGeneric == null) return;

            var type = target.GetType();
            var key = (target.GetInstanceID(), type);

            if (!_cache.TryGetValue(type, out var handlers))
            {
                _activeBindings.Remove(key);
                return;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    var actionType = typeof(Action<>).MakeGenericType(handler.SignalType);
                    var del = Delegate.CreateDelegate(actionType, target, handler.Method, false);
                    if (del == null) continue;

                    var unsubscribeClosed = _unsubscribeGeneric.MakeGenericMethod(handler.SignalType);
                    unsubscribeClosed.Invoke(null, new object[] { target, del });
                }
                catch (Exception ex)
                {
                    Log.Error($"[SignalHub] Error unbinding {type.Name}.{handler.Method.Name}: {ex}");
                }
            }

            _activeBindings.Remove(key);
        }

        public static bool IsBound(MonoBehaviour target)
        {
            if (target == null) return false;
            return _activeBindings.Contains((target.GetInstanceID(), target.GetType()));
        }

        private static List<HandlerInfo> DiscoverHandlers(Type type)
        {
            var list = new List<HandlerInfo>();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attrs = method.GetCustomAttributes<OnSignalAttribute>(true);
                foreach (var attr in attrs)
                {
                    var parms = method.GetParameters();
                    Type sigType = attr.ExplicitSignalType;

                    if (sigType == null)
                    {
                        if (parms.Length != 1)
                        {
                            Log.Error($"[SignalHub] {type.Name}.{method.Name} must have exactly one parameter.");
                            continue;
                        }
                        sigType = parms[0].ParameterType;
                    }
                    else
                    {
                        if (parms.Length != 1 || parms[0].ParameterType != sigType)
                        {
                            Log.Error($"[SignalHub] {type.Name}.{method.Name} must have exactly one parameter of type {sigType.Name}.");
                            continue;
                        }
                    }

                    if (!typeof(ISignal).IsAssignableFrom(sigType))
                    {
                        Log.Error($"[SignalHub] {type.Name}.{method.Name} parameter type {sigType.Name} does not implement ISignal.");
                        continue;
                    }

                    list.Add(new HandlerInfo { SignalType = sigType, Method = method, Priority = attr.Priority });
                }
            }

            return list;
        }
    }
}
