#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    public partial class SignalTrackerWindow
    {
        private int _selectedLogSignalIndex = 0;
        private Type _selectedLogSignalType = null;
        private Vector2 _logLeftScroll, _logRightScroll;

        private void DrawSignalLogView()
        {
            var logs = SignalLogStore.GetLogs();
            var types = SignalLogStore.GetSignalTypes().ToList();

            // Keep selection stable across dynamic type reordering
            if (types.Count > 0)
            {
                if (_selectedLogSignalType == null || !types.Contains(_selectedLogSignalType))
                {
                    _selectedLogSignalType = types[0];
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
            var labelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            var countStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            for (int i = 0; i < types.Count; i++)
            {
                var t = types[i];
                var count = logs.Count(l => l.SignalType == t);
                var rowRect = EditorGUILayout.GetControlRect(false, 22f);
                bool selected = (t == _selectedLogSignalType);
                if (selected)
                {
                    EditorGUI.DrawRect(rowRect, EditorGUIUtility.isProSkin ? new Color(0.25f, 0.45f, 0.65f, 0.35f) : new Color(0.6f, 0.75f, 0.95f, 0.6f));
                }
                var nameRect = new Rect(rowRect.x + 6, rowRect.y + 3, rowRect.width - 60, rowRect.height - 6);
                var countRect = new Rect(rowRect.xMax - 50, rowRect.y + 3, 44, rowRect.height - 6);
                GUI.Label(nameRect, t.Name, labelStyle);
                GUI.Label(countRect, count.ToString(), countStyle);
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    _selectedLogSignalType = t;
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

            Rect rowRect = EditorGUILayout.GetControlRect(false, ROW_HEIGHT);
            GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);
            var innerRect = new Rect(rowRect.x + 3, rowRect.y + 3, rowRect.width - 6, rowRect.height - 6);
            var bg = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.06f);
            EditorGUI.DrawRect(innerRect, bg);

            float foldW = 14f;
            var foldRect = new Rect(rowRect.x + 6, rowRect.y + (rowRect.height - EditorGUIUtility.singleLineHeight) * 0.5f, foldW, EditorGUIUtility.singleLineHeight);
            bool newExpanded = EditorGUI.Foldout(foldRect, e.PayloadExpanded, GUIContent.none, true);
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
            var baseStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            var compStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            var goStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };

            if (EditorGUIUtility.isProSkin)
            {
                compStyle.normal.textColor = new Color(0.55f, 0.8f, 1f, 1f);
                compStyle.hover.textColor = new Color(0.65f, 0.9f, 1f, 1f);
                compStyle.active.textColor = new Color(0.8f, 0.95f, 1f, 1f);
                goStyle.normal.textColor = new Color(0.6f, 0.95f, 0.9f, 1f);
                goStyle.hover.textColor = new Color(0.7f, 1f, 0.95f, 1f);
                goStyle.active.textColor = new Color(0.85f, 1f, 0.98f, 1f);
            }
            else
            {
                compStyle.normal.textColor = new Color(0.05f, 0.35f, 0.75f, 1f);
                compStyle.hover.textColor = new Color(0.1f, 0.45f, 0.9f, 1f);
                compStyle.active.textColor = new Color(0.15f, 0.5f, 1f, 1f);
                goStyle.normal.textColor = new Color(0f, 0.45f, 0.4f, 1f);
                goStyle.hover.textColor = new Color(0f, 0.6f, 0.55f, 1f);
                goStyle.active.textColor = new Color(0f, 0.7f, 0.65f, 1f);
            }

            var timeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            var timeBadgeStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 2, 2) };
            var filterStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 10, fontStyle = FontStyle.Italic };
            filterStyle.normal.textColor = new Color(1f, 0.9f, 0.4f, 1f);

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

            string atTxt = " at ";
            string timeTxt = e.Time.ToString("HH:mm:ss");

            Vector2 compSize = compStyle.CalcSize(new GUIContent(comp));
            Vector2 inSize = baseStyle.CalcSize(new GUIContent(" in "));
            Vector2 goSize = goStyle.CalcSize(new GUIContent(go));
            Vector2 raisedSize = baseStyle.CalcSize(new GUIContent(" raised "));
            Vector2 atSize = baseStyle.CalcSize(new GUIContent(atTxt));
            Vector2 timeSize = timeStyle.CalcSize(new GUIContent(timeTxt));

            float x = rect.x;
            var compRect = new Rect(x, rect.y, compSize.x, rect.height); x += compSize.x;
            var inRect = new Rect(x, rect.y, inSize.x, rect.height); x += inSize.x;
            var goRect = new Rect(x, rect.y, goSize.x, rect.height); x += goSize.x;
            var raisedRect = new Rect(x, rect.y, raisedSize.x, rect.height); x += raisedSize.x;
            var atRect = new Rect(x, rect.y, atSize.x, rect.height); x += atSize.x;

            float padV = 4f, padW = 6f;
            float badgeW = timeSize.x + padW * 2f;
            float badgeH = timeSize.y + padV;
            var timeRect = new Rect(x, rect.y + (rect.height - badgeH) * 0.5f, badgeW, badgeH); x += timeRect.width + 6f;

            string filterLabel = null;
            if (e.Filters != null && e.Filters.Count > 0)
                filterLabel = " filter by " + string.Join(", ", e.Filters);
            Vector2 filterSize = Vector2.zero;
            if (!string.IsNullOrEmpty(filterLabel)) filterSize = filterStyle.CalcSize(new GUIContent(filterLabel));
            var filterRect = new Rect(x, rect.y, Mathf.Min(filterSize.x, rect.xMax - x), rect.height);

            if (GUI.Button(compRect, comp, compStyle))
                TryOpenEmitterScript(e);
            GUI.Label(inRect, " in ", baseStyle);
            if (GUI.Button(goRect, go, goStyle))
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
            GUI.Label(raisedRect, " raised ", baseStyle);
            GUI.Label(atRect, atTxt, baseStyle);

            var prev = GUI.color;
            GUI.color = EditorGUIUtility.isProSkin ? new Color(0.15f, 0.85f, 0.95f, 0.9f) : new Color(0f, 0.65f, 0.7f, 0.9f);
            GUI.Box(timeRect, timeTxt, timeBadgeStyle);
            GUI.color = prev;

            if (!string.IsNullOrEmpty(filterLabel))
                GUI.Label(filterRect, filterLabel, filterStyle);

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
                var pattern = @"\b" + Regex.Escape(subscriber.MethodName) + @"\s*\(";
                var rx = new Regex(pattern);
                int lineNumber = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (rx.IsMatch(lines[i]))
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
                Debug.LogWarning($"[SignalTracker] Failed to open method: {ex.Message}");
            }
        }
    }
}
#endif
