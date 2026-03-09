#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NekoSignal
{
    [Serializable]
    internal class SignalInvocationLog
    {
        public string MethodName;
        public string ComponentName;
        public string GameObjectName;
        public int Priority;
        public bool Threw;
        public string ExceptionMessage;
    }

    [Serializable]
    internal class SignalEmitLog
    {
        public Type SignalType;
        public string SignalTypeName;
        public DateTime Time;
        public int Frame;
        public List<PayloadField> PayloadFields = new();
        public bool PayloadExpanded;
        public List<SignalInvocationLog> Invocations = new();
        public int Id;

        public bool PayloadIsNull;
        public bool PayloadReflectionError;
        public bool PayloadInspectableMembersFound;

        public string EmitterComponentName;
        public string EmitterGameObjectName;
        public UnityEngine.Object EmitterObject;
        public string ScriptFilePath;
        public int ScriptLine;

        public List<string> Filters = new();
    }

    [Serializable]
    internal class PayloadField
    {
        public string Name;
        public string Value;
    }
}
#endif
