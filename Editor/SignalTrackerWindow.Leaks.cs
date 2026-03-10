#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    public partial class SignalTrackerWindow
    {
        private Vector2 _leaksScroll;

        private void DrawMemoryLeaksView()
        {
            var leaks = SignalHub.GetLeakedBindings().ToList();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(
                leaks.Count == 0 ? "No leaks detected" : $"{leaks.Count} leaked binding(s)",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (leaks.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "All SignalHub.Bind() calls have a matching Unbind().",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EditorGUILayout.HelpBox(
                "These MonoBehaviours were destroyed without calling SignalHub.Unbind(). " +
                "Their delegate references are still alive inside SignalHub, preventing GC.",
                MessageType.Warning);

            EditorGUILayout.Space(4);

            _leaksScroll = EditorGUILayout.BeginScrollView(_leaksScroll);

            var rowBg = EditorGUIUtility.isProSkin
                ? new Color(1f, 0.6f, 0.1f, 0.08f)
                : new Color(1f, 0.6f, 0.1f, 0.12f);

            for (int i = 0; i < leaks.Count; i++)
            {
                var rowRect = EditorGUILayout.GetControlRect(false, ROW_HEIGHT);
                if (i % 2 == 0)
                    EditorGUI.DrawRect(rowRect, rowBg);
                DrawTableBorders(rowRect);

                var labelRect = new Rect(rowRect.x + 10, rowRect.y, rowRect.width - 10, rowRect.height);
                var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 11 };
                GUI.Label(labelRect, leaks[i], style);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
