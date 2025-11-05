#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    [CustomEditor(typeof(SignalReceiverMonitor))]
    public class SignalReceiverMonitorInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            var monitor = (SignalReceiverMonitor)target;
            if (!monitor) return;

            // Minimal inspector with a clean summary box
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Active Subscriptions:", EditorStyles.label);
            var countStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight, fontSize = 14 };
            EditorGUILayout.LabelField(monitor.ActiveReceiversCount.ToString(), countStyle, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // Hide the script field in the header by not drawing default header content
        protected override void OnHeaderGUI()
        {
            // Do nothing to avoid showing the script field
        }
    }
}
#endif