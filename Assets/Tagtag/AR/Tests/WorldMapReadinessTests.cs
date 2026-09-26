using NUnit.Framework;
using UnityEngine.XR.ARKit;

namespace Tagtag.AR.Tests
{
    public sealed class WorldMapReadinessTests
    {
        [TestCase(ARWorldMapRequestStatus.ErrorInsufficientFeatures)]
        [TestCase(ARWorldMapRequestStatus.ErrorUnknown)]
        [TestCase(ARWorldMapRequestStatus.ErrorBadData)]
        [TestCase(ARWorldMapRequestStatus.ErrorNotSupported)]
        [TestCase(ARWorldMapRequestStatus.Invalid)]
        [TestCase(ARWorldMapRequestStatus.Pending)]
        public void RejectedNativeRequestCannotEnableCaptureDespiteLiveReadiness(ARWorldMapRequestStatus status)
        {
            var readiness = new WorldMapReadiness();
            readiness.Observe(true, 1);
            int request = readiness.BeginValidation(0);
            readiness.CompleteValidation(request, status, new byte[] { 1 }, 1);
            Assert.That(readiness.TryGetMap(1, 1, out _), Is.False);
        }

        [Test]
        public void LiveMappingAloneDoesNotEnableCapture()
        {
            var readiness = new WorldMapReadiness();
            readiness.Observe(true, 1);
            Assert.That(readiness.TryGetMap(1, 0, out _), Is.False);
            int request = readiness.BeginValidation(0);
            readiness.CompleteValidation(request, ARWorldMapRequestStatus.Success, null, 1);
            Assert.That(readiness.TryGetMap(1, 1, out _), Is.False);
            Assert.That(readiness.CanValidate(2), Is.False);
            Assert.That(readiness.CanValidate(3), Is.True);
        }

        [Test]
        public void CaptureUsesTheSuccessfullyValidatedMapUntilItExpires()
        {
            var readiness = new WorldMapReadiness();
            readiness.Observe(true, 1);
            int request = readiness.BeginValidation(0);
            var bytes = new byte[] { 1, 2, 3 };
            readiness.CompleteValidation(request, ARWorldMapRequestStatus.Success, bytes, 1);
            Assert.That(readiness.TryGetMap(1, 1, out var map), Is.True);
            Assert.That(map, Is.SameAs(bytes), "Capture must reuse the validated map, not request another one.");
            Assert.That(readiness.CanValidate(6), Is.True);
            Assert.That(readiness.TryGetMap(1, 11, out _), Is.False);
        }

        [Test]
        public void ChangedPlacementRejectsOldMapAndLateValidation()
        {
            var readiness = new WorldMapReadiness();
            readiness.Observe(true, 1);
            int request = readiness.BeginValidation(0);
            readiness.Observe(true, 2);
            readiness.CompleteValidation(request, ARWorldMapRequestStatus.Success, new byte[] { 1 }, 1);
            Assert.That(readiness.TryGetMap(2, 1, out _), Is.False);
            request = readiness.BeginValidation(1);
            readiness.CompleteValidation(request, ARWorldMapRequestStatus.Success, new byte[] { 2 }, 2);
            Assert.That(readiness.TryGetMap(3, 2, out _), Is.False);
            Assert.That(readiness.TryGetMap(2, 2, out _), Is.True);
        }

        [Test]
        public void TrackingLossRequiresNewValidationEvenAfterTrackingReturns()
        {
            var readiness = new WorldMapReadiness();
            readiness.Observe(true, 1);
            int request = readiness.BeginValidation(0);
            readiness.CompleteValidation(request, ARWorldMapRequestStatus.Success, new byte[] { 1 }, 1);
            readiness.Observe(false, 1);
            readiness.Observe(true, 1);
            Assert.That(readiness.TryGetMap(1, 2, out _), Is.False);
            Assert.That(readiness.CanValidate(2), Is.True);
        }

        [Test]
        public void FailedRefreshRevokesPreviousReadiness()
        {
            var readiness = new WorldMapReadiness();
            readiness.Observe(true, 1);
            int request = readiness.BeginValidation(0);
            readiness.CompleteValidation(request, ARWorldMapRequestStatus.Success, new byte[] { 1 }, 1);
            request = readiness.BeginValidation(6);
            Assert.That(readiness.CanValidate(7), Is.False);
            readiness.CompleteValidation(request, ARWorldMapRequestStatus.Success, null, 7);
            Assert.That(readiness.TryGetMap(1, 7, out _), Is.False);
        }

        [Test]
        public void CancellationCannotBeUndoneByALateResult()
        {
            var readiness = new WorldMapReadiness();
            readiness.Observe(true, 1);
            int oldRequest = readiness.BeginValidation(0);
            readiness.Invalidate();
            readiness.Observe(true, 1);
            int newRequest = readiness.BeginValidation(1);
            readiness.CompleteValidation(oldRequest, ARWorldMapRequestStatus.Success, new byte[] { 1 }, 2);
            Assert.That(readiness.TryGetMap(1, 2, out _), Is.False);
            Assert.That(readiness.IsValidating, Is.True);
            readiness.CompleteValidation(newRequest, ARWorldMapRequestStatus.Success, new byte[] { 2 }, 3);
            Assert.That(readiness.TryGetMap(1, 3, out _), Is.True);
        }

        [TestCase(0)]
        [TestCase(WorldMapReadiness.MaxMapBytes + 1)]
        public void EmptyOrOversizedMapsCannotEnableCapture(int size)
        {
            var readiness = new WorldMapReadiness();
            readiness.Observe(true, 1);
            int request = readiness.BeginValidation(0);
            readiness.CompleteValidation(request, ARWorldMapRequestStatus.Success, new byte[size], 1);
            Assert.That(readiness.TryGetMap(1, 1, out _), Is.False);
        }
    }
}
