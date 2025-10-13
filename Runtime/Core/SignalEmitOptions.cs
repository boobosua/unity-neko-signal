using System.Collections.Generic;
using NekoLib.Logger;
using UnityEngine;

namespace NekoSignal
{
    /// <summary>
    /// Fluent emit pipeline to publish with subscriber filters, GameEventHub-style.
    /// Does not change Subscribe(); filters are applied at dispatch time per-listener.
    /// </summary>
    public sealed class SignalEmitOptions<T> where T : ISignal
    {
        public T Signal { get; }
        public MonoBehaviour Emitter { get; private set; }
        private readonly List<ISignalFilter> _filters = new();

        public SignalEmitOptions(T signal) { Signal = signal; }

        public SignalEmitOptions<T> EmitBy(MonoBehaviour emitter)
        {
            if (emitter == null)
            {
                Log.Warn("[NekoSignal] EmitBy received null emitter.");
                return this;
            }
            Emitter = emitter;
            return this;
        }

        /// <summary>
        /// Add a subscriber-side filter (evaluated against each listener's owner).
        /// </summary>
        public SignalEmitOptions<T> Require(ISignalFilter filter)
        {
            if (filter == null)
            {
                Log.Warn("[NekoSignal] Require received null filter. Ignored.");
                return this;
            }
            _filters.Add(filter);
            return this;
        }

        /// <summary>
        /// Publish the signal. If filters were provided, only matching subscribers will be invoked.
        /// </summary>
        public void Publish()
        {
            if (_filters.Count == 0)
            {
                SignalBroadcaster.Publish(Signal);
            }
            else
            {
                // Use params overload
                SignalBroadcaster.Publish(Signal, _filters.ToArray());
            }
        }
    }
}
