using System;
using NUnit.Framework;

namespace NekoSignal.Tests
{
    [TestFixture]
    public class OnSignalAttributeTests
    {
        // ── Constructor validation ────────────────────────────────────────────

        [Test]
        public void Constructor_NullType_ThrowsArgumentNullException()
            => Assert.Throws<ArgumentNullException>(() => new OnSignalAttribute(null));

        [Test]
        public void Constructor_NonISignalType_ThrowsArgumentException()
            => Assert.Throws<ArgumentException>(() => new OnSignalAttribute(typeof(string)));

        // ── Valid constructors ────────────────────────────────────────────────

        [Test]
        public void Constructor_ValidStructISignal_StoresType()
        {
            var attr = new OnSignalAttribute(typeof(PingSignal));
            Assert.AreEqual(typeof(PingSignal), attr.ExplicitSignalType);
            Assert.AreEqual(0, attr.Priority);
        }

        [Test]
        public void Constructor_PriorityOnly_StoresPriorityAndNullType()
        {
            var attr = new OnSignalAttribute(5);
            Assert.AreEqual(5, attr.Priority);
            Assert.IsNull(attr.ExplicitSignalType);
        }

        [Test]
        public void Constructor_TypeAndPriority_StoresBoth()
        {
            var attr = new OnSignalAttribute(typeof(PingSignal), priority: 3);
            Assert.AreEqual(typeof(PingSignal), attr.ExplicitSignalType);
            Assert.AreEqual(3, attr.Priority);
        }
    }
}
