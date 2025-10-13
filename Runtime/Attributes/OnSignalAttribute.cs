// NekoSignal — OnSignal Attribute
// Supports implicit and explicit usage
using System;
using NekoLib.Core;
using NekoLib.Extensions;

namespace NekoSignal
{
    /// <summary>
    /// Marks a method as a signal handler.
    /// The method must have exactly one parameter matching the signal type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    [UnityEngine.Scripting.Preserve]
    public sealed class OnSignalAttribute : Attribute
    {
        public Type ExplicitSignalType { get; }

        public OnSignalAttribute() { }

        public OnSignalAttribute(Type signalType)
        {
            if (signalType == null)
                throw new ArgumentNullException(nameof(signalType));

            if (!typeof(ISignal).IsAssignableFrom(signalType))
                throw new ArgumentException($"{signalType.Name.Colorize(Swatch.VR)} does not implement ISignal");

            ExplicitSignalType = signalType;
        }
    }
}
