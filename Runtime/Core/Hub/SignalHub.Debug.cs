#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    public static partial class SignalHub
    {
        [InitializeOnLoadMethod]
        private static void EditorInit()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    _cache.Clear();
                    _activeBindings.Clear();
                    _boundDelegates.Clear();
                }
            };
        }

        /// <summary>Returns display names of bound instances whose MonoBehaviour has been destroyed without calling Unbind.</summary>
        public static IEnumerable<string> GetLeakedBindings()
        {
            foreach (var kvp in _boundDelegates)
            {
                var list = kvp.Value;
                if (list == null || list.Count == 0) continue;
                var target = list[0].Del?.Target as MonoBehaviour;
                if (target is null) continue; // not a MonoBehaviour target (C# null, not Unity fake-null)
                if (!target) // Unity null — destroyed but not unbound
                    yield return $"{list[0].Del.Target.GetType().Name} (Instance ID: {kvp.Key})";
            }
        }
    }
}
#endif
