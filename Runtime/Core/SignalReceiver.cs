using System;
using NekoLib.Logger;

namespace NekoSignal
{
    public sealed class SignalReceiver : IDisposable
    {
        public Type SignalType { get; }
        private readonly Action _unsubscribeAction;
        private bool _isDisposed;

        internal SignalReceiver(Action unsubscribeAction, Type signalType)
        {
            if (unsubscribeAction == null)
            {
                Log.Warn("[SignalReceiver] Unsubscribe action is null.");
                return;
            }

            _unsubscribeAction = unsubscribeAction;
            SignalType = signalType;
        }

        public bool IsActive => !_isDisposed;

        public void Dispose()
        {
            if (_isDisposed) return;
            _unsubscribeAction?.Invoke();
            _isDisposed = true;
        }
    }
}
