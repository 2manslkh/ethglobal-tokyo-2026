using NUnit.Framework;
using UnityEngine.XR.ARFoundation;

namespace Tagtag.AR.Tests
{
    public sealed class CameraPresentationTests
    {
        [Test]
        public void FirstFrameMustBeDisplayableBeforeCameraBecomesLive()
        {
            var presentation = new CameraPresentation();
            presentation.Begin(1d);
            presentation.PermissionGranted(1d);
            presentation.ObserveSession(ARSessionState.SessionInitializing);
            Assert.AreEqual(CameraPresentationState.Preparing, presentation.State);
            Assert.IsFalse(CameraPresentation.CanDisplayFrame(0, true, true, true, true));
            Assert.IsFalse(CameraPresentation.CanDisplayFrame(1, false, true, true, true));
            Assert.IsFalse(CameraPresentation.CanDisplayFrame(1, true, false, true, true));
            Assert.IsFalse(CameraPresentation.CanDisplayFrame(1, true, true, false, true));
            Assert.IsFalse(CameraPresentation.CanDisplayFrame(1, true, true, true, false));
            Assert.IsTrue(CameraPresentation.CanDisplayFrame(1, true, true, true, true));
            presentation.ObserveFrame(1.1d, false);
            Assert.AreEqual(CameraPresentationState.Preparing, presentation.State);
            presentation.ObserveFrame(1.2d, true);
            Assert.AreEqual(CameraPresentationState.Live, presentation.State);
        }

        [Test]
        public void TrackingLossKeepsFreshImageryVisible()
        {
            var presentation = LivePresentation();
            presentation.ObserveSession(ARSessionState.SessionInitializing);
            presentation.Tick(1.8d);
            Assert.AreEqual(CameraPresentationState.Live, presentation.State);
        }

        [Test]
        public void LostCameraFramesRestoreCoverAndNewFrameRecovers()
        {
            var presentation = LivePresentation();
            presentation.Tick(3d);
            Assert.AreEqual(CameraPresentationState.Interrupted, presentation.State);
            presentation.ObserveFrame(3.1d, true);
            Assert.AreEqual(CameraPresentationState.Live, presentation.State);
        }

        [Test]
        public void PermissionAndHardwareFailuresHaveDistinctStates()
        {
            var denied = new CameraPresentation();
            denied.Begin(0d);
            denied.PermissionDenied();
            Assert.AreEqual(CameraPresentationState.PermissionDenied, denied.State);
            denied.ObserveFrame(1d, true);
            Assert.AreEqual(CameraPresentationState.PermissionDenied, denied.State);

            var unsupported = new CameraPresentation();
            unsupported.Begin(0d);
            unsupported.ObserveSession(ARSessionState.Unsupported);
            Assert.AreEqual(CameraPresentationState.Unavailable, unsupported.State);

            var installationNeeded = new CameraPresentation();
            installationNeeded.Begin(0d);
            installationNeeded.PermissionGranted(0d);
            installationNeeded.ObserveSession(ARSessionState.NeedsInstall);
            Assert.AreEqual(CameraPresentationState.Unavailable, installationNeeded.State);
        }

        [Test]
        public void StartupTimeoutReportsFailureWithoutAFrame()
        {
            var presentation = new CameraPresentation();
            presentation.Begin(0d);
            presentation.PermissionGranted(0d);
            presentation.ObserveSession(ARSessionState.SessionTracking);
            presentation.Tick(16d);
            Assert.AreEqual(CameraPresentationState.Failed, presentation.State);
        }

        [Test]
        public void ExitPauseResumeAndRestartRequireFreshFrame()
        {
            var presentation = LivePresentation();
            presentation.Interrupt();
            Assert.AreEqual(CameraPresentationState.Interrupted, presentation.State);
            presentation.Begin(4d);
            Assert.AreEqual(CameraPresentationState.Preparing, presentation.State);
            presentation.PermissionGranted(4d);
            presentation.ObserveSession(ARSessionState.SessionTracking);
            presentation.ObserveFrame(4.1d, true);
            Assert.AreEqual(CameraPresentationState.Live, presentation.State);
            presentation.Begin(5d);
            Assert.AreEqual(CameraPresentationState.Preparing, presentation.State);
            presentation.Exit();
            Assert.AreEqual(CameraPresentationState.Inactive, presentation.State);
            presentation.ObserveFrame(5.1d, true);
            Assert.AreEqual(CameraPresentationState.Inactive, presentation.State);
        }

        [Test]
        public void WorldMapResetHidesPreviousFrameUntilNewImageryArrives()
        {
            var presentation = LivePresentation();
            presentation.ResetFrame(2d);
            Assert.AreEqual(CameraPresentationState.Preparing, presentation.State);
            presentation.Tick(2.5d);
            Assert.AreEqual(CameraPresentationState.Preparing, presentation.State);
            presentation.ObserveFrame(2.6d, true);
            Assert.AreEqual(CameraPresentationState.Live, presentation.State);
        }

        [Test]
        public void PermissionDialogFocusReturnWaitsForPauseReturn()
        {
            var suspension = new CameraSuspension();
            Assert.IsFalse(suspension.IsSuspended);
            suspension.SetFocused(false);
            suspension.SetPaused(true);
            Assert.IsTrue(suspension.IsSuspended);
            suspension.SetFocused(true);
            Assert.IsTrue(suspension.IsSuspended);
            suspension.SetPaused(false);
            Assert.IsFalse(suspension.IsSuspended);
        }

        [Test]
        public void PermissionDialogPauseReturnWaitsForFocusReturn()
        {
            var suspension = new CameraSuspension();
            suspension.SetPaused(true);
            suspension.SetFocused(false);
            suspension.SetPaused(false);
            Assert.IsTrue(suspension.IsSuspended);
            suspension.SetFocused(true);
            Assert.IsFalse(suspension.IsSuspended);
        }

        private static CameraPresentation LivePresentation()
        {
            var presentation = new CameraPresentation();
            presentation.Begin(1d);
            presentation.PermissionGranted(1d);
            presentation.ObserveSession(ARSessionState.SessionTracking);
            presentation.ObserveFrame(1.1d, true);
            return presentation;
        }
    }
}
