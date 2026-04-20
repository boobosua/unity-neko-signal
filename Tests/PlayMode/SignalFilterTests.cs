using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NekoSignal.Tests
{
    [TestFixture]
    public class SignalFilterTests
    {
        private GameObject _go;
        private FilterTestBehaviour _owner;
        private readonly List<SignalReceiver> _receivers = new();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("FilterTest");
            _owner = _go.AddComponent<FilterTestBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in _receivers) r?.Dispose();
            _receivers.Clear();
            Object.Destroy(_go);
            SignalBroadcaster.UnsubscribeAll();
        }

        private void Subscribe(Action<PingSignal> cb)
            => _receivers.Add(_owner.Listen(cb));

        // ── HasComponent<T> ───────────────────────────────────────────────────

        [Test]
        public void HasComponent_PassesWhenPresent()
        {
            _go.AddComponent<Rigidbody>();
            bool called = false;
            Subscribe(_ => called = true);
            _owner.Emit(new PingSignal(), new HasComponent<Rigidbody>());
            Assert.IsTrue(called);
        }

        [Test]
        public void HasComponent_BlocksWhenAbsent()
        {
            bool called = false;
            Subscribe(_ => called = true);
            _owner.Emit(new PingSignal(), new HasComponent<Rigidbody>());
            Assert.IsFalse(called);
        }

        // ── WithTag ───────────────────────────────────────────────────────────

        [Test]
        public void WithTag_PassesMatchingTag()
        {
            _go.tag = "Player";
            bool called = false;
            Subscribe(_ => called = true);
            _owner.Emit(new PingSignal(), new WithTag("Player"));
            Assert.IsTrue(called);
        }

        [Test]
        public void WithTag_BlocksNonMatchingTag()
        {
            _go.tag = "Untagged";
            bool called = false;
            Subscribe(_ => called = true);
            _owner.Emit(new PingSignal(), new WithTag("Player"));
            Assert.IsFalse(called);
        }

        // ── InLayer ───────────────────────────────────────────────────────────

        [Test]
        public void InLayer_PassesMatchingLayer()
        {
            _go.layer = 3;
            bool called = false;
            Subscribe(_ => called = true);
            _owner.Emit(new PingSignal(), new InLayer(1 << 3));
            Assert.IsTrue(called);
        }

        [Test]
        public void InLayer_BlocksNonMatchingLayer()
        {
            _go.layer = 0;
            bool called = false;
            Subscribe(_ => called = true);
            _owner.Emit(new PingSignal(), new InLayer(1 << 3));
            Assert.IsFalse(called);
        }

        // ── Multi-filter AND logic ─────────────────────────────────────────────

        [Test]
        public void MultipleFilters_AllMustPass()
        {
            _go.tag = "Player";
            bool called = false;
            Subscribe(_ => called = true);
            // WithTag passes but HasComponent fails (Rigidbody not added)
            _owner.Emit(new PingSignal(), new WithTag("Player"), new HasComponent<Rigidbody>());
            Assert.IsFalse(called);
        }

        // ── ConfigureFilters fluent API ───────────────────────────────────────

        [Test]
        public void ConfigureFilters_WithRequire_EmitsWhenFilterPasses()
        {
            _go.AddComponent<Rigidbody>();
            bool called = false;
            Subscribe(_ => called = true);
            new PingSignal().ConfigureFilters<PingSignal>()
                .Require(new HasComponent<Rigidbody>())
                .Emit();
            Assert.IsTrue(called);
        }

        [Test]
        public void ConfigureFilters_NoRequire_EmitsNormally()
        {
            // _filters is null → SignalEmitOptions.Emit takes the no-filter code path
            bool called = false;
            Subscribe(_ => called = true);
            new PingSignal().ConfigureFilters<PingSignal>().Emit();
            Assert.IsTrue(called);
        }

        [Test]
        public void ConfigureFilters_Require_ChainReturnsSameInstance()
        {
            var opts = new PingSignal().ConfigureFilters<PingSignal>();
            var returned = opts.Require(new AlwaysPassFilter());
            Assert.AreSame(opts, returned, "Require must return 'this' for fluent chaining.");
        }

        // ── Null filter validation ─────────────────────────────────────────────

        [Test]
        public void Require_NullFilter_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PingSignal().ConfigureFilters<PingSignal>().Require(null));
        }

        [Test]
        public void NullElementInEmitParams_ThrowsArgumentNullException()
        {
            // EmitFilteredCore validates each element; null entry throws ArgumentNullException.
            // At least one subscriber is required to reach the filter loop.
            Subscribe(_ => { });
            Assert.Throws<ArgumentNullException>(() =>
                SignalBus.Emit(new PingSignal(), (ISignalFilter)null));
        }

        // ── Filter exception isolation ─────────────────────────────────────────
        // EmitFilteredCore catches filter exceptions and treats them as false
        // (subscriber skipped); the exception does not propagate to the caller.

        [Test]
        public void Filter_ThrowingException_TreatedAsFalse_SubscriberSkipped()
        {
            bool called = false;
            Subscribe(_ => called = true);
            Assert.DoesNotThrow(() => SignalBus.Emit(new PingSignal(), new ThrowingFilter()));
            Assert.IsFalse(called, "Exception in filter must be caught and treated as block.");
        }

        // ── Custom ISignalFilter ───────────────────────────────────────────────

        [Test]
        public void CustomFilter_AlwaysPass_SubscriberReceives()
        {
            bool called = false;
            Subscribe(_ => called = true);
            SignalBus.Emit(new PingSignal(), new AlwaysPassFilter());
            Assert.IsTrue(called);
        }

        [Test]
        public void CustomFilter_AlwaysBlock_SubscriberSkipped()
        {
            bool called = false;
            Subscribe(_ => called = true);
            SignalBus.Emit(new PingSignal(), new AlwaysBlockFilter());
            Assert.IsFalse(called);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private class FilterTestBehaviour : MonoBehaviour { }

        private class AlwaysPassFilter  : ISignalFilter { public bool Evaluate(MonoBehaviour _) => true; }
        private class AlwaysBlockFilter : ISignalFilter { public bool Evaluate(MonoBehaviour _) => false; }

        private class ThrowingFilter : ISignalFilter
        {
            public bool Evaluate(MonoBehaviour _) => throw new Exception("filter error");
        }
    }
}
