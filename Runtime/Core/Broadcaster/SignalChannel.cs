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

    internal sealed partial class SignalChannel<T> : ISignalChannel where T : ISignal
    {
        private struct Sub
        {
            public Action<T> Callback;
            public MonoBehaviour Owner;
            public int Priority;
        }

        private readonly List<Sub> _subs = new();

        private bool _isInvoking;
        private readonly List<int> _pendingRemovals = new();

        public int SubscriberCount => _subs.Count;

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
        }

        public void RemoveCallback(Action<T> callback)
        {
            if (callback == null) return;

            for (int i = 0; i < _subs.Count; i++)
            {
                if (_subs[i].Callback == callback)
                {
                    if (_isInvoking)
                        _pendingRemovals.Add(i);
                    else
                        _subs.RemoveAt(i);
                    return;
                }
            }
        }

        public void Clear()
        {
            _subs.Clear();
            _pendingRemovals.Clear();
        }

        public void Publish(T signal)
        {
            if (_subs.Count == 0) return;

#if UNITY_EDITOR
            var logEntry = SignalLogStore.BeginPublish(typeof(T), signal);
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

        public void PublishFiltered(T signal, ISignalFilter[] filters)
        {
            if (_subs.Count == 0) return;

#if UNITY_EDITOR
            var logEntry = SignalLogStore.BeginPublish(typeof(T), signal);
            if (filters != null && filters.Length > 0)
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
                        for (int f = 0; f < filters.Length; f++)
                        {
                            if (!filters[f].Evaluate(owner))
                            {
                                pass = false;
                                break;
                            }
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

            _pendingRemovals.Sort();
            int write = 0;
            for (int r = 1; r < _pendingRemovals.Count; r++)
                if (_pendingRemovals[r] != _pendingRemovals[write])
                    _pendingRemovals[++write] = _pendingRemovals[r];
            int count = write + 1;

            for (int idx = count - 1; idx >= 0; idx--)
            {
                int i = _pendingRemovals[idx];
                if (i >= 0 && i < _subs.Count)
                    _subs.RemoveAt(i);
            }
            _pendingRemovals.Clear();
        }
    }
}
