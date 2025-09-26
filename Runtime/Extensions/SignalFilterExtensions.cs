using UnityEngine;

namespace NekoSignal
{
    public static class SignalFilterExtensions
    {
        /// <summary>
        /// Wraps a signal into an emit options pipeline.
        /// </summary>
        public static SignalEmitOptions<T> SetEmitter<T>(this T signal, MonoBehaviour emitter) where T : ISignal
        {
            return new SignalEmitOptions<T>(signal).EmitBy(emitter);
        }
    }
}
