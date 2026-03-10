using System;
using System.Collections.Generic;
using NekoLib.Extensions;

namespace NekoSignal
{
    /// <summary>Fluent builder for filtered signal emitting.</summary>
    public sealed class SignalEmitOptions<T> where T : struct, ISignal
    {
        private readonly T _signal;
        private List<ISignalFilter> _filters;

        internal SignalEmitOptions(T signal) { _signal = signal; }

        internal List<ISignalFilter> Filters => _filters;

        /// <summary>Adds a subscriber-side filter evaluated against each listener's owner.</summary>
        public SignalEmitOptions<T> Require(ISignalFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            (_filters ??= new()).Add(filter);
            return this;
        }

        /// <summary>Emits the stored signal. Only matching subscribers are invoked when filters are set.</summary>
        public void Emit()
        {
            if (_filters.IsNullOrEmpty())
                SignalBroadcaster.Emit(_signal);
            else
                SignalBroadcaster.Emit(_signal, _filters);
        }
    }
}
