using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.ARKit;

namespace Tagtag.AR.Tests
{
    public sealed class ArGatesTests
    {
        [Test]
        public void PublicationRequiresTrackedAnchorAndMappedSurface()
        {
            Assert.IsFalse(ArGates.CanPublish(true, true, false, true, false));
            Assert.IsFalse(ArGates.CanPublish(true, false, true, true, false));
            Assert.IsFalse(ArGates.CanPublish(false, true, true, true, false));
            Assert.IsTrue(ArGates.CanPublish(true, true, true, true, false));
        }

        [Test]
        public void ExtendingWorldMapAllowsValidation()
        {
            Assert.IsTrue(ArGates.CanSerializeWorldMap(ARWorldMappingStatus.Extending));
            Assert.IsTrue(ArGates.CanSerializeWorldMap(ARWorldMappingStatus.Mapped));
            Assert.IsFalse(ArGates.CanSerializeWorldMap(ARWorldMappingStatus.Limited));
            Assert.IsFalse(ArGates.CanSerializeWorldMap(ARWorldMappingStatus.NotAvailable));
        }

        [Test]
        public void TapRequiresRecoveredAnchorTrackingAndThreeMetres()
        {
            Assert.IsFalse(ArGates.CanCollect(true, false, true, 1f, true));
            Assert.IsFalse(ArGates.CanCollect(true, true, false, 1f, true));
            Assert.IsFalse(ArGates.CanCollect(true, true, true, 3.01f, true));
            Assert.IsFalse(ArGates.CanCollect(true, true, true, 1f, false));
            Assert.IsTrue(ArGates.CanCollect(true, true, true, 3f, true));
        }

        [Test]
        public void RecoveryNeedsSustainedMatchingFrames()
        {
            var gate = new RecoveryGate();
            for (var i = 0; i < 14; i++) Assert.IsFalse(gate.Observe(true, true, true, 0.1f));
            Assert.IsTrue(gate.Observe(true, true, true, 0.1f));
            Assert.IsFalse(gate.Observe(false, true, true, 0.1f));
            Assert.IsFalse(gate.Observe(true, true, true, 0.1f));
        }

        [Test]
        public void WorldMapEnvelopeRejectsTruncatedOrUnboundedPayloads()
        {
            Assert.IsFalse(WorldMapEnvelope.TryDecode(new byte[] { 1, 2, 3 }, out _, out _));
            var bytes = WorldMapEnvelope.Encode(4, 9, new byte[] { 1, 2, 3, 4 });
            Assert.IsTrue(WorldMapEnvelope.TryDecode(bytes, out var anchorId, out var map));
            Assert.AreEqual(4UL, anchorId.subId1);
            Assert.AreEqual(9UL, anchorId.subId2);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, map);
        }
    }
}
