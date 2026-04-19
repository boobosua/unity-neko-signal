#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NekoLib.Logger;
using UnityEditor;
using UnityEngine;

namespace NekoSignal
{
    internal static partial class SignalBroadcaster
    {
        [InitializeOnLoadMethod]
        private static void InitializeEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Log.Info("[SignalBroadcaster] Clearing all signal channels on play mode exit.");
                UnsubscribeAll();
            }
        }

        /// <summary>Gets channel info for all active signal types.</summary>
        internal static IEnumerable<SignalChannelInfo> GetAllChannelInfo()
        {
            return _signalChannels.Select(kvp => new SignalChannelInfo
            {
                SignalType = kvp.Key,
                SubscriberCount = kvp.Value.SubscriberCount,
                Channel = kvp.Value
            });
        }

        /// <summary>Gets subscriber info for a signal type by Type object.</summary>
        internal static IEnumerable<SignalSubscriberInfo> GetSubscriberInfoByType(Type signalType)
        {
            if (!_signalChannels.TryGetValue(signalType, out var channel) || channel.SubscriberCount == 0)
                return Enumerable.Empty<SignalSubscriberInfo>();

            return channel.GetSubscriberInfo();
        }
    }

    /// <summary>Information about a signal channel for editor tools.</summary>
    internal class SignalChannelInfo
    {
        public Type SignalType { get; set; }
        public int SubscriberCount { get; set; }
        internal ISignalChannel Channel { get; set; }
    }

    /// <summary>Information about a signal subscriber for editor tools.</summary>
    internal class SignalSubscriberInfo
    {
        public string MethodName { get; set; }
        public string TargetName { get; set; }
        public UnityEngine.Object TargetObject { get; set; }
        public GameObject OwnerGameObject { get; set; }
        public Type SignalType { get; set; }
        public bool IsValid { get; set; }
        public int Priority { get; set; }
    }
}
#endif
