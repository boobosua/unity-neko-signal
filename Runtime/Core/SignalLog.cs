#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    [Serializable]
    internal class SignalInvocationLog
    {
        public string MethodName;
        public string ComponentName;
        public string GameObjectName;
        public int Priority;
        public bool Threw;
        public string ExceptionMessage;
    }

    [Serializable]
    internal class SignalPublishLog
    {
        public Type SignalType;
        public string SignalTypeName;
        public DateTime Time;
        public int Frame;
        public List<PayloadField> PayloadFields = new();
        public bool PayloadExpanded = false;
        public List<SignalInvocationLog> Invocations = new();
        public int Id; // unique incremental id for stable tracking

        // Payload state diagnostics
        public bool PayloadIsNull;
        public bool PayloadReflectionError; // true if reflection threw and we aborted
        public bool PayloadInspectableMembersFound; // true if any members captured

        // Publisher context (editor-only logging)
        public string PublisherComponentName;
        public string PublisherGameObjectName;
        public UnityEngine.Object PublisherObject; // MonoBehaviour or GameObject for ping/select
        public string ScriptFilePath;
        public int ScriptLine;

        // Filters used during publish (for filtered publishes)
        public List<string> Filters = new();
    }

    [Serializable]
    internal class PayloadField
    {
        public string Name;
        public string Value;
    }

    /// <summary>
    /// Lightweight in-memory log store for editor visualization.
    /// Editor-only (compiled out in player builds).
    /// </summary>
    internal static class SignalLogStore
    {
        public static bool Enabled = true;
        public static int Capacity = 256;

        private static readonly LinkedList<SignalPublishLog> _buffer = new();
        private static readonly object _lock = new();

        // Thread-static publisher context to be set by extensions when available
        [ThreadStatic] private static MonoBehaviour _currentPublisher;
        [ThreadStatic] private static string _currentFile;
        [ThreadStatic] private static int _currentLine;

        public readonly struct PublisherContextScope : IDisposable
        {
            private readonly MonoBehaviour _prevPublisher;
            private readonly string _prevFile;
            private readonly int _prevLine;

            public PublisherContextScope(MonoBehaviour publisher, string file, int line)
            {
                _prevPublisher = _currentPublisher;
                _prevFile = _currentFile;
                _prevLine = _currentLine;

                _currentPublisher = publisher;
                _currentFile = file;
                _currentLine = line;
            }

            public void Dispose()
            {
                _currentPublisher = _prevPublisher;
                _currentFile = _prevFile;
                _currentLine = _prevLine;
            }
        }

        public static PublisherContextScope Publisher(MonoBehaviour owner, string file, int line)
            => new PublisherContextScope(owner, file, line);

        public static SignalPublishLog BeginPublish(Type signalType, object payload)
        {
            if (!Enabled || signalType == null) return null;
            var entry = new SignalPublishLog
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

            // Populate publisher context from scope or stack trace
            try
            {
                // Prefer explicit scope context
                if (_currentPublisher)
                {
                    entry.PublisherObject = _currentPublisher;
                    entry.PublisherComponentName = _currentPublisher.GetType().Name;
                    entry.PublisherGameObjectName = _currentPublisher.gameObject ? _currentPublisher.gameObject.name : "<GO>";
                }

                // File/line from scope if provided
                if (!string.IsNullOrEmpty(_currentFile))
                {
                    entry.ScriptFilePath = _currentFile;
                    entry.ScriptLine = _currentLine;
                }

                // If missing, try to infer from stack trace
                if (string.IsNullOrEmpty(entry.ScriptFilePath))
                {
                    var st = new System.Diagnostics.StackTrace(true);
                    for (int i = 0; i < st.FrameCount; i++)
                    {
                        var f = st.GetFrame(i);
                        var m = f.GetMethod();
                        var dt = m?.DeclaringType;
                        if (dt == null) continue;
                        var ns = dt.Namespace ?? string.Empty;
                        if (ns.StartsWith("NekoSignal")) continue; // skip library frames
#if UNITY_EDITOR
                        // Prefer MonoBehaviour frames
                        if (typeof(MonoBehaviour).IsAssignableFrom(dt) || true)
                        {
                            entry.ScriptFilePath = f.GetFileName();
                            entry.ScriptLine = f.GetFileLineNumber();
                            if (string.IsNullOrEmpty(entry.PublisherComponentName))
                                entry.PublisherComponentName = dt.Name;
                            if (string.IsNullOrEmpty(entry.PublisherGameObjectName) && _currentPublisher)
                                entry.PublisherGameObjectName = _currentPublisher.gameObject ? _currentPublisher.gameObject.name : null;
                            break;
                        }
#endif
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

        public static void AddFilters(SignalPublishLog entry, ISignalFilter[] filters)
        {
            if (!Enabled || entry == null || filters == null || filters.Length == 0) return;
            try
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    var f = filters[i];
                    if (f == null) continue;
                    entry.Filters.Add(f.GetType().Name);
                }
            }
            catch { }
        }

        public static void AddInvocation(SignalPublishLog entry, string method, string component, string gameObject, int priority, bool threw, string exceptionMsg)
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

        public static List<SignalPublishLog> GetLogs()
        {
            lock (_lock)
            {
                return _buffer.ToList();
            }
        }

        public static IEnumerable<Type> GetSignalTypes()
        {
            lock (_lock)
            {
                // Only show types currently present in the buffer (active recently)
                // Order by activity (count) desc, then name
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
            lock (_lock)
            {
                _buffer.Clear();
            }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitializeEditorHooks()
        {
            // Clear once when the project/editor domain loads
            Clear();
            EditorApplication.playModeStateChanged += state =>
            {
                // Only clear when entering play mode (fresh run)
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    Clear();
                }
            };
        }
#endif

        private static int _nextId = 0;

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
                var pubFields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var f in pubFields)
                {
                    object v = null; try { v = f.GetValue(payload); } catch { }
                    list.Add(new PayloadField { Name = f.Name, Value = FormatValue(v) });
                    seen.Add(f.Name);
                }

                // 2) Private serialized fields ([SerializeField])
                var nonPubFields = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var f in nonPubFields)
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
                    catch { /* ignore reflection edge cases */ }
                }

                // 3) Public readable properties (no indexers)
                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var p in props)
                {
                    if (!p.CanRead) continue;
                    if (p.GetIndexParameters()?.Length > 0) continue; // skip indexers
                    if (seen.Contains(p.Name)) continue;
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
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is Enum) return v.ToString();
            if (v is ValueType) return v.ToString();
            if (v is UnityEngine.Object uo) return uo ? uo.name : "(Destroyed)";
            return v.ToString();
        }
    }
}
#endif
