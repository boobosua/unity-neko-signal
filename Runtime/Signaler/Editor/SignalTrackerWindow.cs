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
        [MenuItem("Tools/Neko Indie/Signal Tracker")]
        public static void ShowWindow()
        {
            GetWindow<SignalTrackerWindow>("Signal Tracker");
        }

        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private float _refreshRate = 1f;
        private double _lastRefreshTime;
        private readonly Dictionary<Type, bool> _foldoutStates = new();
        private string _searchFilter = string.Empty;
        private bool _showEmptyChannels = false;

        // Cached textures for performance
        private static Dictionary<Color, Texture2D> _textureCache = new Dictionary<Color, Texture2D>();

        private void OnEnable()
        {
            titleContent = new GUIContent("Signal Tracker", "Track active signals across the project");
            _lastRefreshTime = EditorApplication.timeSinceStartup;

            // Set minimum window size for better table display
            minSize = new Vector2(600, 300);

            // Subscribe to play mode state changes for proper cleanup
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            // Unsubscribe from play mode state changes
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            // Clean up cached textures
            foreach (var texture in _textureCache.Values)
            {
                if (texture != null)
                    DestroyImmediate(texture);
            }
            _textureCache.Clear();
        }

        private void Update()
        {
            if (_autoRefresh && EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshRate)
            {
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Force refresh when play mode changes to reflect signal cleanup
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Clear foldout states immediately
                _foldoutStates.Clear();

                // Schedule a delayed repaint to ensure SignalBroadcaster cleanup happens first
                EditorApplication.delayCall += () =>
                {
                    if (this != null) // Check if window still exists
                        Repaint();
                };
            }
            else if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                // Clear foldout states and force immediate repaint when mode changes
                _foldoutStates.Clear();
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSignalList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Auto-refresh toggle
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(90));

            if (!_autoRefresh)
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    Repaint();
                }
            }

            GUILayout.Space(10);

            // Refresh rate slider
            GUILayout.Label("Rate:", GUILayout.Width(35));
            _refreshRate = GUILayout.HorizontalSlider(_refreshRate, 0.1f, 5f, GUILayout.Width(100));
            GUILayout.Label($"{_refreshRate:F1}s", GUILayout.Width(30));

            GUILayout.Space(10);

            // Show empty channels toggle
            _showEmptyChannels = GUILayout.Toggle(_showEmptyChannels, "Show Empty", EditorStyles.toolbarButton, GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            // Cleanup Stale References button
            if (GUILayout.Button("Cleanup", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                SignalBroadcaster.CleanupStaleReferences();
                Debug.Log("[SignalTracker] Cleaned up stale signal references");
            }

            // Clear All Signals button
            if (GUILayout.Button("Clear All Signals", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("Clear All Signals",
                    "Are you sure you want to clear all signal subscriptions? This will unsubscribe all active signals.",
                    "Yes, Clear All", "Cancel"))
                {
                    SignalBroadcaster.UnsubscribeAll();
                    Debug.Log("[SignalTracker] All signal channels cleared manually");
                }
            }

            GUILayout.Space(10);

            // Search field
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarTextField, GUILayout.Width(200));

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _searchFilter = string.Empty;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSignalList()
        {
            var channels = SignalBroadcaster.GetAllChannelInfo().ToList();

            // Apply filters
            if (!_showEmptyChannels)
            {
                channels = channels.Where(c => c.SubscriberCount > 0).ToList();
            }

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                channels = channels.Where(c =>
                    c.SignalType.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            if (channels.Count == 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    _showEmptyChannels ? "No signal channels found." : "No active signal channels found.",
                    MessageType.Info);
                return;
            }

            // Summary
            var totalSubscribers = channels.Sum(c => c.SubscriberCount);
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Play mode indicator
            var playModeIcon = EditorApplication.isPlaying ? "▶️" : "⏹️";
            var playModeText = EditorApplication.isPlaying ? "Playing" : "Stopped";
            var playModeColor = EditorApplication.isPlaying ? Color.green : Color.gray;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"📊 Summary: {channels.Count} channels, {totalSubscribers} total subscribers", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            var originalColor = GUI.color;
            GUI.color = playModeColor;
            EditorGUILayout.LabelField($"{playModeIcon} {playModeText}", EditorStyles.boldLabel, GUILayout.Width(80));
            GUI.color = originalColor;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var channelInfo in channels.OrderBy(c => c.SignalType.Name))
            {
                DrawSignalChannel(channelInfo);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSignalChannel(SignalChannelInfo channelInfo)
        {
            var signalType = channelInfo.SignalType;
            var subscriberCount = channelInfo.SubscriberCount;

            // Get or create foldout state
            if (!_foldoutStates.ContainsKey(signalType))
            {
                _foldoutStates[signalType] = subscriberCount > 0;
            }

            // Create a rounded box style for the signal channel
            var channelBoxStyle = new GUIStyle(EditorStyles.helpBox);
            channelBoxStyle.padding = new RectOffset(8, 8, 8, 8);
            channelBoxStyle.margin = new RectOffset(4, 4, 2, 6);

            EditorGUILayout.BeginVertical(channelBoxStyle);

            // Header with improved styling
            var headerBgColor = subscriberCount > 0
                ? new Color(0.2f, 0.4f, 0.6f, 0.3f)  // Blue tint for active signals
                : new Color(0.4f, 0.4f, 0.4f, 0.2f); // Gray for inactive

            if (EditorGUIUtility.isProSkin)
            {
                headerBgColor = subscriberCount > 0
                    ? new Color(0.1f, 0.3f, 0.5f, 0.4f)
                    : new Color(0.3f, 0.3f, 0.3f, 0.3f);
            }

            var headerStyle = new GUIStyle();
            headerStyle.normal.background = MakeTexture(1, 1, headerBgColor);
            headerStyle.padding = new RectOffset(6, 6, 4, 4);
            headerStyle.margin = new RectOffset(0, 0, 0, 2);

            EditorGUILayout.BeginHorizontal(headerStyle);

            var foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            _foldoutStates[signalType] = EditorGUILayout.Foldout(
                _foldoutStates[signalType],
                $"🔗 {signalType.Name}",
                true,
                foldoutStyle);

            GUILayout.FlexibleSpace();

            // Enhanced subscriber count badge
            var badgeColor = subscriberCount > 0 ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
            var badgeTextColor = subscriberCount > 0 ? Color.white : Color.black;

            var badgeStyle = new GUIStyle(EditorStyles.miniButton);
            badgeStyle.normal.textColor = badgeTextColor;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.fontSize = 10;

            var originalBgColor = GUI.backgroundColor;
            GUI.backgroundColor = badgeColor;
            GUILayout.Label($"{subscriberCount}", badgeStyle, GUILayout.Width(35), GUILayout.Height(18));
            GUI.backgroundColor = originalBgColor;

            EditorGUILayout.EndHorizontal();

            // Show subscribers if expanded and has subscribers
            if (_foldoutStates[signalType] && subscriberCount > 0)
            {
                EditorGUILayout.Space(4);
                DrawSubscribers(channelInfo);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSubscribers(SignalChannelInfo channelInfo)
        {
            var subscribers = SignalBroadcaster.GetSubscriberInfoByType(channelInfo.SignalType).ToList();

            // Filter out truly invalid subscribers
            subscribers = subscribers.Where(s =>
                s.IsValid &&
                (s.OwnerGameObject == null || s.OwnerGameObject) && // Unity null check for destroyed objects
                (s.TargetObject == null || s.TargetObject) // Unity null check for destroyed objects
            ).ToList();

            if (subscribers.Count == 0)
            {
                EditorGUILayout.LabelField("No active subscribers found", EditorStyles.miniLabel);
                return;
            }

            // Create a box style for the table
            var tableBoxStyle = new GUIStyle(GUI.skin.box);
            tableBoxStyle.padding = new RectOffset(0, 0, 0, 0);
            tableBoxStyle.margin = new RectOffset(0, 0, 2, 2);

            EditorGUILayout.BeginVertical(tableBoxStyle);

            // Table header with background
            var headerStyle = new GUIStyle();
            headerStyle.normal.background = MakeTexture(1, 1, new Color(0.3f, 0.3f, 0.3f, 1f));
            headerStyle.padding = new RectOffset(8, 8, 6, 6);
            headerStyle.margin = new RectOffset(0, 0, 0, 1);

            EditorGUILayout.BeginHorizontal(headerStyle);

            var headerLabelStyle = new GUIStyle(EditorStyles.boldLabel);
            headerLabelStyle.normal.textColor = Color.white;

            GUILayout.Label("Method", headerLabelStyle, GUILayout.Width(200));
            GUILayout.Label("Target", headerLabelStyle, GUILayout.Width(250));
            GUILayout.Label("GameObject", headerLabelStyle, GUILayout.MinWidth(150));

            EditorGUILayout.EndHorizontal();

            // Subscribers with zebra stripes
            for (int i = 0; i < subscribers.Count; i++)
            {
                DrawSubscriberRow(subscribers[i], i % 2 == 0, channelInfo.SignalType);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSubscriberRow(SignalSubscriberInfo subscriber, bool isEvenRow, Type signalType)
        {
            // Create zebra stripe background
            var rowColor = isEvenRow
                ? new Color(0.85f, 0.85f, 0.85f, 0.3f)  // Light gray for even rows
                : new Color(0.95f, 0.95f, 0.95f, 0.1f); // Very light gray for odd rows

            if (EditorGUIUtility.isProSkin)
            {
                rowColor = isEvenRow
                    ? new Color(0.3f, 0.3f, 0.3f, 0.3f)   // Dark gray for even rows (Pro skin)
                    : new Color(0.2f, 0.2f, 0.2f, 0.1f);  // Darker gray for odd rows (Pro skin)
            }

            var rowStyle = new GUIStyle();
            rowStyle.normal.background = MakeTexture(1, 1, rowColor);
            rowStyle.padding = new RectOffset(8, 8, 4, 4);
            rowStyle.margin = new RectOffset(0, 0, 0, 1);

            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(22));

            // Method name with better handling for anonymous methods
            var methodDisplayName = GetMethodDisplayName(subscriber.MethodName);
            var methodStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft
            };
            if (!subscriber.IsValid)
                methodStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.red : new Color(0.8f, 0f, 0f);

            // Add status icon before method name
            var statusIcon = subscriber.IsValid ? "✅ " : "❌ ";
            GUILayout.Label(statusIcon + methodDisplayName, methodStyle, GUILayout.Width(200));

            // Target name with more space now (increased from 180 to 250)
            var targetText = subscriber.TargetName;
            if (targetText.Length > 35)  // Increased from 25 to 35 characters
                targetText = targetText.Substring(0, 32) + "...";

            var targetStyle = new GUIStyle(EditorStyles.label);
            targetStyle.alignment = TextAnchor.MiddleLeft;
            if (!subscriber.IsValid)
                targetStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.red : new Color(0.8f, 0f, 0f);

            GUILayout.Label(targetText, targetStyle, GUILayout.Width(250));

            // GameObject (clickable) with more space (increased from 120 to 150)
            if (subscriber.OwnerGameObject != null)
            {
                var buttonStyle = new GUIStyle(EditorStyles.linkLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Normal
                };
                buttonStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.7f, 1f) : new Color(0f, 0.3f, 0.8f);
                buttonStyle.hover.textColor = EditorGUIUtility.isProSkin ? Color.cyan : Color.blue;

                if (GUILayout.Button(subscriber.OwnerGameObject.name, buttonStyle, GUILayout.MinWidth(150)))
                {
                    EditorGUIUtility.PingObject(subscriber.OwnerGameObject);
                    Selection.activeGameObject = subscriber.OwnerGameObject;
                }
            }
            else if (subscriber.TargetObject != null)
            {
                var buttonStyle = new GUIStyle(EditorStyles.linkLabel)
                {
                    alignment = TextAnchor.MiddleLeft
                };
                buttonStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.7f, 1f) : new Color(0f, 0.3f, 0.8f);

                if (GUILayout.Button(subscriber.TargetObject.name, buttonStyle, GUILayout.MinWidth(150)))
                {
                    EditorGUIUtility.PingObject(subscriber.TargetObject);
                    Selection.activeObject = subscriber.TargetObject;
                }
            }
            else
            {
                var naStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                naStyle.alignment = TextAnchor.MiddleLeft;
                GUILayout.Label("N/A", naStyle, GUILayout.MinWidth(150));
            }

            EditorGUILayout.EndHorizontal();
        }

        // Helper method to clean up method display names
        private string GetMethodDisplayName(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
                return "Unknown Method";

            // Handle anonymous methods (lambdas)
            if (methodName.Contains("<") && methodName.Contains(">"))
            {
                // Try to extract meaningful information from anonymous method names
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

        // Helper method to create solid color textures for backgrounds with caching
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            // Use cached texture if available
            if (_textureCache.TryGetValue(color, out var cachedTexture) && cachedTexture != null)
                return cachedTexture;

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();

            // Cache the texture
            _textureCache[color] = texture;
            return texture;
        }

        private void OnInspectorUpdate()
        {
            if (_autoRefresh)
            {
                Repaint();
            }
        }
    }
}
#endif
