#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    [CustomEditor(typeof(SignalReceiverMonitor))]
    public class SignalReceiverMonitorInspector : Editor
    {
        // Styling and sizes to match Tracker Window (minified)
        private const float ROW_HEIGHT = 22f;     // more compact rows
        private const float HEADER_HEIGHT = 24f;  // more compact header
        private static readonly Color HEADER_BG = new(0.2f, 0.2f, 0.2f, 0.4f);
        private static readonly Color BORDER_COLOR = new(0.5f, 0.5f, 0.5f, 0.3f);
        private const int ITEMS_PER_PAGE = 10;
        private static readonly System.Collections.Generic.Dictionary<int, int> _pages = new(); // instanceID -> page index
        private static readonly System.Collections.Generic.Dictionary<int, string> _pageTexts = new(); // instanceID -> input text

        public override void OnInspectorGUI()
        {
            var monitor = (SignalReceiverMonitor)target;
            if (!monitor) return;

            // Build list of this GameObject's active subscribers from the broadcaster
            var go = monitor.gameObject;
            var channels = SignalBroadcaster.GetAllChannelInfo();
            if (channels == null)
            {
                EditorGUILayout.HelpBox("SignalBroadcaster not available", MessageType.Warning);
                return;
            }

            var rows = new System.Collections.Generic.List<SignalSubscriberInfo>();
            foreach (var ch in channels)
            {
                var subs = SignalBroadcaster.GetSubscriberInfoByType(ch.SignalType);
                if (subs == null) continue;
                foreach (var s in subs)
                {
                    if (s == null || !s.IsValid) continue;
                    if (s.OwnerGameObject == go) rows.Add(s);
                }
            }

            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField("No active subscribers on this GameObject", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // Sort like the tracker: by priority desc then component name
            rows = rows
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => GetComponentDisplayName(r))
                .ToList();

            // Pagination setup
            int id = monitor.GetInstanceID();
            if (!_pages.ContainsKey(id)) _pages[id] = 0;
            int totalItems = rows.Count;
            int totalPages = Mathf.CeilToInt(totalItems / (float)ITEMS_PER_PAGE);
            _pages[id] = Mathf.Clamp(_pages[id], 0, Mathf.Max(0, totalPages - 1));

            // Pagination bar styled similar to Odin (compact toolbar style)
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            float lineH = EditorGUIUtility.singleLineHeight;
            var miniLabel = EditorStyles.miniLabel;
            // Compact, centered numeric field style matching miniLabel size
            var pageFieldStyle = new GUIStyle(EditorStyles.miniTextField)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = miniLabel.fontSize,
                fixedHeight = lineH,
            };
            // Reduce padding to keep height consistent with mini buttons
            pageFieldStyle.padding = new RectOffset(2, 2, 2, 2);

            var itemWord = totalItems == 1 ? "Item" : "Items";
            EditorGUILayout.LabelField($"{totalItems} {itemWord}", miniLabel, GUILayout.Width(80), GUILayout.Height(lineH));
            GUILayout.FlexibleSpace();

            // Prev
            GUI.enabled = _pages[id] > 0;
            if (GUILayout.Button("\u25C0", EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(lineH))) // ◀
            {
                _pages[id]--; _pageTexts[id] = (_pages[id] + 1).ToString();
            }
            GUI.enabled = true;

            // Centered page number input between prev/next
            if (!_pageTexts.ContainsKey(id) || string.IsNullOrEmpty(_pageTexts[id]))
                _pageTexts[id] = (_pages[id] + 1).ToString();

            GUILayout.Space(2);

            if (totalPages <= 0)
            {
                GUI.enabled = false;
                var zeroRect = GUILayoutUtility.GetRect(22, lineH, GUILayout.Width(22), GUILayout.Height(lineH));
                EditorGUI.TextField(zeroRect, "0", pageFieldStyle);
                GUI.enabled = true;
                EditorGUILayout.LabelField("/ 0", miniLabel, GUILayout.Width(24), GUILayout.Height(lineH));
            }
            else
            {
                // Input field [ n ] / total; reduced width for alignment
                string oldText = _pageTexts[id];
                var fieldRect = GUILayoutUtility.GetRect(22, lineH, GUILayout.Width(22), GUILayout.Height(lineH));
                string newText = EditorGUI.TextField(fieldRect, oldText, pageFieldStyle);
                if (!ReferenceEquals(oldText, newText))
                {
                    // Try to parse and clamp; invalid resets to current page
                    if (int.TryParse(newText, out int pageNum))
                    {
                        if (pageNum >= 1 && pageNum <= totalPages)
                        {
                            _pages[id] = pageNum - 1;
                            _pageTexts[id] = pageNum.ToString();
                        }
                        else
                        {
                            _pageTexts[id] = (_pages[id] + 1).ToString();
                        }
                    }
                    else
                    {
                        _pageTexts[id] = (_pages[id] + 1).ToString();
                    }
                }
                EditorGUILayout.LabelField($"/ {totalPages}", miniLabel, GUILayout.Width(24), GUILayout.Height(lineH));
            }

            GUILayout.Space(2);

            // Next
            GUI.enabled = _pages[id] < totalPages - 1;
            if (GUILayout.Button("\u25B6", EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(lineH))) // ▶
            {
                _pages[id]++; _pageTexts[id] = (_pages[id] + 1).ToString();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Header (Component | Method | Priority)
            DrawMiniHeader();

            // Page slice
            int start = _pages[id] * ITEMS_PER_PAGE;
            int end = Mathf.Min(start + ITEMS_PER_PAGE, totalItems);
            for (int i = start; i < end; i++)
            {
                DrawRow(rows[i], (i - start) % 2 == 0);
            }
        }

        private static Color GetZebraStripeColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(0f, 0f, 0f, 0.06f);
        }

        private void DrawMiniHeader()
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, HEADER_HEIGHT);
            EditorGUI.DrawRect(headerRect, HEADER_BG);
            DrawTableBorders(headerRect);

            float totalWidth = headerRect.width;
            // Allocate more room to Component and Method evenly; keep Priority narrow
            float priorityWidth = Mathf.Min(70f, totalWidth * 0.12f); // cap to ~70px
            float remaining = totalWidth - priorityWidth;
            float componentWidth = remaining * 0.5f;  // 50% of remaining
            float methodWidth = remaining * 0.5f;     // 50% of remaining

            float x = headerRect.x;
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

            GUI.Label(new Rect(x, headerRect.y, componentWidth, headerRect.height), "Component", headerStyle);
            x += componentWidth; DrawVerticalDivider(x, headerRect.y, headerRect.height);

            GUI.Label(new Rect(x, headerRect.y, methodWidth, headerRect.height), "Method", headerStyle);
            x += methodWidth; DrawVerticalDivider(x, headerRect.y, headerRect.height);

            GUI.Label(new Rect(x, headerRect.y, priorityWidth, headerRect.height), "Priority", headerStyle);
        }

        private void DrawRow(SignalSubscriberInfo s, bool isEven)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, ROW_HEIGHT);
            if (isEven) EditorGUI.DrawRect(rowRect, GetZebraStripeColor());
            DrawTableBorders(rowRect);

            float totalWidth = rowRect.width;
            float priorityWidth = Mathf.Min(70f, totalWidth * 0.12f);
            float remaining = totalWidth - priorityWidth;
            float componentWidth = remaining * 0.5f;
            float methodWidth = remaining * 0.5f;

            float x = rowRect.x;
            Color defaultTextColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            Color dimmedTextColor = Color.Lerp(defaultTextColor, Color.gray, 0.2f);

            // Component
            var compName = GetComponentDisplayName(s);
            DrawCenteredText(new Rect(x, rowRect.y, componentWidth, rowRect.height), compName, dimmedTextColor, 10);
            x += componentWidth; DrawVerticalDivider(x, rowRect.y, rowRect.height);

            // Method (clickable to open script if possible)
            var method = GetMethodDisplayName(s.MethodName);
            var linkCenter = new GUIStyle(EditorStyles.linkLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            var methodRect = new Rect(x, rowRect.y, methodWidth, rowRect.height);
            if (GUI.Button(new Rect(methodRect.x + 8, methodRect.y, methodRect.width - 16, methodRect.height), method, linkCenter))
            {
                TryOpenSubscriberMethod(s);
            }
            x += methodWidth; DrawVerticalDivider(x, rowRect.y, rowRect.height);

            // Priority
            DrawCenteredText(new Rect(x, rowRect.y, priorityWidth, rowRect.height), s.Priority.ToString(), dimmedTextColor, 10);
        }

        private string GetComponentDisplayName(SignalSubscriberInfo subscriber)
        {
            if (subscriber?.TargetObject is MonoBehaviour mb && mb)
                return mb.GetType().Name;
            if (subscriber?.TargetObject != null)
                return subscriber.TargetObject.GetType().Name;
            return subscriber?.TargetName ?? "N/A";
        }

        private string GetMethodDisplayName(string methodName)
        {
            if (string.IsNullOrEmpty(methodName)) return "Unknown Method";
            if (methodName.Contains("<") && methodName.Contains(">"))
            {
                if (methodName.Contains("<OnEnable>")) return "Lambda in OnEnable";
                if (methodName.Contains("<Start>")) return "Lambda in Start";
                if (methodName.Contains("<Awake>")) return "Lambda in Awake";
                if (methodName.Contains("b__")) return "Lambda Expression";
                return "Anonymous Method";
            }
            return methodName;
        }

        private void DrawCenteredText(Rect rect, string text, Color textColor, int fontSize = 12, FontStyle fontStyle = FontStyle.Normal)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = fontStyle,
                normal = { textColor = textColor }
            };
            GUI.Label(rect, text, style);
        }

        private void DrawTableBorders(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), BORDER_COLOR); // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), BORDER_COLOR); // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), BORDER_COLOR); // Left
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), BORDER_COLOR); // Right
        }

        private void DrawVerticalDivider(float x, float y, float height)
        {
            EditorGUI.DrawRect(new Rect(x - 1, y, 1, height), BORDER_COLOR);
        }

        private void TryOpenSubscriberMethod(SignalSubscriberInfo subscriber)
        {
            try
            {
                MonoScript ms = null;
                if (subscriber?.TargetObject is MonoBehaviour mb && mb)
                {
                    ms = MonoScript.FromMonoBehaviour(mb);
                }

                if (!ms) return;

                var path = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(path)) { AssetDatabase.OpenAsset(ms); return; }

                var lines = System.IO.File.ReadAllLines(path);
                var pattern = "\\b" + System.Text.RegularExpressions.Regex.Escape(subscriber.MethodName) + "\\s*\\(";
                var rx = new System.Text.RegularExpressions.Regex(pattern);
                int lineNumber = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (rx.IsMatch(lines[i])) { lineNumber = i + 1; break; }
                }
                if (lineNumber > 0) AssetDatabase.OpenAsset(ms, lineNumber); else AssetDatabase.OpenAsset(ms);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SignalReceiverMonitor] Failed to open method: {ex.Message}");
            }
        }

        // Hide the script field in the header by not drawing default header content
        protected override void OnHeaderGUI()
        {
            // Intentionally empty to avoid showing the script field
        }
    }
}
#endif