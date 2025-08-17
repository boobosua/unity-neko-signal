#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace NekoSignal
{
    [CustomEditor(typeof(SignalReceiverMonitor))]
    public class SignalReceiverMonitorInspector : Editor
    {
        private bool _showDetails = true;
        private Vector2 _scrollPosition;

        public override void OnInspectorGUI()
        {
            var monitor = (SignalReceiverMonitor)target;

            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Signal Receiver Monitor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Summary
            var receiversCount = monitor.ActiveReceiversCount;
            var summaryStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12
            };

            EditorGUILayout.BeginVertical(summaryStyle);
            EditorGUILayout.LabelField($"📊 Active Subscriptions: {receiversCount}", EditorStyles.boldLabel);

            if (receiversCount == 0)
            {
                EditorGUILayout.LabelField("🎉 No active signal subscriptions", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"💾 Automatic cleanup on GameObject destruction", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            if (receiversCount > 0)
            {
                EditorGUILayout.Space();

                // Toggle for details
                _showDetails = EditorGUILayout.Foldout(_showDetails, "📋 Subscription Details", true);

                if (_showDetails)
                {
                    EditorGUILayout.Space();

                    // Scrollable area for many subscriptions
                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MaxHeight(300));

                    var receivers = monitor.GetReceiversInfo().ToList();

                    for (int i = 0; i < receivers.Count; i++)
                    {
                        var (callbackInfo, isActive) = receivers[i];

                        // Style based on active status
                        var bgColor = isActive ? new Color(0.8f, 1f, 0.8f, 0.3f) : new Color(1f, 0.8f, 0.8f, 0.3f);
                        var icon = isActive ? "✅" : "❌";

                        var originalColor = GUI.backgroundColor;
                        GUI.backgroundColor = bgColor;

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        GUI.backgroundColor = originalColor;

                        EditorGUILayout.BeginHorizontal();

                        // Status icon
                        GUILayout.Label(icon, GUILayout.Width(20));

                        // Callback info
                        EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(25));
                        EditorGUILayout.LabelField(callbackInfo, EditorStyles.wordWrappedLabel);

                        EditorGUILayout.EndHorizontal();

                        // Status text
                        var statusStyle = new GUIStyle(EditorStyles.miniLabel);
                        statusStyle.normal.textColor = isActive ? Color.green : Color.red;
                        EditorGUILayout.LabelField($"Status: {(isActive ? "Active" : "Disposed")}", statusStyle);

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);
                    }

                    EditorGUILayout.EndScrollView();
                }

                EditorGUILayout.Space();

                // Action buttons
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("🔄 Refresh", GUILayout.Height(25)))
                {
                    Repaint();
                }

                if (GUILayout.Button("🗑️ Dispose All", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Dispose All Receivers",
                        $"Are you sure you want to dispose all {receiversCount} signal receivers?",
                        "Yes", "Cancel"))
                    {
                        monitor.DisposeAllReceivers();
                        Repaint();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
        }

        // Custom header
        protected override void OnHeaderGUI()
        {
            var monitor = (SignalReceiverMonitor)target;
            var receiversCount = monitor.ActiveReceiversCount;

            GUILayout.BeginVertical();

            // Default header
            base.OnHeaderGUI();

            // Custom status bar
            var statusColor = receiversCount > 0 ? Color.green : Color.grey;
            var originalColor = GUI.color;
            GUI.color = statusColor;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.color = originalColor;

            GUILayout.Label($"📡 {receiversCount} Active Subscriptions", EditorStyles.boldLabel);

            if (receiversCount > 0)
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("🗑️", GUILayout.Width(25), GUILayout.Height(18)))
                {
                    monitor.DisposeAllReceivers();
                }
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
#endif