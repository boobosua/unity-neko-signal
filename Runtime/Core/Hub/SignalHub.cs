using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NekoLib.Logger;
using UnityEngine;

namespace NekoSignal
{
    /// <summary>Binds/unbinds MonoBehaviour methods marked with [OnSignal].</summary>
    [UnityEngine.Scripting.Preserve]
    public static partial class SignalHub
    {
        private static readonly Dictionary<Type, List<HandlerInfo>> _cache = new();
        private static readonly HashSet<(int instanceId, Type type)> _activeBindings = new();
        private static readonly Dictionary<int, List<(Type SignalType, Delegate Del)>> _boundDelegates = new();

        private static readonly MethodInfo _subscribeMethod =
            typeof(SignalBroadcaster).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == nameof(SignalBroadcaster.Subscribe)
                                     && m.IsGenericMethodDefinition
                                     && m.GetParameters().Length == 3);

        private static readonly MethodInfo _unsubscribeMethod =
            typeof(SignalBroadcaster).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == nameof(SignalBroadcaster.Unsubscribe)
                                     && m.IsGenericMethodDefinition
                                     && m.GetParameters().Length == 1);

        private sealed class HandlerInfo
        {
            public Type SignalType;
            public MethodInfo Method;
            public int Priority;
        }

        /// <summary>Discovers and subscribes all [OnSignal] methods on the target to their respective signal types.</summary>
        public static void Bind(MonoBehaviour target)
        {
            if (target == null) return;
            if (_subscribeMethod == null)
            {
                Log.Error("[SignalHub] Failed to resolve Subscribe via reflection. Bind will not work.");
                return;
            }

            var type = target.GetType();
            var key = (target.GetInstanceID(), type);

            if (_activeBindings.Contains(key))
                return; // already bound

            if (!_cache.TryGetValue(type, out var handlers))
            {
                handlers = DiscoverHandlers(type);
                _cache[type] = handlers;
            }

            var delegateList = new List<(Type, Delegate)>();
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

                    var subscribeClosed = _subscribeMethod.MakeGenericMethod(handler.SignalType);
                    subscribeClosed.Invoke(null, new object[] { target, del, handler.Priority });
                    delegateList.Add((handler.SignalType, del));
                }
                catch (Exception ex)
                {
                    Log.Error($"[SignalHub] Error binding {type.Name}.{handler.Method.Name}: {ex}");
                }
            }

            _boundDelegates[target.GetInstanceID()] = delegateList;
            _activeBindings.Add(key);
        }

        /// <summary>Unsubscribes all [OnSignal] methods previously bound on the target.</summary>
        public static void Unbind(MonoBehaviour target)
        {
            if (target == null) return;
            if (_unsubscribeMethod == null)
            {
                Log.Error("[SignalHub] Failed to resolve Unsubscribe via reflection. Unbind will not work.");
                return;
            }

            var type = target.GetType();
            var key = (target.GetInstanceID(), type);

            var instanceId = target.GetInstanceID();
            if (_boundDelegates.TryGetValue(instanceId, out var delegateList))
            {
                foreach (var (signalType, del) in delegateList)
                {
                    try
                    {
                        var unsubscribeClosed = _unsubscribeMethod.MakeGenericMethod(signalType);
                        unsubscribeClosed.Invoke(null, new object[] { del });
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[SignalHub] Error unbinding {type.Name}: {ex}");
                    }
                }
                _boundDelegates.Remove(instanceId);
            }

            _activeBindings.Remove(key);
        }

        /// <summary>Returns true if the target currently has active [OnSignal] bindings.</summary>
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

                    if (!sigType.IsValueType)
                    {
                        Log.Error($"[SignalHub] {type.Name}.{method.Name}: signal type {sigType.Name} must be a struct.");
                        continue;
                    }

                    list.Add(new HandlerInfo { SignalType = sigType, Method = method, Priority = attr.Priority });
                }
            }

            return list;
        }
    }
}
