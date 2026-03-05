#if UNITY_EDITOR
using UnityEditor;

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
                }
            };
        }
    }
}
#endif
