#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    public partial class SignalTrackerWindow : EditorWindow
    {
        [MenuItem("Tools/Neko Framework/Signal Tracker")]
        public static void ShowWindow()
        {
            GetWindow<SignalTrackerWindow>("Signal Tracker");
        }

        private enum Tab { SubscriptionMonitor, SignalLog, MemoryLeaks }
        private Tab _activeTab = Tab.SubscriptionMonitor;

        private string _searchFilter = string.Empty;

        // Pagination for each signal type
        private readonly Dictionary<Type, int> _signalPages = new();
        private const int SUBSCRIBERS_PER_PAGE = 8;

        // UI Colors
        private readonly Color HEADER_BG = new(0.2f, 0.2f, 0.2f, 0.4f);
        private readonly Color BORDER_COLOR = new(0.5f, 0.5f, 0.5f, 0.3f);

        // Table configuration
        private const float ROW_HEIGHT = 26f;
        private const float HEADER_HEIGHT = 28f;

        private static Color GetZebraStripeColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(0f, 0f, 0f, 0.06f);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Signal Tracker", "Track active signals across the project");
            minSize = new Vector2(900, 600);
        }

        private void OnGUI()
        {
            try
            {
                int newIndex = NekoLib.EditorTabBar.Draw((int)_activeTab, new[] { "Subscription Monitor", "Signal Log", "Memory Leaks" }, 24f);
                _activeTab = (Tab)newIndex;

                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                if (_activeTab == Tab.SubscriptionMonitor)
                {
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                        Repaint();
                }

                GUILayout.Space(10);
                GUILayout.FlexibleSpace();

                if (_activeTab != Tab.MemoryLeaks)
                {
                    GUILayout.Label("Search:", EditorStyles.miniLabel, GUILayout.Width(50));
                    _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));
                    GUILayout.Space(10);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                if (_activeTab == Tab.SubscriptionMonitor)
                    DrawSubscriptionMonitorView();
                else if (_activeTab == Tab.SignalLog)
                    DrawSignalLogView();
                else
                    DrawMemoryLeaksView();
            }
            catch (Exception e)
            {
                EditorGUILayout.HelpBox($"Error in SignalTracker: {e.Message}", MessageType.Error);
            }
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
    }
}
#endif
