using System;
using System.Collections.Generic;
using NekoLib.Logger;
using UnityEngine;

namespace NekoSignal
{
    internal interface ISignalChannel
    {
        int SubscriberCount { get; }
        void Clear();
#if UNITY_EDITOR
        IEnumerable<SignalSubscriberInfo> GetSubscriberInfo();
#endif
    }

    internal sealed partial class SignalChannel<T> : ISignalChannel where T : struct, ISignal
    {
        private struct Sub
        {
            public Action<T> Callback;
            public MonoBehaviour Owner;
            public int Priority;
        }

        private readonly List<Sub> _subs = new();
        private int _activeCount;

        private bool _isInvoking;
        private readonly List<int> _pendingRemovals = new();

        public int SubscriberCount => _activeCount;

        public void AddCallback(Action<T> callback, MonoBehaviour owner, int priority)
        {
            if (callback == null || owner == null) return;
            var item = new Sub { Callback = callback, Owner = owner, Priority = priority };

            // Insert keeping descending priority order, preserving FIFO for same priority
            int insertIndex = _subs.Count;
            for (int i = 0; i < _subs.Count; i++)
            {
                if (item.Priority > _subs[i].Priority)
                {
                    insertIndex = i;
                    break;
                }
            }
            if (insertIndex == _subs.Count)
                _subs.Add(item);
            else
                _subs.Insert(insertIndex, item);

            _activeCount++;
        }

        public void RemoveCallback(Action<T> callback)
        {
            if (callback == null) return;

            for (int i = 0; i < _subs.Count; i++)
            {
                if (_subs[i].Callback == callback)
                {
                    if (_isInvoking)
                    {
                        _pendingRemovals.Add(i);
                        // Count is decremented in FlushPendingRemovals
                    }
                    else
                    {
                        _subs.RemoveAt(i);
                        _activeCount--;
                    }
                    return;
                }
            }
        }

        public void Clear()
        {
            _subs.Clear();
            _pendingRemovals.Clear();
            _activeCount = 0;
        }

        public void Emit(T signal)
        {
            if (_subs.Count == 0) return;

#if UNITY_EDITOR
            var logEntry = SignalLogStore.BeginEmit(typeof(T), signal);
#endif
            _isInvoking = true;
            try
            {
                for (int i = 0; i < _subs.Count; i++)
                {
                    var cb = _subs[i].Callback;
                    var owner = _subs[i].Owner;
                    var prio = _subs[i].Priority;

                    if (!owner)
                    {
                        _pendingRemovals.Add(i);
                        continue;
                    }

                    try
                    {
                        cb?.Invoke(signal);
#if UNITY_EDITOR
                        SignalLogStore.AddInvocation(logEntry,
                            cb?.Method?.Name ?? "<fn>",
                            owner ? owner.GetType().Name : "<owner>",
                            owner ? owner.gameObject.name : "<null>",
                            prio, false, null);
#endif
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[SignalChannel<{typeof(T).Name}>] Exception in subscriber ({owner.name}): {ex}");
#if UNITY_EDITOR
                        SignalLogStore.AddInvocation(logEntry,
                            cb?.Method?.Name ?? "<fn>",
                            owner ? owner.GetType().Name : "<owner>",
                            owner ? owner.gameObject.name : "<null>",
                            prio, true, ex.Message);
#endif
                    }
                }
            }
            finally
            {
                _isInvoking = false;
                FlushPendingRemovals();
            }
        }

        public void EmitFiltered(T signal, ISignalFilter[] filters) => EmitFilteredCore(signal, filters);

        internal void EmitFiltered(T signal, List<ISignalFilter> filters) => EmitFilteredCore(signal, filters);

        private void EmitFilteredCore(T signal, IReadOnlyList<ISignalFilter> filters)
        {
            if (_subs.Count == 0) return;

#if UNITY_EDITOR
            var logEntry = SignalLogStore.BeginEmit(typeof(T), signal);
            if (filters != null && filters.Count > 0)
                SignalLogStore.AddFilters(logEntry, filters);
#endif
            _isInvoking = true;
            try
            {
                for (int i = 0; i < _subs.Count; i++)
                {
                    var cb = _subs[i].Callback;
                    var owner = _subs[i].Owner;
                    var prio = _subs[i].Priority;

                    if (!owner)
                    {
                        _pendingRemovals.Add(i);
                        continue;
                    }

                    bool pass = true;
                    if (filters != null)
                    {
                        for (int f = 0; f < filters.Count; f++)
                        {
                            if (filters[f] == null)
                                throw new ArgumentNullException(nameof(filters), $"Filter at index {f} is null.");

                            bool result;
                            try
                            {
                                result = filters[f].Evaluate(owner);
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"[SignalChannel<{typeof(T).Name}>] Filter {filters[f].GetType().Name} threw: {ex.Message}");
                                result = false;
                            }

                            if (!result) { pass = false; break; }
                        }
                    }
                    if (!pass) continue;

                    try
                    {
                        cb?.Invoke(signal);
#if UNITY_EDITOR
                        SignalLogStore.AddInvocation(logEntry,
                            cb?.Method?.Name ?? "<fn>",
                            owner ? owner.GetType().Name : "<owner>",
                            owner ? owner.gameObject.name : "<null>",
                            prio, false, null);
#endif
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[SignalChannel<{typeof(T).Name}>] Exception in filtered subscriber ({owner.name}): {ex}");
#if UNITY_EDITOR
                        SignalLogStore.AddInvocation(logEntry,
                            cb?.Method?.Name ?? "<fn>",
                            owner ? owner.GetType().Name : "<owner>",
                            owner ? owner.gameObject.name : "<null>",
                            prio, true, ex.Message);
#endif
                    }
                }
            }
            finally
            {
                _isInvoking = false;
                FlushPendingRemovals();
            }
        }

        private void FlushPendingRemovals()
        {
            if (_pendingRemovals.Count == 0) return;

            // Sort ascending, remove from highest index downward so earlier indices stay valid.
            // Dedup handles the rare case where the same index appears twice (e.g. stale-owner sweep
            // and a concurrent pending removal queued in the same dispatch).
            _pendingRemovals.Sort();
            int prev = -1;
            for (int idx = _pendingRemovals.Count - 1; idx >= 0; idx--)
            {
                int i = _pendingRemovals[idx];
                if (i != prev && i >= 0 && i < _subs.Count)
                {
                    _subs.RemoveAt(i);
                    _activeCount--;
                    prev = i;
                }
            }
            _pendingRemovals.Clear();
            if (_pendingRemovals.Capacity > 16)
                _pendingRemovals.Capacity = 16;
        }
    }
}
