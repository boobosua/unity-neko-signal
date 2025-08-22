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
        private int _currentPage = 0;
        private const int ITEMS_PER_PAGE = 10;

        public override void OnInspectorGUI()
        {
            try
            {
                var monitor = (SignalReceiverMonitor)target;
                if (monitor == null) return;

                // Header
                EditorGUILayout.Space();
                // Summary
                var receiversCount = monitor.ActiveReceiversCount;
                var summaryStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    fontSize = 12
                };

                EditorGUILayout.BeginVertical(summaryStyle);
                EditorGUILayout.LabelField($"Active Subscriptions: {receiversCount}", EditorStyles.boldLabel);

                if (receiversCount == 0)
                {
                    EditorGUILayout.LabelField("🎉 No active signal subscriptions", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField($"Automatic cleanup on GameObject destruction", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();

                if (receiversCount > 0)
                {
                    EditorGUILayout.Space();

                    // Toggle for details
                    _showDetails = EditorGUILayout.Foldout(_showDetails, "Subscription Details", true);

                    if (_showDetails)
                    {
                        DrawReceiversTable(monitor);
                    }

                    // Reduced spacing before buttons
                    EditorGUILayout.Space(5);

                    // Action buttons
                    DrawActionButtons(monitor, receiversCount);
                }

                EditorGUILayout.Space();
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Error in SignalReceiverMonitor: {e.Message}", MessageType.Error);
                Debug.LogError($"[SignalReceiverMonitor] OnInspectorGUI Error: {e}");
            }
        }

        private void DrawReceiversTable(SignalReceiverMonitor monitor)
        {
            try
            {
                var receivers = monitor.GetReceiversInfo()?.ToList();
                if (receivers == null || receivers.Count == 0)
                {
                    EditorGUILayout.LabelField("No receiver information available", EditorStyles.miniLabel);
                    return;
                }

                int totalPages = Mathf.CeilToInt((float)receivers.Count / ITEMS_PER_PAGE);

                // Ensure current page is valid
                _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, totalPages - 1));

                // Table header with pagination info
                if (totalPages > 1)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"Page {_currentPage + 1} of {totalPages}", EditorStyles.miniLabel, GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                }

                // Table-style layout
                var tableStyle = new GUIStyle(EditorStyles.helpBox);
                EditorGUILayout.BeginVertical(tableStyle);

                try
                {
                    // Table header
                    EditorGUILayout.BeginHorizontal();
                    var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
                    EditorGUILayout.LabelField("", headerStyle, GUILayout.Width(20)); // Status icon
                    EditorGUILayout.LabelField("#", headerStyle, GUILayout.Width(30));
                    EditorGUILayout.LabelField("Callback Information", headerStyle);
                    EditorGUILayout.LabelField("Status", headerStyle, GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();

                    // Separator line
                    var separatorRect = GUILayoutUtility.GetRect(0, 1);
                    EditorGUI.DrawRect(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));

                    // Table rows
                    int startIndex = _currentPage * ITEMS_PER_PAGE;
                    int endIndex = Mathf.Min(startIndex + ITEMS_PER_PAGE, receivers.Count);

                    for (int i = startIndex; i < endIndex; i++)
                    {
                        if (i < receivers.Count)
                        {
                            DrawReceiverRow(receivers[i], i, startIndex);
                        }
                    }
                }
                finally
                {
                    EditorGUILayout.EndVertical();
                }

                // Pagination controls
                if (totalPages > 1)
                {
                    int startIndex = _currentPage * ITEMS_PER_PAGE;
                    int endIndex = Mathf.Min(startIndex + ITEMS_PER_PAGE, receivers.Count);
                    DrawPaginationControls(receivers, startIndex, endIndex);
                }
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Error drawing receivers table: {e.Message}", MessageType.Error);
                Debug.LogError($"[SignalReceiverMonitor] DrawReceiversTable Error: {e}");
            }
        }

        private void DrawReceiverRow((string callbackInfo, bool isActive) receiver, int globalIndex, int startIndex)
        {
            var (callbackInfo, isActive) = receiver;

            // Alternating row colors
            var bgColor = (globalIndex - startIndex) % 2 == 0 ?
                new Color(0f, 0f, 0f, 0.05f) :
                new Color(0f, 0f, 0f, 0.1f);

            if (!isActive)
            {
                bgColor = new Color(1f, 0.3f, 0.3f, 0.2f); // Red tint for inactive
            }

            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            try
            {
                GUI.backgroundColor = originalColor;

                // Status icon
                var icon = isActive ? "✅" : "❌";
                EditorGUILayout.LabelField(icon, GUILayout.Width(20));

                // Index
                EditorGUILayout.LabelField($"{globalIndex + 1}", GUILayout.Width(30));

                // Callback info (truncated for table display)
                var truncatedInfo = callbackInfo?.Length > 60 ?
                    callbackInfo.Substring(0, 57) + "..." :
                    callbackInfo ?? "Unknown";
                EditorGUILayout.LabelField(truncatedInfo, EditorStyles.label);

                // Status
                var statusStyle = new GUIStyle(EditorStyles.miniLabel);
                statusStyle.normal.textColor = isActive ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
                var statusText = isActive ? "Active" : "Disposed";
                EditorGUILayout.LabelField(statusText, statusStyle, GUILayout.Width(60));
            }
            finally
            {
                GUI.backgroundColor = originalColor;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPaginationControls(System.Collections.Generic.List<(string, bool)> receivers, int startIndex, int endIndex)
        {
            EditorGUILayout.BeginHorizontal();

            try
            {
                int totalPages = Mathf.CeilToInt((float)receivers.Count / ITEMS_PER_PAGE);

                // Previous button
                GUI.enabled = _currentPage > 0;
                if (GUILayout.Button("◀ Previous", GUILayout.Width(80), GUILayout.Height(20)))
                {
                    _currentPage--;
                }
                GUI.enabled = true;

                GUILayout.FlexibleSpace();

                // Page info
                EditorGUILayout.LabelField($"{startIndex + 1}-{endIndex} of {receivers.Count}",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Width(80));

                GUILayout.FlexibleSpace();

                // Next button
                GUI.enabled = _currentPage < totalPages - 1;
                if (GUILayout.Button("Next ▶", GUILayout.Width(80), GUILayout.Height(20)))
                {
                    _currentPage++;
                }
                GUI.enabled = true;
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawActionButtons(SignalReceiverMonitor monitor, int receiversCount)
        {
            EditorGUILayout.BeginHorizontal();

            try
            {
                if (GUILayout.Button("🔄 Refresh", GUILayout.Height(25)))
                {
                    // Only repaint if not in layout/repaint event to prevent loops
                    if (Event.current?.type != EventType.Repaint && Event.current?.type != EventType.Layout)
                    {
                        Repaint();
                    }
                }

                if (GUILayout.Button("🗑️ Dispose All", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Dispose All Receivers",
                        $"Are you sure you want to dispose all {receiversCount} signal receivers?",
                        "Yes", "Cancel"))
                    {
                        try
                        {
                            monitor.DisposeAllReceivers();
                            if (Event.current?.type != EventType.Repaint && Event.current?.type != EventType.Layout)
                            {
                                Repaint();
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"[SignalReceiverMonitor] Error disposing all receivers: {e}");
                        }
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        // Custom header
        protected override void OnHeaderGUI()
        {
            try
            {
                var monitor = (SignalReceiverMonitor)target;
                if (monitor == null) return;

                var receiversCount = monitor.ActiveReceiversCount;

                GUILayout.BeginVertical();

                try
                {
                    // Default header
                    base.OnHeaderGUI();

                    // Custom status bar
                    var statusColor = receiversCount > 0 ? Color.green : Color.grey;
                    var originalColor = GUI.color;
                    GUI.color = statusColor;

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    try
                    {
                        GUI.color = originalColor;

                        GUILayout.Label($"📡 {receiversCount} Active Subscriptions", EditorStyles.boldLabel);

                        if (receiversCount > 0)
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("🗑️", GUILayout.Width(25), GUILayout.Height(18)))
                            {
                                try
                                {
                                    monitor.DisposeAllReceivers();
                                }
                                catch (System.Exception e)
                                {
                                    Debug.LogError($"[SignalReceiverMonitor] Error disposing receivers from header: {e}");
                                }
                            }
                        }
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }
                }
                finally
                {
                    GUILayout.EndVertical();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SignalReceiverMonitor] OnHeaderGUI Error: {e}");
                // Fallback to default header
                base.OnHeaderGUI();
            }
        }
    }
}
#endif