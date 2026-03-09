#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    /// <summary>Lightweight in-memory log store for editor visualization. Compiled out in player builds.</summary>
    internal static class SignalLogStore
    {
        public static bool Enabled = true;
        public static int Capacity = 256;

        private static readonly LinkedList<SignalEmitLog> _buffer = new();
        private static readonly object _lock = new();

        [ThreadStatic] private static MonoBehaviour _currentEmitter;
        [ThreadStatic] private static string _currentFile;
        [ThreadStatic] private static int _currentLine;

        public readonly struct EmitterContextScope : IDisposable
        {
            private readonly MonoBehaviour _prevEmitter;
            private readonly string _prevFile;
            private readonly int _prevLine;

            public EmitterContextScope(MonoBehaviour emitter, string file, int line)
            {
                _prevEmitter = _currentEmitter;
                _prevFile = _currentFile;
                _prevLine = _currentLine;

                _currentEmitter = emitter;
                _currentFile = file;
                _currentLine = line;
            }

            public void Dispose()
            {
                _currentEmitter = _prevEmitter;
                _currentFile = _prevFile;
                _currentLine = _prevLine;
            }
        }

        public static EmitterContextScope Emitter(MonoBehaviour owner, string file, int line)
            => new EmitterContextScope(owner, file, line);

        [InitializeOnLoadMethod]
        private static void InitializeEditorHooks()
        {
            Clear();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    Clear();
            };
        }

        public static SignalEmitLog BeginEmit(Type signalType, object payload)
        {
            if (!Enabled || signalType == null) return null;

            var entry = new SignalEmitLog
            {
                SignalType = signalType,
                SignalTypeName = signalType.Name,
                Time = DateTime.Now,
                Frame = Time.frameCount,
                Id = ++_nextId,
            };

            entry.PayloadIsNull = payload == null;
            entry.PayloadFields = BuildPayloadFields(payload, out var reflectionError);
            entry.PayloadReflectionError = reflectionError;
            entry.PayloadInspectableMembersFound = entry.PayloadFields != null && entry.PayloadFields.Count > 0;

            try
            {
                if (_currentEmitter)
                {
                    entry.EmitterObject = _currentEmitter;
                    entry.EmitterComponentName = _currentEmitter.GetType().Name;
                    entry.EmitterGameObjectName = _currentEmitter.gameObject ? _currentEmitter.gameObject.name : "<GO>";
                }

                if (!string.IsNullOrEmpty(_currentFile))
                {
                    entry.ScriptFilePath = _currentFile;
                    entry.ScriptLine = _currentLine;
                }

                if (string.IsNullOrEmpty(entry.ScriptFilePath))
                {
                    var st = new System.Diagnostics.StackTrace(true);
                    for (int i = 0; i < st.FrameCount; i++)
                    {
                        var f = st.GetFrame(i);
                        var m = f.GetMethod();
                        var dt = m?.DeclaringType;
                        if (dt == null) continue;
                        if ((dt.Namespace ?? string.Empty).StartsWith("NekoSignal")) continue;

                        entry.ScriptFilePath = f.GetFileName();
                        entry.ScriptLine = f.GetFileLineNumber();
                        if (string.IsNullOrEmpty(entry.EmitterComponentName))
                            entry.EmitterComponentName = dt.Name;
                        if (string.IsNullOrEmpty(entry.EmitterGameObjectName) && _currentEmitter)
                            entry.EmitterGameObjectName = _currentEmitter.gameObject ? _currentEmitter.gameObject.name : null;
                        break;
                    }
                }
            }
            catch { }

            lock (_lock)
            {
                _buffer.AddFirst(entry);
                while (_buffer.Count > Capacity)
                    _buffer.RemoveLast();
            }

            return entry;
        }

        public static void AddFilters(SignalEmitLog entry, IReadOnlyList<ISignalFilter> filters)
        {
            if (!Enabled || entry == null || filters == null || filters.Count == 0) return;
            try
            {
                for (int i = 0; i < filters.Count; i++)
                {
                    if (filters[i] != null) entry.Filters.Add(filters[i].GetType().Name);
                }
            }
            catch { }
        }

        public static void AddInvocation(SignalEmitLog entry, string method, string component, string gameObject, int priority, bool threw, string exceptionMsg)
        {
            if (!Enabled || entry == null) return;
            entry.Invocations.Add(new SignalInvocationLog
            {
                MethodName = method,
                ComponentName = component,
                GameObjectName = gameObject,
                Priority = priority,
                Threw = threw,
                ExceptionMessage = exceptionMsg
            });
        }

        public static List<SignalEmitLog> GetLogs()
        {
            lock (_lock) { return _buffer.ToList(); }
        }

        public static IEnumerable<Type> GetSignalTypes()
        {
            lock (_lock)
            {
                var counts = new Dictionary<Type, int>();
                foreach (var b in _buffer)
                {
                    if (!counts.ContainsKey(b.SignalType)) counts[b.SignalType] = 0;
                    counts[b.SignalType]++;
                }
                return counts
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key.Name)
                    .Select(kv => kv.Key)
                    .ToList();
            }
        }

        public static void Clear()
        {
            lock (_lock) { _buffer.Clear(); }
        }

        private static int _nextId;

        private static List<PayloadField> BuildPayloadFields(object payload, out bool reflectionFailed)
        {
            var list = new List<PayloadField>();
            reflectionFailed = false;
            if (payload == null) return list;
            try
            {
                var t = payload.GetType();
                var seen = new HashSet<string>();

                // 1) Public instance fields
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    object v = null; try { v = f.GetValue(payload); } catch { }
                    list.Add(new PayloadField { Name = f.Name, Value = FormatValue(v) });
                    seen.Add(f.Name);
                }

                // 2) Private serialized fields
                foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (seen.Contains(f.Name)) continue;
                    try
                    {
                        if (f.GetCustomAttribute<SerializeField>() != null)
                        {
                            object v = null; try { v = f.GetValue(payload); } catch { }
                            list.Add(new PayloadField { Name = f.Name.TrimStart('<').TrimEnd('>'), Value = FormatValue(v) });
                            seen.Add(f.Name);
                        }
                    }
                    catch { }
                }

                // 3) Public readable properties (no indexers)
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!p.CanRead || p.GetIndexParameters()?.Length > 0 || seen.Contains(p.Name)) continue;
                    object v = null; try { v = p.GetValue(payload, null); } catch { }
                    list.Add(new PayloadField { Name = p.Name, Value = FormatValue(v) });
                    seen.Add(p.Name);
                }

                return list;
            }
            catch
            {
                reflectionFailed = true;
                return list;
            }
        }

        private static string FormatValue(object v)
        {
            if (v == null) return "null";
            if (v is string s) return "\"" + s + "\"";
            if (v is bool b) return b ? "true" : "false";
            if (v is Enum) return v.ToString();
            if (v is ValueType) return v.ToString();
            if (v is UnityEngine.Object uo) return uo ? uo.name : "(Destroyed)";
            return v.ToString();
        }
    }
}
#endif
