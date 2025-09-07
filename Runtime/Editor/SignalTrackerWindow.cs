#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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

        private Vector2 _scrollPosition;
        private string _searchFilter = string.Empty;
        private readonly Dictionary<Type, bool> _foldoutStates = new();
        private bool _showEmptyChannels = false;

        // Pagination for each signal type
        private readonly Dictionary<Type, int> _signalPages = new();
        private const int SUBSCRIBERS_PER_PAGE = 8;

        // UI Colors - Clean and consistent with Timer Tracker
        private readonly Color ZEBRA_STRIPE = new(0f, 0f, 0f, 0.05f);           // Row striping
        private readonly Color HEADER_BG = new(0.2f, 0.2f, 0.2f, 0.4f);         // Header background
        private readonly Color BORDER_COLOR = new(0.5f, 0.5f, 0.5f, 0.3f);      // Table borders

        // Table configuration
        private const float ROW_HEIGHT = 38f;
        private const float HEADER_HEIGHT = 42f;

        private void OnGUI()
        {
            try
            {
                // Simple clean toolbar
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    Repaint();
                }

                GUILayout.Space(10);

                _showEmptyChannels = GUILayout.Toggle(_showEmptyChannels, "Show Empty", EditorStyles.toolbarButton, GUILayout.Width(80));

                GUILayout.FlexibleSpace();

                // Search bar on the right with margins
                GUILayout.Label("Search:", EditorStyles.miniLabel, GUILayout.Width(50));
                _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));
                GUILayout.Space(10); // Right margin

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

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

                // Filter based on show empty channels toggle
                if (!_showEmptyChannels)
                {
                    channels = channels.Where(c => c.SubscriberCount > 0).ToList();
                }

                if (channels.Count == 0)
                {
                    EditorGUILayout.LabelField(
                        _showEmptyChannels ? "No signal channels found" : "No active signal channels found",
                        EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                // Scroll view for signals
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                // Draw each signal as a foldout
                foreach (var channelInfo in channels.OrderBy(c => c.SignalType?.Name ?? "Unknown"))
                {
                    if (channelInfo.SignalType != null)
                    {
                        DrawSignalFoldout(channelInfo);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Error in SignalTracker: {e.Message}", MessageType.Error);
            }
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Signal Tracker", "Track active signals across the project");
            minSize = new Vector2(900, 600);
        }

        private void DrawStatistics(List<SignalChannelInfo> channels)
        {
            var totalSubscribers = channels.Sum(c => c.SubscriberCount);
            var activeChannels = channels.Count(c => c.SubscriberCount > 0);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"Channels: {channels.Count} | Active: {activeChannels} | Subscribers: {totalSubscribers}",
                EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Show empty channels toggle
            _showEmptyChannels = GUILayout.Toggle(_showEmptyChannels, "Show Empty", GUILayout.Width(80));

            GUILayout.Space(10);

            // Search field
            GUILayout.Label("Search:", GUILayout.Width(50));
            var newSearchFilter = GUILayout.TextField(_searchFilter, GUILayout.Width(200));
            if (newSearchFilter != _searchFilter)
            {
                _searchFilter = newSearchFilter;
            }

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _searchFilter = string.Empty;
                GUI.FocusControl(null);
            }

            GUILayout.Space(10);

            // Cleanup button
            if (GUILayout.Button("Cleanup", GUILayout.Width(70)))
            {
                try
                {
                    SignalBroadcaster.CleanupStaleReferences();
                    Debug.Log("[SignalTracker] Cleaned up stale signal references");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SignalTracker] Error cleaning up: {e.Message}");
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSignalFoldout(SignalChannelInfo channelInfo)
        {
            var signalType = channelInfo.SignalType;
            var subscriberCount = channelInfo.SubscriberCount;

            // Initialize foldout state
            if (!_foldoutStates.ContainsKey(signalType))
            {
                _foldoutStates[signalType] = subscriberCount > 0;
            }

            EditorGUILayout.Space(10); // Increased space before each signal section

            // Create a bigger box for the signal
            var boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 8, 8) // Increased padding for bigger box
            };
            EditorGUILayout.BeginVertical(boxStyle);

            // Header with foldout - simplified, no badge on the right
            EditorGUILayout.BeginHorizontal();

            // Foldout arrow and signal name with subscriber count in the title
            _foldoutStates[signalType] = EditorGUILayout.Foldout(
                _foldoutStates[signalType],
                $"{signalType.Name} ({subscriberCount} subscribers)",
                true);

            EditorGUILayout.EndHorizontal();

            // Show subscriber table if expanded and has subscribers
            if (_foldoutStates[signalType] && subscriberCount > 0)
            {
                EditorGUILayout.Space(10); // Increased space before table
                EditorGUI.indentLevel++;
                DrawSubscriberTable(channelInfo);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(8); // Space after table
            }

            EditorGUILayout.EndVertical();
        }

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
            float methodWidth = totalWidth * 0.35f;    // 35% for Method
            float targetWidth = totalWidth * 0.35f;    // 35% for Target
            float gameObjectWidth = totalWidth * 0.3f; // 30% for GameObject

            float x = headerRect.x;
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

            // Headers with vertical dividers
            DrawHeaderColumn(new Rect(x, headerRect.y, methodWidth, headerRect.height), "Method", headerStyle);
            x += methodWidth;
            DrawVerticalDivider(x, headerRect.y, headerRect.height);

            DrawHeaderColumn(new Rect(x, headerRect.y, targetWidth, headerRect.height), "Target", headerStyle);
            x += targetWidth;
            DrawVerticalDivider(x, headerRect.y, headerRect.height);

            DrawHeaderColumn(new Rect(x, headerRect.y, gameObjectWidth, headerRect.height), "GameObject", headerStyle);
        }

        private void DrawSubscriberTableRow(SignalSubscriberInfo subscriber, bool isEvenRow)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, ROW_HEIGHT);

            // Zebra stripe background
            if (isEvenRow)
            {
                EditorGUI.DrawRect(rowRect, ZEBRA_STRIPE);
            }

            // Row borders
            DrawTableBorders(rowRect);

            // Dynamic column widths (same as header)
            float totalWidth = rowRect.width;
            float methodWidth = totalWidth * 0.35f;
            float targetWidth = totalWidth * 0.35f;
            float gameObjectWidth = totalWidth * 0.3f;

            float x = rowRect.x;

            try
            {
                // Use default text color (no special coloring unless it's a link)
                Color defaultTextColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
                Color dimmedTextColor = Color.Lerp(defaultTextColor, Color.gray, 0.3f);

                // Method name
                var methodDisplayName = GetMethodDisplayName(subscriber.MethodName);
                DrawSubscriberColumn(new Rect(x, rowRect.y, methodWidth, rowRect.height), methodDisplayName, dimmedTextColor);
                x += methodWidth;
                DrawVerticalDivider(x, rowRect.y, rowRect.height);

                // Target name
                var targetText = subscriber.TargetName;
                if (targetText.Length > 30)
                    targetText = targetText.Substring(0, 27) + "...";
                DrawSubscriberColumn(new Rect(x, rowRect.y, targetWidth, rowRect.height), targetText, dimmedTextColor);
                x += targetWidth;
                DrawVerticalDivider(x, rowRect.y, rowRect.height);

                // GameObject (clickable if valid) - this gets special link color
                DrawGameObjectColumn(new Rect(x, rowRect.y, gameObjectWidth, rowRect.height), subscriber);

            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SignalTracker] Error drawing subscriber row: {e}");
            }
        }

        private void DrawSubscriberColumn(Rect rect, string text, Color textColor)
        {
            var centeredRect = new Rect(rect.x + 8, rect.y, rect.width - 16, rect.height);

            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
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
                    fontSize = 11
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
                    fontSize = 11
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
            // Bottom border
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), BORDER_COLOR);
        }

        private void DrawHeaderColumn(Rect rect, string text, GUIStyle style)
        {
            GUI.Label(rect, text, style);
        }

        private void DrawVerticalDivider(float x, float y, float height)
        {
            EditorGUI.DrawRect(new Rect(x - 1, y, 1, height), BORDER_COLOR);
        }
    }
}
#endif
