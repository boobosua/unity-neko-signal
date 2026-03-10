#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    public partial class SignalTrackerWindow
    {
        private int _monitorSelectedIndex = 0;
        private Vector2 _monitorLeftScroll, _monitorRightScroll;

        private void DrawSubscriptionMonitorView()
        {
            var channels = SignalBroadcaster.GetAllChannelInfo()?.ToList();
            if (channels == null)
            {
                EditorGUILayout.HelpBox("SignalBroadcaster not available", MessageType.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                channels = channels.Where(c =>
                    c.SignalType != null &&
                    c.SignalType.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

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
            var sep2 = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(new Rect(sep2.x, sep2.y, sep2.width, 1f), BORDER_COLOR);
            EditorGUILayout.Space(2);
            _monitorLeftScroll = EditorGUILayout.BeginScrollView(_monitorLeftScroll, GUILayout.ExpandHeight(true));
            var labelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            var countStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                var t = ch.SignalType;
                var count = ch.SubscriberCount;
                var rowRect = EditorGUILayout.GetControlRect(false, 22f);
                bool isSelectedRow = (i == _monitorSelectedIndex);
                if (isSelectedRow)
                {
                    EditorGUI.DrawRect(rowRect, EditorGUIUtility.isProSkin ? new Color(0.25f, 0.45f, 0.65f, 0.35f) : new Color(0.6f, 0.75f, 0.95f, 0.6f));
                }
                var nameRect = new Rect(rowRect.x + 6, rowRect.y + 3, rowRect.width - 60, rowRect.height - 6);
                var countRect = new Rect(rowRect.xMax - 50, rowRect.y + 3, 44, rowRect.height - 6);
                GUI.Label(nameRect, t.Name, labelStyle);
                GUI.Label(countRect, count.ToString(), countStyle);
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    _monitorSelectedIndex = i;
                    Event.current.Use();
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

        private void DrawSubscriberTable(SignalChannelInfo channelInfo)
        {
            var subscribers = SignalBroadcaster.GetSubscriberInfoByType(channelInfo.SignalType)?.ToList();
            if (subscribers == null || subscribers.Count == 0)
            {
                EditorGUILayout.LabelField("No subscribers found", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            subscribers = subscribers.Where(s =>
                s != null &&
                s.IsValid &&
                (s.OwnerGameObject == null || s.OwnerGameObject) &&
                (s.TargetObject == null || s.TargetObject)
            ).ToList();

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

            if (!_signalPages.ContainsKey(signalType))
                _signalPages[signalType] = 0;

            int totalPages = Mathf.CeilToInt((float)subscribers.Count / SUBSCRIBERS_PER_PAGE);
            _signalPages[signalType] = Mathf.Clamp(_signalPages[signalType], 0, Mathf.Max(0, totalPages - 1));

            int startIndex = _signalPages[signalType] * SUBSCRIBERS_PER_PAGE;
            int endIndex = Mathf.Min(startIndex + SUBSCRIBERS_PER_PAGE, subscribers.Count);

            if (totalPages > 1)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Page {_signalPages[signalType] + 1} of {totalPages} ({startIndex + 1}-{endIndex} of {subscribers.Count})",
                    EditorStyles.miniLabel, GUILayout.Width(200));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            DrawSubscriberTableHeader();

            for (int i = startIndex; i < endIndex; i++)
            {
                if (i < subscribers.Count && subscribers[i] != null)
                    DrawSubscriberTableRow(subscribers[i], (i - startIndex) % 2 == 0);
            }

            if (totalPages > 1)
            {
                EditorGUILayout.Space(5);
                DrawSubscriberPaginationControls(signalType, totalPages, startIndex + 1, endIndex, subscribers.Count);
            }
        }

        private void DrawSubscriberTableHeader()
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, HEADER_HEIGHT);
            EditorGUI.DrawRect(headerRect, HEADER_BG);
            DrawTableBorders(headerRect);

            float totalWidth = headerRect.width;
            float gameObjectWidth = totalWidth * 0.3f;
            float componentWidth = totalWidth * 0.3f;
            float methodWidth = totalWidth * 0.25f;
            float priorityWidth = totalWidth * 0.15f;

            float x = headerRect.x;
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            headerStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

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

            if (isEvenRow)
                EditorGUI.DrawRect(rowRect, GetZebraStripeColor());

            DrawTableBorders(rowRect);

            float totalWidth = rowRect.width;
            float gameObjectWidth = totalWidth * 0.3f;
            float componentWidth = totalWidth * 0.3f;
            float methodWidth = totalWidth * 0.25f;
            float priorityWidth = totalWidth * 0.15f;

            float x = rowRect.x;

            try
            {
                Color defaultTextColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
                Color dimmedTextColor = Color.Lerp(defaultTextColor, Color.gray, 0.2f);

                DrawGameObjectColumn(new Rect(x, rowRect.y, gameObjectWidth, rowRect.height), subscriber);
                x += gameObjectWidth;
                DrawVerticalDivider(x, rowRect.y, rowRect.height);

                var componentName = GetComponentDisplayName(subscriber);
                if (componentName.Length > 30) componentName = componentName.Substring(0, 27) + "...";
                DrawSubscriberColumn(new Rect(x, rowRect.y, componentWidth, rowRect.height), componentName, dimmedTextColor);
                x += componentWidth;
                DrawVerticalDivider(x, rowRect.y, rowRect.height);

                var methodDisplayName = GetMethodDisplayName(subscriber.MethodName);
                var methodRect = new Rect(x, rowRect.y, methodWidth, rowRect.height);
                var linkCenter = new GUIStyle(EditorStyles.linkLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
                if (GUI.Button(new Rect(methodRect.x + 8, methodRect.y, methodRect.width - 16, methodRect.height), methodDisplayName, linkCenter))
                    TryOpenSubscriberMethod(subscriber);
                x += methodWidth;
                DrawVerticalDivider(x, rowRect.y, rowRect.height);

                DrawSubscriberColumn(new Rect(x, rowRect.y, priorityWidth, rowRect.height), subscriber.Priority.ToString(), dimmedTextColor);
            }
            catch (Exception e)
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
            var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
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

                var linkStyle = new GUIStyle(EditorStyles.linkLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
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

                var linkStyle = new GUIStyle(EditorStyles.linkLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
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

            GUI.enabled = _signalPages[signalType] > 0;
            if (GUILayout.Button("◀ Previous", GUILayout.Width(70), GUILayout.Height(20)))
                _signalPages[signalType]--;
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{startItem}-{endItem} of {totalItems}",
                EditorStyles.centeredGreyMiniLabel, GUILayout.Width(80));
            GUILayout.FlexibleSpace();

            GUI.enabled = _signalPages[signalType] < totalPages - 1;
            if (GUILayout.Button("Next ▶", GUILayout.Width(70), GUILayout.Height(20)))
                _signalPages[signalType]++;
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
