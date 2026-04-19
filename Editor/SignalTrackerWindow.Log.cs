#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NekoLib.Logger;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    internal partial class SignalTrackerWindow
    {
        private int _selectedLogSignalIndex = 0;
        private Type _selectedLogSignalType = null;
        private Vector2 _logLeftScroll, _logRightScroll;

        private void DrawSignalLogView()
        {
            var logs  = SignalLogStore.GetLogs();
            var types = SignalLogStore.GetSignalTypes().ToList();

            // Keep selection stable across dynamic type reordering
            if (types.Count > 0)
            {
                if (_selectedLogSignalType == null || !types.Contains(_selectedLogSignalType))
                {
                    _selectedLogSignalType  = types[0];
                    _selectedLogSignalIndex = 0;
                }
                else
                {
                    _selectedLogSignalIndex = types.IndexOf(_selectedLogSignalType);
                }
            }

            EditorGUILayout.BeginHorizontal();

            // Left sidebar: signals
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            EditorGUILayout.LabelField("Signals", EditorStyles.boldLabel);
            var sep2 = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(new Rect(sep2.x, sep2.y, sep2.width, 1f), BORDER_COLOR);
            EditorGUILayout.Space(2);
            _logLeftScroll = EditorGUILayout.BeginScrollView(_logLeftScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < types.Count; i++)
            {
                var t     = types[i];
                var count = logs.Count(l => l.SignalType == t);
                var rowRect = EditorGUILayout.GetControlRect(false, 22f);
                bool selected = (t == _selectedLogSignalType);
                if (selected)
                    EditorGUI.DrawRect(rowRect, _selectionColor);
                var nameRect  = new Rect(rowRect.x + 6,     rowRect.y + 3, rowRect.width - 60, rowRect.height - 6);
                var countRect = new Rect(rowRect.xMax - 50, rowRect.y + 3, 44,                 rowRect.height - 6);
                GUI.Label(nameRect,  t.Name, _sidebarLabelStyle);
                GUI.Label(countRect, count.ToString(), _sidebarCountStyle);
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    _selectedLogSignalType  = t;
                    _selectedLogSignalIndex = i;
                    Event.current.Use();
                }
            }
            EditorGUILayout.EndScrollView();

            // Controls
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", GUILayout.Width(100)))
                SignalLogStore.Clear();
            SignalLogStore.Enabled = GUILayout.Toggle(SignalLogStore.Enabled, "Capture", GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUIUtility.labelWidth = 40f;
            int newCap = EditorGUILayout.IntField("Max", SignalLogStore.Capacity, GUILayout.Width(120));
            if (newCap != SignalLogStore.Capacity)
                SignalLogStore.Capacity = Mathf.Clamp(newCap, 16, 10000);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Vertical divider
            GUILayout.Box(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(1));
            var divRect = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(new Rect(divRect.x, divRect.y + 2, 1, Mathf.Max(0, divRect.height - 4)), BORDER_COLOR);

            // Right panel: log entries
            EditorGUILayout.BeginVertical();
            _logRightScroll = EditorGUILayout.BeginScrollView(_logRightScroll);

            if (types.Count == 0)
            {
                EditorGUILayout.HelpBox("No log entries yet. Trigger an emit to see logs.", MessageType.Info);
            }
            else
            {
                var selType = _selectedLogSignalType ?? types[Mathf.Clamp(_selectedLogSignalIndex, 0, types.Count - 1)];
                var entries = logs.Where(l => l.SignalType == selType).OrderByDescending(l => l.Time).ToList();

                foreach (var e in entries)
                {
                    DrawLogEntry(e);
                    EditorGUILayout.Space(6);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLogEntry(SignalEmitLog e)
        {
            EditorGUILayout.BeginVertical(GUIStyle.none);

            Rect rowRect  = EditorGUILayout.GetControlRect(false, ROW_HEIGHT);
            GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);
            var innerRect = new Rect(rowRect.x + 3, rowRect.y + 3, rowRect.width - 6, rowRect.height - 6);
            EditorGUI.DrawRect(innerRect, _zebraStripeColor);

            float foldW    = 14f;
            var   foldRect = new Rect(rowRect.x + 6, rowRect.y + (rowRect.height - EditorGUIUtility.singleLineHeight) * 0.5f, foldW, EditorGUIUtility.singleLineHeight);
            bool  newExpanded = EditorGUI.Foldout(foldRect, e.PayloadExpanded, GUIContent.none, true);
            if (newExpanded != e.PayloadExpanded)
                e.PayloadExpanded = newExpanded;

            var msgRect = new Rect(rowRect.x + 10 + foldW, rowRect.y, rowRect.width - (20 + foldW), rowRect.height);
            DrawEmitMessage(msgRect, e);

            if (e.PayloadExpanded)
            {
                var inner = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 6, 6) };
                EditorGUILayout.BeginVertical(inner);
                if (e.PayloadFields != null && e.PayloadFields.Count > 0)
                {
                    foreach (var f in e.PayloadFields)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(f.Name, GUILayout.Width(160));
                        EditorGUILayout.LabelField(f.Value, EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    var msg = e.PayloadReflectionError
                        ? "Payload could not be inspected (reflection error)."
                        : "Payload has no public/serialized fields or readable properties.";
                    EditorGUILayout.LabelField(msg, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEmitMessage(Rect rect, SignalEmitLog e)
        {
            string comp = string.IsNullOrEmpty(e.EmitterComponentName) ? "<Component>" : e.EmitterComponentName;

            string go;
            if (!string.IsNullOrEmpty(e.EmitterGameObjectName))
                go = e.EmitterGameObjectName;
            else if (e.EmitterObject is GameObject goObj && goObj)
                go = goObj.name;
            else if (e.EmitterObject != null)
                go = e.EmitterObject.GetType().Name;
            else if (!string.IsNullOrEmpty(e.EmitterComponentName))
                go = e.EmitterComponentName;
            else if (!string.IsNullOrEmpty(e.ScriptFilePath))
            {
                try { go = Path.GetFileNameWithoutExtension(e.ScriptFilePath) ?? "<Source>"; }
                catch { go = "<Source>"; }
            }
            else
                go = e.SignalTypeName ?? "<Source>";

            string atTxt  = " at ";
            string timeTxt = e.Time.ToString("HH:mm:ss");

            Vector2 compSize    = _logCompStyle.CalcSize(new GUIContent(comp));
            Vector2 inSize      = _logBaseStyle.CalcSize(new GUIContent(" in "));
            Vector2 goSize      = _logGoStyle.CalcSize(new GUIContent(go));
            Vector2 raisedSize  = _logBaseStyle.CalcSize(new GUIContent(" raised "));
            Vector2 atSize      = _logBaseStyle.CalcSize(new GUIContent(atTxt));
            Vector2 timeSize    = _logTimeStyle.CalcSize(new GUIContent(timeTxt));

            float x = rect.x;
            var compRect    = new Rect(x, rect.y, compSize.x,   rect.height); x += compSize.x;
            var inRect      = new Rect(x, rect.y, inSize.x,     rect.height); x += inSize.x;
            var goRect      = new Rect(x, rect.y, goSize.x,     rect.height); x += goSize.x;
            var raisedRect  = new Rect(x, rect.y, raisedSize.x, rect.height); x += raisedSize.x;
            var atRect      = new Rect(x, rect.y, atSize.x,     rect.height); x += atSize.x;

            float padV  = 4f, padW = 6f;
            float badgeW = timeSize.x + padW * 2f;
            float badgeH = timeSize.y + padV;
            var timeRect = new Rect(x, rect.y + (rect.height - badgeH) * 0.5f, badgeW, badgeH); x += timeRect.width + 6f;

            string filterLabel = null;
            if (e.Filters != null && e.Filters.Count > 0)
                filterLabel = " filter by " + string.Join(", ", e.Filters);
            Vector2 filterSize = Vector2.zero;
            if (!string.IsNullOrEmpty(filterLabel)) filterSize = _logFilterStyle.CalcSize(new GUIContent(filterLabel));
            var filterRect = new Rect(x, rect.y, Mathf.Min(filterSize.x, rect.xMax - x), rect.height);

            if (GUI.Button(compRect, comp, _logCompStyle))
                TryOpenEmitterScript(e);
            GUI.Label(inRect, " in ", _logBaseStyle);
            if (GUI.Button(goRect, go, _logGoStyle))
            {
                if (e.EmitterObject)
                {
                    if (e.EmitterObject is MonoBehaviour mb && mb)
                    {
                        EditorGUIUtility.PingObject(mb.gameObject);
                        Selection.activeGameObject = mb.gameObject;
                    }
                    else if (e.EmitterObject is GameObject g && g)
                    {
                        EditorGUIUtility.PingObject(g);
                        Selection.activeGameObject = g;
                    }
                }
            }
            GUI.Label(raisedRect, " raised ", _logBaseStyle);
            GUI.Label(atRect, atTxt, _logBaseStyle);

            var prev = GUI.color;
            GUI.color = _logTimeBadgeColor;
            GUI.Box(timeRect, timeTxt, _logTimeBadgeStyle);
            GUI.color = prev;

            if (!string.IsNullOrEmpty(filterLabel))
                GUI.Label(filterRect, filterLabel, _logFilterStyle);

            var evt = Event.current;
            if (evt.type == EventType.MouseUp && rect.Contains(evt.mousePosition))
            {
                if (!compRect.Contains(evt.mousePosition) && !goRect.Contains(evt.mousePosition) &&
                    !timeRect.Contains(evt.mousePosition) && !filterRect.Contains(evt.mousePosition))
                {
                    e.PayloadExpanded = !e.PayloadExpanded;
                    evt.Use();
                }
            }
        }

        private void TryOpenEmitterScript(SignalEmitLog e)
        {
            if (string.IsNullOrEmpty(e.ScriptFilePath) || e.ScriptLine <= 0)
            {
                if (e.EmitterObject is MonoBehaviour mb && mb)
                {
                    var ms = MonoScript.FromMonoBehaviour(mb);
                    if (ms) AssetDatabase.OpenAsset(ms);
                }
                return;
            }

            var rel = ToAssetsRelativePath(e.ScriptFilePath);
            MonoScript script = null;
            if (!string.IsNullOrEmpty(rel))
                script = AssetDatabase.LoadAssetAtPath<MonoScript>(rel);

            if (script)
                AssetDatabase.OpenAsset(script, e.ScriptLine);
            else
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(e.ScriptFilePath, e.ScriptLine);
        }

        private static string ToAssetsRelativePath(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            file = file.Replace('\\', '/');
            int idx = file.IndexOf("Assets/");
            return idx >= 0 ? file.Substring(idx) : null;
        }

        private void TryOpenSubscriberMethod(SignalSubscriberInfo subscriber)
        {
            try
            {
                MonoScript ms = null;
                if (subscriber?.TargetObject is MonoBehaviour mb && mb)
                    ms = MonoScript.FromMonoBehaviour(mb);
                else if (subscriber?.OwnerGameObject != null)
                {
                    var comp = subscriber.OwnerGameObject.GetComponent<MonoBehaviour>();
                    if (comp) ms = MonoScript.FromMonoBehaviour(comp);
                }

                if (!ms) return;

                var path = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(path))
                {
                    AssetDatabase.OpenAsset(ms);
                    return;
                }

                var lines = File.ReadAllLines(path);
                var searchTerm = subscriber.MethodName + "(";
                int lineNumber = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(searchTerm))
                    {
                        lineNumber = i + 1;
                        break;
                    }
                }

                if (lineNumber > 0)
                    AssetDatabase.OpenAsset(ms, lineNumber);
                else
                    AssetDatabase.OpenAsset(ms);
            }
            catch (Exception ex)
            {
                Log.Warn($"[SignalTracker] Failed to open method: {ex.Message}");
            }
        }
    }
}
#endif
