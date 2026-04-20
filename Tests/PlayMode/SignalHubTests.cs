using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NekoSignal.Tests
{
    [TestFixture]
    public class SignalHubTests
    {
        private GameObject _go;
        private HubTestReceiver _receiver;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HubTest");
            _receiver = _go.AddComponent<HubTestReceiver>();
            PriorityHubReceiver.Order.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_receiver && SignalHub.IsBound(_receiver))
                SignalHub.Unbind(_receiver);
            Object.Destroy(_go);
            SignalBroadcaster.UnsubscribeAll();
        }

        // ── Core routing ─────────────────────────────────────────────────────

        [Test]
        public void Bind_RoutesSignalToAttributeMethod()
        {
            SignalHub.Bind(_receiver);
            SignalBus.Emit(new PingSignal { Id = 7 });
            Assert.AreEqual(7, _receiver.LastId);
        }

        [Test]
        public void Unbind_StopsRouting()
        {
            SignalHub.Bind(_receiver);
            SignalHub.Unbind(_receiver);
            SignalBus.Emit(new PingSignal { Id = 99 });
            Assert.AreEqual(0, _receiver.LastId);
        }

        [Test]
        public void IsBound_ReflectsCurrentState()
        {
            Assert.IsFalse(SignalHub.IsBound(_receiver));
            SignalHub.Bind(_receiver);
            Assert.IsTrue(SignalHub.IsBound(_receiver));
            SignalHub.Unbind(_receiver);
            Assert.IsFalse(SignalHub.IsBound(_receiver));
        }

        // ── Idempotency ──────────────────────────────────────────────────────

        [Test]
        public void DoubleBind_IsIdempotent()
        {
            SignalHub.Bind(_receiver);
            SignalHub.Bind(_receiver);
            SignalBus.Emit(new PingSignal { Id = 1 });
            Assert.AreEqual(1, _receiver.CallCount, "Handler must be invoked exactly once.");
        }

        // ── Null safety ───────────────────────────────────────────────────────

        [Test]
        public void Bind_Null_DoesNotThrow()
            => Assert.DoesNotThrow(() => SignalHub.Bind(null));

        [Test]
        public void Unbind_Null_DoesNotThrow()
            => Assert.DoesNotThrow(() => SignalHub.Unbind(null));

        // ── Priority from attribute ───────────────────────────────────────────

        [Test]
        public void AttributePriority_ControlsDispatchOrder()
        {
            var go2 = new GameObject("PriorityHub");
            var r = go2.AddComponent<PriorityHubReceiver>();
            SignalHub.Bind(r);
            SignalBus.Emit(new PingSignal());
            CollectionAssert.AreEqual(new[] { 10, 1 }, PriorityHubReceiver.Order,
                "Higher-priority [OnSignal] method must be invoked first.");
            SignalHub.Unbind(r);
            Object.Destroy(go2);
        }

        // ── Multiple [OnSignal] attributes on same method ─────────────────────
        // AllowMultiple = true: each attribute creates a separate subscription.

        [Test]
        public void MultipleAttributes_OnSameMethod_InvokedMultipleTimes()
        {
            var go2 = new GameObject("MultiAttrHub");
            var r = go2.AddComponent<MultiAttrReceiver>();
            SignalHub.Bind(r);
            SignalBus.Emit(new PingSignal());
            Assert.AreEqual(2, r.CallCount, "Method with 2 [OnSignal] attributes must fire twice.");
            SignalHub.Unbind(r);
            Object.Destroy(go2);
        }

        // ── Explicit signal type on attribute ─────────────────────────────────

        [Test]
        public void ExplicitTypeAttribute_RoutesCorrectly()
        {
            var go2 = new GameObject("ExplicitTypeHub");
            var r = go2.AddComponent<ExplicitTypeReceiver>();
            SignalHub.Bind(r);
            SignalBus.Emit(new PingSignal { Id = 5 });
            Assert.AreEqual(5, r.LastId);
            SignalHub.Unbind(r);
            Object.Destroy(go2);
        }

        // ── Invalid method signature ──────────────────────────────────────────
        // DiscoverHandlers logs an error and skips methods with wrong param count.

        [Test]
        public void InvalidMethod_WrongParamCount_SkippedGracefully()
        {
            var go2 = new GameObject("InvalidMethodHub");
            var r = go2.AddComponent<InvalidMethodReceiver>();
            Assert.DoesNotThrow(() => SignalHub.Bind(r));
            Assert.DoesNotThrow(() => SignalBus.Emit(new PingSignal()));
            Assert.AreEqual(0, r.CallCount, "Invalid method must be silently skipped.");
            if (SignalHub.IsBound(r)) SignalHub.Unbind(r);
            Object.Destroy(go2);
        }

        // ── Unbind then re-Bind ───────────────────────────────────────────────

        [Test]
        public void UnbindThenBind_ResubscribesCorrectly()
        {
            SignalHub.Bind(_receiver);
            SignalHub.Unbind(_receiver);
            SignalHub.Bind(_receiver);
            SignalBus.Emit(new PingSignal { Id = 3 });
            Assert.AreEqual(3, _receiver.LastId);
            Assert.AreEqual(1, _receiver.CallCount, "After rebind, must fire exactly once.");
        }

        // ── Inherited [OnSignal] methods ──────────────────────────────────────
        // DiscoverHandlers uses BindingFlags without DeclaredOnly and
        // GetCustomAttributes(inherit: true), so base-class handlers are found.

        [Test]
        public void InheritedOnSignal_DiscoveredOnDerivedType()
        {
            var go2 = new GameObject("InheritedHub");
            var r = go2.AddComponent<DerivedHubReceiver>();
            SignalHub.Bind(r);
            SignalBus.Emit(new PingSignal { Id = 7 });
            Assert.AreEqual(7, r.LastId, "Inherited [OnSignal] method must be discovered.");
            SignalHub.Unbind(r);
            Object.Destroy(go2);
        }
    }

    // ── Fixture helpers ───────────────────────────────────────────────────────

    internal class HubTestReceiver : MonoBehaviour
    {
        public int LastId;
        public int CallCount;

        [OnSignal]
        private void HandlePing(PingSignal s) { LastId = s.Id; CallCount++; }
    }

    internal class PriorityHubReceiver : MonoBehaviour
    {
        public static readonly List<int> Order = new();
        [OnSignal(10)] private void High(PingSignal _) => Order.Add(10);
        [OnSignal(1)]  private void Low(PingSignal _)  => Order.Add(1);
    }

    internal class MultiAttrReceiver : MonoBehaviour
    {
        public int CallCount;
        [OnSignal]
        [OnSignal]
        private void Handle(PingSignal _) => CallCount++;
    }

    internal class ExplicitTypeReceiver : MonoBehaviour
    {
        public int LastId;
        [OnSignal(typeof(PingSignal))]
        private void Handle(PingSignal s) => LastId = s.Id;
    }

    internal class InvalidMethodReceiver : MonoBehaviour
    {
        public int CallCount;
        [OnSignal]
        private void TooManyParams(PingSignal s1, PongSignal s2) => CallCount++;
    }

    internal class BaseHubReceiver : MonoBehaviour
    {
        public int LastId;
        [OnSignal]
        protected virtual void HandlePing(PingSignal s) => LastId = s.Id;
    }

    internal class DerivedHubReceiver : BaseHubReceiver { }
}
