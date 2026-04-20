using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using LogAssert = UnityEngine.TestTools.LogAssert;

namespace NekoSignal.Tests
{
    [TestFixture]
    public class SignalDispatchTests
    {
        private GameObject _go;
        private MonoBehaviour _owner;
        private readonly List<SignalReceiver> _receivers = new();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("DispatchTest");
            _owner = _go.AddComponent<DispatchTestBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in _receivers) r?.Dispose();
            _receivers.Clear();
            if (_go) Object.Destroy(_go);
            SignalBroadcaster.UnsubscribeAll();
        }

        private SignalReceiver Track(SignalReceiver r) { _receivers.Add(r); return r; }

        // ── Basic dispatch ──────────────────────────────────────────────────

        [Test]
        public void Emit_WithSubscriber_InvokesCallback()
        {
            bool called = false;
            Track(_owner.Listen<PingSignal>(_ => called = true));
            SignalBus.Emit(new PingSignal());
            Assert.IsTrue(called);
        }

        [Test]
        public void Emit_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => SignalBus.Emit(new PongSignal()));
        }

        [Test]
        public void Emit_DeliversPayloadFields()
        {
            int received = -1;
            Track(_owner.Listen<PingSignal>(s => received = s.Id));
            SignalBus.Emit(new PingSignal { Id = 42 });
            Assert.AreEqual(42, received);
        }

        // ── Null / invalid inputs ───────────────────────────────────────────
        // Subscribe returns null (logs warning) instead of throwing.

        [Test]
        public void Listen_NullOwner_ReturnsNull_NoThrow()
        {
            LogAssert.ignoreFailingMessages = true; // SignalBroadcaster.Subscribe logs a warning
            SignalReceiver r = null;
            Assert.DoesNotThrow(() => r = SignalBus.Listen<PingSignal>(null, _ => { }));
            Assert.IsNull(r, "Subscribe with null owner must return null.");
        }

        [Test]
        public void Listen_NullCallback_ReturnsNull_NoThrow()
        {
            LogAssert.ignoreFailingMessages = true; // SignalBroadcaster.Subscribe logs a warning
            SignalReceiver r = null;
            Assert.DoesNotThrow(() => r = _owner.Listen<PingSignal>((Action<PingSignal>)null));
            Assert.IsNull(r, "Subscribe with null callback must return null.");
        }

        // ── Unsubscribe ─────────────────────────────────────────────────────

        [Test]
        public void Unsubscribe_PreventsSubsequentInvocation()
        {
            bool called = false;
            var r = Track(_owner.Listen<PingSignal>(_ => called = true));
            r.Dispose();
            SignalBus.Emit(new PingSignal());
            Assert.IsFalse(called);
        }

        // ── Priority ────────────────────────────────────────────────────────

        [Test]
        public void Priority_HigherValueCalledFirst()
        {
            var order = new List<int>();
            Track(_owner.Listen<PingSignal>(_ => order.Add(1),  priority: 1));
            Track(_owner.Listen<PingSignal>(_ => order.Add(10), priority: 10));
            Track(_owner.Listen<PingSignal>(_ => order.Add(5),  priority: 5));
            SignalBus.Emit(new PingSignal());
            CollectionAssert.AreEqual(new[] { 10, 5, 1 }, order);
        }

        [Test]
        public void Priority_SamePriority_FifoOrder()
        {
            var order = new List<int>();
            Track(_owner.Listen<PingSignal>(_ => order.Add(1)));
            Track(_owner.Listen<PingSignal>(_ => order.Add(2)));
            Track(_owner.Listen<PingSignal>(_ => order.Add(3)));
            SignalBus.Emit(new PingSignal());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void Priority_NegativeValues_OrderedCorrectly()
        {
            var order = new List<int>();
            Track(_owner.Listen<PingSignal>(_ => order.Add(-1), priority: -1));
            Track(_owner.Listen<PingSignal>(_ => order.Add(0),  priority: 0));
            Track(_owner.Listen<PingSignal>(_ => order.Add(1),  priority: 1));
            SignalBus.Emit(new PingSignal());
            CollectionAssert.AreEqual(new[] { 1, 0, -1 }, order);
        }

        // ── Duplicate callback (no dedup by design) ─────────────────────────
        // AddCallback does not deduplicate; same delegate subscribed twice fires twice.

        [Test]
        public void DuplicateCallback_InvokedTwice()
        {
            int count = 0;
            Action<PingSignal> cb = _ => count++;
            Track(_owner.Listen<PingSignal>(cb));
            Track(_owner.Listen<PingSignal>(cb));
            SignalBus.Emit(new PingSignal());
            Assert.AreEqual(2, count, "Identical delegate registered twice must fire twice.");
        }

        // ── Exception isolation ─────────────────────────────────────────────

        [Test]
        public void Exception_InOneSubscriber_OthersStillReceive()
        {
            LogAssert.ignoreFailingMessages = true; // SignalChannel logs the caught exception as an error
            bool secondCalled = false;
            Track(_owner.Listen<PingSignal>(_ => throw new InvalidOperationException("test"), priority: 10));
            Track(_owner.Listen<PingSignal>(_ => secondCalled = true, priority: 1));
            Assert.DoesNotThrow(() => SignalBus.Emit(new PingSignal()));
            Assert.IsTrue(secondCalled);
        }

        // ── SubscriberCount ─────────────────────────────────────────────────

        [Test]
        public void SubscriberCount_ReflectsActiveSubscriptions()
        {
            Assert.AreEqual(0, SignalBus.GetSubscriberCount<PingSignal>());
            var r = Track(_owner.Listen<PingSignal>(_ => { }));
            Assert.AreEqual(1, SignalBus.GetSubscriberCount<PingSignal>());
            r.Dispose();
            Assert.AreEqual(0, SignalBus.GetSubscriberCount<PingSignal>());
        }

        // ── Destroyed-owner lifecycle ────────────────────────────────────────
        // SubscriberCount uses Unity's bool operator: destroyed owners return false
        // immediately after Destroy(), before the next Emit. Emit then flushes the
        // stale entry from the internal list.

        [UnityTest]
        public IEnumerator DestroyedOwner_ExcludedFromCount_AndNotInvokedOnNextEmit()
        {
            bool called = false;
            Track(_owner.Listen<PingSignal>(_ => called = true));
            Assert.AreEqual(1, SignalBus.GetSubscriberCount<PingSignal>());

            Object.Destroy(_go);
            yield return null; // Unity processes Destroy at end of frame

            Assert.AreEqual(0, SignalBus.GetSubscriberCount<PingSignal>(),
                "Destroyed owner must be excluded from count before next Emit.");

            Assert.DoesNotThrow(() => SignalBus.Emit(new PingSignal()));
            Assert.IsFalse(called, "Destroyed owner must not receive the signal.");
            Assert.AreEqual(0, SignalBus.GetSubscriberCount<PingSignal>());
        }

        // ── Unsubscribe during Emit ──────────────────────────────────────────
        // RemoveCallback defers removal via _pendingRemovals when _isInvoking is true.

        [UnityTest]
        public IEnumerator UnsubscribeDuringEmit_DeferredThenRespected()
        {
            SignalReceiver self = null;
            int callCount = 0;
            self = Track(_owner.Listen<PingSignal>(_ =>
            {
                callCount++;
                self?.Dispose();
            }));

            SignalBus.Emit(new PingSignal());
            Assert.AreEqual(1, callCount, "Must fire once in the emit where it unsubscribes.");

            yield return null;
            SignalBus.Emit(new PingSignal());
            Assert.AreEqual(1, callCount, "Must not fire after deferred unsubscribe is flushed.");
        }

        private class DispatchTestBehaviour : MonoBehaviour { }
    }
}
