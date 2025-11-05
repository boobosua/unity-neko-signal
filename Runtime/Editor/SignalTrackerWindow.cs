#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    public class SignalTrackerWindow : EditorWindow
    {
        [MenuItem("Window/Neko Indie/Signal Tracker")]
        public static void ShowWindow()
        {
            GetWindow<SignalTrackerWindow>("Signal Tracker");
        }

        private enum Tab { SubscriptionMonitor, SignalLog }
        private Tab _activeTab = Tab.SubscriptionMonitor;

        private string _searchFilter = string.Empty;

        // Pagination for each signal type
        private readonly Dictionary<Type, int> _signalPages = new();
        private const int SUBSCRIBERS_PER_PAGE = 8;

        // UI Colors - Clean and consistent with Timer Tracker
        // Use dynamic zebra color so it shows clearly in both Pro and Personal skins
        private readonly Color HEADER_BG = new(0.2f, 0.2f, 0.2f, 0.4f);         // Header background
        private readonly Color BORDER_COLOR = new(0.5f, 0.5f, 0.5f, 0.3f);      // Table borders

        // Table configuration
        private const float ROW_HEIGHT = 26f;   // Slightly taller for larger message text
        private const float HEADER_HEIGHT = 28f; // Compact header

        private static Color GetZebraStripeColor()
        {
            // Slightly lighter stripe on dark skin; slightly darker on light skin
            return EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(0f, 0f, 0f, 0.06f);
        }

        private void OnGUI()
        {
            try
            {
                // Standalone top tab bar
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                var newTabTop = (Tab)GUILayout.Toolbar((int)_activeTab, new[] { "Subscription Monitor", "Signal Log" }, EditorStyles.toolbarButton, GUILayout.Height(22));
                if (newTabTop != _activeTab)
                    _activeTab = newTabTop;
                EditorGUILayout.EndHorizontal();

                // Secondary toolbar for actions/search
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                // Only show Refresh in Subscription Monitor; Log tab doesn't need it
                if (_activeTab == Tab.SubscriptionMonitor)
                {
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        Repaint();
                    }
                }

                GUILayout.Space(10);

                // Removed "Show Empty" toggle per request

                GUILayout.FlexibleSpace();

                // Search bar on the right with margins
                GUILayout.Label("Search:", EditorStyles.miniLabel, GUILayout.Width(50));
                _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));
                GUILayout.Space(10); // Right margin

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                if (_activeTab == Tab.SubscriptionMonitor)
                {
                    DrawSubscriptionMonitorView();
                }
                else
                {
                    // Signal Log view
                    DrawSignalLogView();
                }
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Error in SignalTracker: {e.Message}", MessageType.Error);
            }
        }

        // -----------------------------
        // Subscription Monitor View (sidebar + table)
        // -----------------------------
        private int _monitorSelectedIndex = 0;
        private Vector2 _monitorLeftScroll, _monitorRightScroll;

        private void DrawSubscriptionMonitorView()
        {
            // Get signal data
            var channels = SignalBroadcaster.GetAllChannelInfo()?.ToList();
            if (channels == null)
            {
                EditorGUILayout.HelpBox("SignalBroadcaster not available", MessageType.Warning);
                return;
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                channels = channels.Where(c =>
                    c.SignalType != null &&
                    c.SignalType.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            // Always hide empty channels
            channels = channels.Where(c => c.SubscriberCount > 0)
                               .OrderBy(c => c.SignalType?.Name ?? "Unknown")
                               .ToList();

            if (channels.Count == 0)
            {
                EditorGUILayout.LabelField("No active signal channels found", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _monitorSelectedIndex = Mathf.Clamp(_monitorSelectedIndex, 0, channels.Count - 1);

            EditorGUILayout.BeginHorizontal();
            // Left sidebar: signals
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            EditorGUILayout.LabelField("Signals", EditorStyles.boldLabel);
            _monitorLeftScroll = EditorGUILayout.BeginScrollView(_monitorLeftScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                var t = ch.SignalType;
                var count = ch.SubscriberCount;
                if (GUILayout.Toggle(i == _monitorSelectedIndex, $"{t.Name} ({count})", "Button"))
                {
                    _monitorSelectedIndex = i;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Vertical divider
            GUILayout.Box(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(1));
            var divRect = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(new Rect(divRect.x, divRect.y + 2, 1, Mathf.Max(0, divRect.height - 4)), BORDER_COLOR);

            // Right panel: subscription table
            EditorGUILayout.BeginVertical();
            _monitorRightScroll = EditorGUILayout.BeginScrollView(_monitorRightScroll);
            var selected = channels[_monitorSelectedIndex];
            DrawSubscriberTable(selected);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Signal Tracker", "Track active signals across the project");
            minSize = new Vector2(900, 600);
        }

        // Removed legacy statistics and foldout-based monitor UI to simplify and optimize

        private void DrawSubscriberTable(SignalChannelInfo channelInfo)
        {
            var subscribers = SignalBroadcaster.GetSubscriberInfoByType(channelInfo.SignalType)?.ToList();
            if (subscribers == null || subscribers.Count == 0)
            {
                EditorGUILayout.LabelField("No subscribers found", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // Filter valid subscribers
            subscribers = subscribers.Where(s =>
                s != null &&
                s.IsValid &&
                (s.OwnerGameObject == null || s.OwnerGameObject) &&
                (s.TargetObject == null || s.TargetObject)
            ).ToList();

            // Sort by priority (desc), then by GameObject name
            subscribers = subscribers
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.OwnerGameObject != null ? s.OwnerGameObject.name : s.TargetName)
                .ToList();

            if (subscribers.Count == 0)
            {
                EditorGUILayout.LabelField("No active subscribers found", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var signalType = channelInfo.SignalType;

            // Initialize pagination for this signal type
            if (!_signalPages.ContainsKey(signalType))
            {
                _signalPages[signalType] = 0;
            }

            // Calculate pagination
            int totalPages = Mathf.CeilToInt((float)subscribers.Count / SUBSCRIBERS_PER_PAGE);
            _signalPages[signalType] = Mathf.Clamp(_signalPages[signalType], 0, Mathf.Max(0, totalPages - 1));

            int startIndex = _signalPages[signalType] * SUBSCRIBERS_PER_PAGE;
            int endIndex = Mathf.Min(startIndex + SUBSCRIBERS_PER_PAGE, subscribers.Count);

            // Pagination info (if needed)
            if (totalPages > 1)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Page {_signalPages[signalType] + 1} of {totalPages} ({startIndex + 1}-{endIndex} of {subscribers.Count})",
                    EditorStyles.miniLabel, GUILayout.Width(200));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            // Table header
            DrawSubscriberTableHeader();

            // Table rows with pagination
            for (int i = startIndex; i < endIndex; i++)
            {
                if (i < subscribers.Count && subscribers[i] != null)
                {
                    DrawSubscriberTableRow(subscribers[i], (i - startIndex) % 2 == 0);
                }
            }

            // Pagination controls (if needed)
            if (totalPages > 1)
            {
                EditorGUILayout.Space(5);
                DrawSubscriberPaginationControls(signalType, totalPages, startIndex + 1, endIndex, subscribers.Count);
            }
        }

        private void DrawSubscriberTableHeader()
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, HEADER_HEIGHT);

            // Header background
            EditorGUI.DrawRect(headerRect, HEADER_BG);

            // Header borders
            DrawTableBorders(headerRect);

            // Dynamic column widths - flexible and proportional (full width to match tabs)
            float totalWidth = headerRect.width; // Use full width
            float gameObjectWidth = totalWidth * 0.3f; // 30% GameObject (first)
            float componentWidth = totalWidth * 0.3f;  // 30% Component
            float methodWidth = totalWidth * 0.25f;    // 25% Method
            float priorityWidth = totalWidth * 0.15f;  // 15% Priority

            float x = headerRect.x;
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

            // Headers with vertical dividers
            DrawHeaderColumn(new Rect(x, headerRect.y, gameObjectWidth, headerRect.height), "GameObject", headerStyle);
            x += gameObjectWidth;
            DrawVerticalDivider(x, headerRect.y, headerRect.height);

            DrawHeaderColumn(new Rect(x, headerRect.y, componentWidth, headerRect.height), "Component", headerStyle);
            x += componentWidth;
            DrawVerticalDivider(x, headerRect.y, headerRect.height);

            DrawHeaderColumn(new Rect(x, headerRect.y, methodWidth, headerRect.height), "Method", headerStyle);
            x += methodWidth;
            DrawVerticalDivider(x, headerRect.y, headerRect.height);

            DrawHeaderColumn(new Rect(x, headerRect.y, priorityWidth, headerRect.height), "Priority", headerStyle);
        }

        private void DrawSubscriberTableRow(SignalSubscriberInfo subscriber, bool isEvenRow)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, ROW_HEIGHT);

            // Zebra stripe background
            if (isEvenRow)
            {
                EditorGUI.DrawRect(rowRect, GetZebraStripeColor());
            }

            // Row borders
            DrawTableBorders(rowRect);

            // Dynamic column widths (same as header)
            float totalWidth = rowRect.width;
            float gameObjectWidth = totalWidth * 0.3f;
            float componentWidth = totalWidth * 0.3f;
            float methodWidth = totalWidth * 0.25f;
            float priorityWidth = totalWidth * 0.15f;

            float x = rowRect.x;

            try
            {
                // Use default text color (no special coloring unless it's a link)
                Color defaultTextColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
                Color dimmedTextColor = Color.Lerp(defaultTextColor, Color.gray, 0.2f);

                // GameObject first (clickable)
                DrawGameObjectColumn(new Rect(x, rowRect.y, gameObjectWidth, rowRect.height), subscriber);
                x += gameObjectWidth;
                DrawVerticalDivider(x, rowRect.y, rowRect.height);

                // Component name (type)
                var componentName = GetComponentDisplayName(subscriber);
                if (componentName.Length > 30) componentName = componentName.Substring(0, 27) + "...";
                DrawSubscriberColumn(new Rect(x, rowRect.y, componentWidth, rowRect.height), componentName, dimmedTextColor);
                x += componentWidth;
                DrawVerticalDivider(x, rowRect.y, rowRect.height);

                // Method name
                var methodDisplayName = GetMethodDisplayName(subscriber.MethodName);
                var methodRect = new Rect(x, rowRect.y, methodWidth, rowRect.height);
                var linkCenter = new GUIStyle(EditorStyles.linkLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
                if (GUI.Button(new Rect(methodRect.x + 8, methodRect.y, methodRect.width - 16, methodRect.height), methodDisplayName, linkCenter))
                {
                    TryOpenSubscriberMethod(subscriber);
                }
                x += methodWidth;
                DrawVerticalDivider(x, rowRect.y, rowRect.height);

                // Priority
                DrawSubscriberColumn(new Rect(x, rowRect.y, priorityWidth, rowRect.height), subscriber.Priority.ToString(), dimmedTextColor);

            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SignalTracker] Error drawing subscriber row: {e}");
            }
        }

        private string GetComponentDisplayName(SignalSubscriberInfo subscriber)
        {
            if (subscriber?.TargetObject is MonoBehaviour mb && mb)
                return mb.GetType().Name;
            if (subscriber?.TargetObject != null)
                return subscriber.TargetObject.GetType().Name;
            if (subscriber?.OwnerGameObject != null)
                return subscriber.OwnerGameObject.GetComponent<MonoBehaviour>()?.GetType().Name ?? "(GameObject)";
            return subscriber?.TargetName ?? "N/A";
        }

        private void DrawSubscriberColumn(Rect rect, string text, Color textColor)
        {
            var centeredRect = new Rect(rect.x + 8, rect.y, rect.width - 16, rect.height);

            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };
            style.normal.textColor = textColor;

            GUI.Label(centeredRect, text, style);
        }

        private void DrawGameObjectColumn(Rect rect, SignalSubscriberInfo subscriber)
        {
            var centeredRect = new Rect(rect.x + 8, rect.y, rect.width - 16, rect.height);

            if (subscriber.OwnerGameObject != null)
            {
                var gameObjectName = subscriber.OwnerGameObject.name;
                if (gameObjectName.Length > 25)
                    gameObjectName = gameObjectName.Substring(0, 22) + "...";

                // Use link label style for clickable GameObjects
                var linkStyle = new GUIStyle(EditorStyles.linkLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10
                };

                if (GUI.Button(centeredRect, gameObjectName, linkStyle))
                {
                    EditorGUIUtility.PingObject(subscriber.OwnerGameObject);
                    Selection.activeGameObject = subscriber.OwnerGameObject;
                }
            }
            else if (subscriber.TargetObject != null)
            {
                var targetObjectName = subscriber.TargetObject.name;
                if (targetObjectName.Length > 25)
                    targetObjectName = targetObjectName.Substring(0, 22) + "...";

                var linkStyle = new GUIStyle(EditorStyles.linkLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10
                };

                if (GUI.Button(centeredRect, targetObjectName, linkStyle))
                {
                    EditorGUIUtility.PingObject(subscriber.TargetObject);
                    Selection.activeObject = subscriber.TargetObject;
                }
            }
            else
            {
                Color defaultTextColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
                Color dimmedTextColor = Color.Lerp(defaultTextColor, Color.gray, 0.3f);
                DrawSubscriberColumn(rect, "N/A", dimmedTextColor);
            }
        }

        private void DrawSubscriberPaginationControls(Type signalType, int totalPages, int startItem, int endItem, int totalItems)
        {
            EditorGUILayout.BeginHorizontal();

            // Previous button
            GUI.enabled = _signalPages[signalType] > 0;
            if (GUILayout.Button("◀ Previous", GUILayout.Width(70), GUILayout.Height(20)))
            {
                _signalPages[signalType]--;
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            // Page info
            EditorGUILayout.LabelField($"{startItem}-{endItem} of {totalItems}",
                EditorStyles.centeredGreyMiniLabel, GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            // Next button
            GUI.enabled = _signalPages[signalType] < totalPages - 1;
            if (GUILayout.Button("Next ▶", GUILayout.Width(70), GUILayout.Height(20)))
            {
                _signalPages[signalType]++;
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private string GetMethodDisplayName(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
                return "Unknown Method";

            // Handle anonymous methods (lambdas)
            if (methodName.Contains("<") && methodName.Contains(">"))
            {
                if (methodName.Contains("<OnEnable>"))
                    return "Lambda in OnEnable";
                if (methodName.Contains("<Start>"))
                    return "Lambda in Start";
                if (methodName.Contains("<Awake>"))
                    return "Lambda in Awake";
                if (methodName.Contains("b__"))
                    return "Lambda Expression";

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
            // Full border: top, bottom, and vertical separators are drawn elsewhere
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), BORDER_COLOR); // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), BORDER_COLOR); // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), BORDER_COLOR); // Left
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), BORDER_COLOR); // Right
        }

        private void DrawHeaderColumn(Rect rect, string text, GUIStyle style)
        {
            GUI.Label(rect, text, style);
        }

        private void DrawVerticalDivider(float x, float y, float height)
        {
            EditorGUI.DrawRect(new Rect(x - 1, y, 1, height), BORDER_COLOR);
        }

        // -----------------------------
        // Signal Log View
        // -----------------------------
        private int _selectedLogSignalIndex = 0;
        private Vector2 _logLeftScroll, _logRightScroll;

        private void DrawSignalLogView()
        {
#if UNITY_EDITOR
            var logs = SignalLogStore.GetLogs();
            var types = SignalLogStore.GetSignalTypes().ToList();

            EditorGUILayout.BeginHorizontal();
            // Left sidebar: signals
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            EditorGUILayout.LabelField("Signals", EditorStyles.boldLabel);
            _logLeftScroll = EditorGUILayout.BeginScrollView(_logLeftScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < types.Count; i++)
            {
                var t = types[i];
                var count = logs.Count(l => l.SignalType == t);
                var style = (i == _selectedLogSignalIndex) ? EditorStyles.whiteLabel : EditorStyles.label;
                if (GUILayout.Toggle(i == _selectedLogSignalIndex, $"{t.Name} ({count})", "Button"))
                    _selectedLogSignalIndex = i;
            }
            EditorGUILayout.EndScrollView();

            // Controls
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", GUILayout.Width(100)))
                SignalLogStore.Clear();
            SignalLogStore.Enabled = GUILayout.Toggle(SignalLogStore.Enabled, "Capture", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Vertical divider between sidebar and log area
            GUILayout.Box(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(1));
            var divRect = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(new Rect(divRect.x, divRect.y + 2, 1, Mathf.Max(0, divRect.height - 4)), BORDER_COLOR);

            // Right panel: log entries
            EditorGUILayout.BeginVertical();
            _logRightScroll = EditorGUILayout.BeginScrollView(_logRightScroll);

            if (types.Count == 0)
            {
                EditorGUILayout.HelpBox("No log entries yet. Trigger a publish to see logs.", MessageType.Info);
            }
            else
            {
                var selType = types[Mathf.Clamp(_selectedLogSignalIndex, 0, types.Count - 1)];
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
#endif
        }

#if UNITY_EDITOR
        private void DrawLogEntry(SignalPublishLog e)
        {
            // Use plain container; the message itself will be a single rounded rectangle
            EditorGUILayout.BeginVertical(GUIStyle.none);

            // No header row; single-line message below

            // Invocation table header (compact)
            // Single row per publish (no header, just message row)
            Rect rowRect = EditorGUILayout.GetControlRect(false, ROW_HEIGHT);
            // Draw one rounded rectangle background for the message and brighten inside to make foldout pop more
            GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);
            var innerRect = new Rect(rowRect.x + 3, rowRect.y + 3, rowRect.width - 6, rowRect.height - 6);
            var bg = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.06f);
            EditorGUI.DrawRect(innerRect, bg);

            // Compose message with clickable parts
            var msgRect = new Rect(rowRect.x + 10, rowRect.y, rowRect.width - 20, rowRect.height);
            DrawPublishMessage(msgRect, e);

            // Clicking empty area of the message toggles payload foldout (handled within DrawPublishMessage as well).

            // Payload preview under the message
            if (e.PayloadExpanded && e.PayloadFields != null && e.PayloadFields.Count > 0)
            {
                var inner = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 6, 6) };
                EditorGUILayout.BeginVertical(inner);
                foreach (var f in e.PayloadFields)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(f.Name, GUILayout.Width(160));
                    EditorGUILayout.LabelField(f.Value, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            // Footer profiler summary
            // No footer/frame line; keep it to a single visible line per event unless payload is expanded

            EditorGUILayout.EndVertical();
        }
#endif

#if UNITY_EDITOR
        private void DrawPublishMessage(Rect rect, SignalPublishLog e)
        {
            // Styles
            var baseStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            // Two link styles (component and gameobject) derived from label to keep baseline identical
            var compStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            var goStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            // Distinct, readable colors per skin
            if (EditorGUIUtility.isProSkin)
            {
                compStyle.normal.textColor = new Color(0.55f, 0.8f, 1f, 1f); // light blue
                compStyle.hover.textColor = new Color(0.65f, 0.9f, 1f, 1f);
                compStyle.active.textColor = new Color(0.8f, 0.95f, 1f, 1f);
                goStyle.normal.textColor = new Color(0.6f, 0.95f, 0.9f, 1f); // aqua-green
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

            string comp = string.IsNullOrEmpty(e.PublisherComponentName) ? "<Component>" : e.PublisherComponentName;
            string go = string.IsNullOrEmpty(e.PublisherGameObjectName) ? "<GameObject>" : e.PublisherGameObjectName;
            string atTxt = " at ";
            // Shorter time (rounded to seconds)
            string timeTxt = e.Time.ToString("HH:mm:ss");

            // Measure segments
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
            // Event name omitted per request
            var atRect = new Rect(x, rect.y, atSize.x, rect.height); x += atSize.x;
            // Time badge rect with padding, vertically centered using full badge height
            float padV = 4f; float padW = 6f;
            float badgeW = timeSize.x + padW * 2f;
            float badgeH = timeSize.y + padV;
            var timeRect = new Rect(x, rect.y + (rect.height - badgeH) * 0.5f, badgeW, badgeH); x += timeRect.width + 6f;
            // Optional filters indicator
            string filterLabel = null;
            if (e.Filters != null && e.Filters.Count > 0)
            {
                filterLabel = " filter by " + string.Join(", ", e.Filters);
            }
            Vector2 filterSize = Vector2.zero;
            if (!string.IsNullOrEmpty(filterLabel)) filterSize = filterStyle.CalcSize(new GUIContent(filterLabel));
            var filterRect = new Rect(x, rect.y, Mathf.Min(filterSize.x, rect.xMax - x), rect.height);

            // Draw labels/buttons
            if (GUI.Button(compRect, comp, compStyle))
            {
                TryOpenPublisherScript(e);
            }
            GUI.Label(inRect, " in ", baseStyle);
            if (GUI.Button(goRect, go, goStyle))
            {
                if (e.PublisherObject)
                {
                    if (e.PublisherObject is MonoBehaviour mb && mb)
                    {
                        EditorGUIUtility.PingObject(mb.gameObject);
                        Selection.activeGameObject = mb.gameObject;
                    }
                    else if (e.PublisherObject is GameObject g && g)
                    {
                        EditorGUIUtility.PingObject(g);
                        Selection.activeGameObject = g;
                    }
                }
            }
            GUI.Label(raisedRect, " raised ", baseStyle);
            GUI.Label(atRect, atTxt, baseStyle);
            // Time badge with rounded rectangle
            var prev = GUI.color;
            GUI.color = EditorGUIUtility.isProSkin ? new Color(0.15f, 0.85f, 0.95f, 0.9f) : new Color(0f, 0.65f, 0.7f, 0.9f);
            GUI.Box(timeRect, timeTxt, timeBadgeStyle);
            GUI.color = prev;
            if (!string.IsNullOrEmpty(filterLabel))
            {
                GUI.Label(filterRect, filterLabel, filterStyle);
            }

            // Toggle payload when clicking message area excluding the link rectangles
            var evt = Event.current;
            if (evt.type == EventType.MouseUp && rect.Contains(evt.mousePosition))
            {
                if (!compRect.Contains(evt.mousePosition) && !goRect.Contains(evt.mousePosition) && !timeRect.Contains(evt.mousePosition) && !filterRect.Contains(evt.mousePosition))
                {
                    e.PayloadExpanded = !e.PayloadExpanded;
                    evt.Use();
                }
            }
        }

        private void TryOpenPublisherScript(SignalPublishLog e)
        {
            if (string.IsNullOrEmpty(e.ScriptFilePath) || e.ScriptLine <= 0)
            {
                // Fallback: open component script if we have an instance
                if (e.PublisherObject is MonoBehaviour mb && mb)
                {
                    var ms = MonoScript.FromMonoBehaviour(mb);
                    if (ms) AssetDatabase.OpenAsset(ms);
                }
                return;
            }

            var rel = ToAssetsRelativePath(e.ScriptFilePath);
            MonoScript script = null;
            if (!string.IsNullOrEmpty(rel))
            {
                script = AssetDatabase.LoadAssetAtPath<MonoScript>(rel);
            }
            if (script)
            {
                AssetDatabase.OpenAsset(script, e.ScriptLine);
            }
            else
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(e.ScriptFilePath, e.ScriptLine);
            }
        }

        private static string ToAssetsRelativePath(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            file = file.Replace('\\', '/');
            int idx = file.IndexOf("Assets/");
            if (idx >= 0) return file.Substring(idx);
            return null;
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
                else if (subscriber?.OwnerGameObject != null)
                {
                    var comp = subscriber.OwnerGameObject.GetComponent<MonoBehaviour>();
                    if (comp) ms = MonoScript.FromMonoBehaviour(comp);
                }

                if (!ms)
                {
                    return;
                }

                var path = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(path))
                {
                    AssetDatabase.OpenAsset(ms);
                    return;
                }

                var lines = File.ReadAllLines(path);
                var pattern = @"\b" + Regex.Escape(subscriber.MethodName) + @"\s*\("; // methodName(
                var rx = new Regex(pattern);
                int lineNumber = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (rx.IsMatch(lines[i]))
                    {
                        lineNumber = i + 1; // Unity is 1-based
                        break;
                    }
                }

                if (lineNumber > 0)
                {
                    AssetDatabase.OpenAsset(ms, lineNumber);
                }
                else
                {
                    AssetDatabase.OpenAsset(ms);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SignalTracker] Failed to open method: {ex.Message}");
            }
        }
#endif
    }
}
#endif
