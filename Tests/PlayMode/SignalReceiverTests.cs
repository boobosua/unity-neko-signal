using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NekoSignal.Tests
{
    [TestFixture]
    public class SignalReceiverTests
    {
        private GameObject _go;
        private MonoBehaviour _owner;
        private readonly List<SignalReceiver> _receivers = new();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ReceiverTest");
            _owner = _go.AddComponent<ReceiverTestBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in _receivers) r?.Dispose();
            _receivers.Clear();
            Object.Destroy(_go);
            SignalBroadcaster.UnsubscribeAll();
        }

        [Test]
        public void IsActive_TrueAfterListen()
        {
            var r = _owner.Listen<PingSignal>(_ => { });
            _receivers.Add(r);
            Assert.IsTrue(r.IsActive);
        }

        [Test]
        public void IsActive_FalseAfterDispose()
        {
            var r = _owner.Listen<PingSignal>(_ => { });
            _receivers.Add(r);
            r.Dispose();
            Assert.IsFalse(r.IsActive);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var r = _owner.Listen<PingSignal>(_ => { });
            _receivers.Add(r);
            Assert.DoesNotThrow(() => { r.Dispose(); r.Dispose(); });
        }

        [Test]
        public void SignalType_MatchesGenericTypeArgument()
        {
            var r = _owner.Listen<PingSignal>(_ => { });
            _receivers.Add(r);
            Assert.AreEqual(typeof(PingSignal), r.SignalType);
        }

        // ── Null returns from invalid inputs ──────────────────────────────────
        // SignalBroadcaster.Subscribe logs a warning and returns null instead of
        // throwing when owner or callback is null.

        [Test]
        public void Listen_NullOwner_ReturnsNull()
        {
            SignalReceiver r = null;
            Assert.DoesNotThrow(() => r = SignalBus.Listen<PingSignal>(null, _ => { }));
            Assert.IsNull(r);
        }

        [Test]
        public void Listen_NullCallback_ReturnsNull()
        {
            SignalReceiver r = null;
            Assert.DoesNotThrow(() => r = _owner.Listen<PingSignal>((Action<PingSignal>)null));
            Assert.IsNull(r);
        }

        private class ReceiverTestBehaviour : MonoBehaviour { }
    }
}
