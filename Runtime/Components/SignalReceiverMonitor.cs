using System;
using System.Collections.Generic;
using NekoLib.Logger;
using UnityEngine;

namespace NekoSignal
{
    [DisallowMultipleComponent]
    internal sealed partial class SignalReceiverMonitor : MonoBehaviour
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
    }
}


