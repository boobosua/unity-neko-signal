// NekoSignal — OnSignal Attribute
// Supports implicit and explicit usage
using System;

namespace NekoSignal
{
    /// <summary>
    /// Marks a method as a signal handler.
    /// The method must have exactly one parameter matching the signal type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class OnSignalAttribute : Attribute
    {
        public Type ExplicitSignalType { get; }

        public OnSignalAttribute() { }

        public OnSignalAttribute(Type signalType)
        {
            if (signalType == null)
                throw new ArgumentNullException(nameof(signalType));

            if (!typeof(ISignal).IsAssignableFrom(signalType))
                throw new ArgumentException($"{signalType.Name} does not implement ISignal");

            ExplicitSignalType = signalType;
        }
    }
}
