#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    internal partial class SignalTrackerWindow : EditorWindow
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

        // UI layout constants
        private const float ROW_HEIGHT = 26f;
        private const float HEADER_HEIGHT = 28f;
        private const int NAME_TRUNCATE_COMPONENT = 30;
        private const int NAME_TRUNCATE_GAMEOBJECT = 25;

        // Cached colors (skin-independent)
        private readonly Color HEADER_BG = new(0.2f, 0.2f, 0.2f, 0.4f);
        private readonly Color BORDER_COLOR = new(0.5f, 0.5f, 0.5f, 0.3f);

        // --- Cached GUIStyles ---
        private bool _stylesInitialized;
        private bool _cachedProSkin;

        // Shared sidebar
        private GUIStyle _sidebarLabelStyle;
        private GUIStyle _sidebarCountStyle;
        private Color _selectionColor;

        // Monitor table
        private GUIStyle _tableHeaderStyle;
        private GUIStyle _tableCellStyle;
        private GUIStyle _tableCellDimStyle;
        private GUIStyle _tableCellNaStyle;
        private GUIStyle _tableLinkCenterStyle;
        private Color _defaultTextColor;
        private Color _dimmedTextColor;
        private Color _naTextColor;

        // Log payload box
        private GUIStyle _logPayloadBoxStyle;

        // Signal Log emit message
        private GUIStyle _logBaseStyle;
        private GUIStyle _logCompStyle;
        private GUIStyle _logGoStyle;
        private GUIStyle _logTimeStyle;
        private GUIStyle _logTimeBadgeStyle;
        private GUIStyle _logFilterStyle;
        private Color _logTimeBadgeColor;

        // Memory Leaks
        private GUIStyle _leaksRowStyle;
        private Color _leaksRowBgColor;
        private Color _zebraStripeColor;

        private void EnsureStyles()
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            if (_stylesInitialized && _cachedProSkin == proSkin) return;
            _cachedProSkin = proSkin;
            _stylesInitialized = true;

            _defaultTextColor = proSkin ? Color.white : Color.black;
            _dimmedTextColor = Color.Lerp(_defaultTextColor, Color.gray, 0.2f);
            _naTextColor = Color.Lerp(_defaultTextColor, Color.gray, 0.3f);
            _selectionColor = proSkin
                ? new Color(0.25f, 0.45f, 0.65f, 0.35f)
                : new Color(0.6f, 0.75f, 0.95f, 0.6f);
            _zebraStripeColor = proSkin
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(0f, 0f, 0f, 0.06f);

            // Sidebar
            _sidebarLabelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            _sidebarCountStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };

            // Table
            _tableHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _tableHeaderStyle.normal.textColor = _defaultTextColor;

            _tableCellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            _tableCellStyle.normal.textColor = _defaultTextColor;

            _tableCellDimStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            _tableCellDimStyle.normal.textColor = _dimmedTextColor;

            _tableCellNaStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            _tableCellNaStyle.normal.textColor = _naTextColor;

            _tableLinkCenterStyle = new GUIStyle(EditorStyles.linkLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };

            _logPayloadBoxStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 6, 6) };

            // Log
            _logBaseStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            _logCompStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            _logGoStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };

            if (proSkin)
            {
                _logCompStyle.normal.textColor = new Color(0.55f, 0.8f, 1f, 1f);
                _logCompStyle.hover.textColor = new Color(0.65f, 0.9f, 1f, 1f);
                _logCompStyle.active.textColor = new Color(0.8f, 0.95f, 1f, 1f);
                _logGoStyle.normal.textColor = new Color(0.6f, 0.95f, 0.9f, 1f);
                _logGoStyle.hover.textColor = new Color(0.7f, 1f, 0.95f, 1f);
                _logGoStyle.active.textColor = new Color(0.85f, 1f, 0.98f, 1f);
            }
            else
            {
                _logCompStyle.normal.textColor = new Color(0.05f, 0.35f, 0.75f, 1f);
                _logCompStyle.hover.textColor = new Color(0.1f, 0.45f, 0.9f, 1f);
                _logCompStyle.active.textColor = new Color(0.15f, 0.5f, 1f, 1f);
                _logGoStyle.normal.textColor = new Color(0f, 0.45f, 0.4f, 1f);
                _logGoStyle.hover.textColor = new Color(0f, 0.6f, 0.55f, 1f);
                _logGoStyle.active.textColor = new Color(0f, 0.7f, 0.65f, 1f);
            }

            _logTimeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            _logTimeBadgeStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 2, 2) };
            _logFilterStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 10, fontStyle = FontStyle.Italic };
            _logFilterStyle.normal.textColor = new Color(1f, 0.9f, 0.4f, 1f);
            _logTimeBadgeColor = proSkin
                ? new Color(0.15f, 0.85f, 0.95f, 0.9f)
                : new Color(0f, 0.65f, 0.7f, 0.9f);

            // Leaks
            _leaksRowStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 11 };
            _leaksRowBgColor = proSkin
                ? new Color(1f, 0.6f, 0.1f, 0.08f)
                : new Color(1f, 0.6f, 0.1f, 0.12f);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Signal Tracker", "Track active signals across the project");
            minSize = new Vector2(900, 600);
        }

        private void OnGUI()
        {
            EnsureStyles();
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

        private void DrawTableBorders(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), BORDER_COLOR);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), BORDER_COLOR);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), BORDER_COLOR);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), BORDER_COLOR);
        }

        private void DrawHeaderColumn(Rect rect, string text)
        {
            GUI.Label(rect, text, _tableHeaderStyle);
        }

        private void DrawVerticalDivider(float x, float y, float height)
        {
            EditorGUI.DrawRect(new Rect(x - 1, y, 1, height), BORDER_COLOR);
        }
    }
}
#endif
