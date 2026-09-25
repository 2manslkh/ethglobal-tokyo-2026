using NUnit.Framework;

namespace Tagtag.UI.Tests
{
    public sealed class PaperFlowTests
    {
        [Test]
        public void CameraCoverRemainsOpaqueUntilCameraReportsLiveImagery()
        {
            Assert.IsTrue(PaperFlow.Camera(CameraPresentationState.Inactive).Cover);
            Assert.IsTrue(PaperFlow.Camera(CameraPresentationState.Preparing).Cover);
            Assert.IsTrue(PaperFlow.Camera(CameraPresentationState.Interrupted).Cover);
            Assert.IsFalse(PaperFlow.Camera(CameraPresentationState.Live).Cover);
            Assert.AreEqual("Getting the camera ready", PaperFlow.Camera(CameraPresentationState.Preparing).Title);
        }

        [Test]
        public void CameraFailureOffersTheRecoveryMatchingItsCause()
        {
            Assert.AreEqual(CameraRecovery.Settings, PaperFlow.Camera(CameraPresentationState.PermissionDenied).Recovery);
            Assert.AreEqual(CameraRecovery.Retry, PaperFlow.Camera(CameraPresentationState.Failed).Recovery);
            Assert.AreEqual(CameraRecovery.Retry, PaperFlow.Camera(CameraPresentationState.Interrupted).Recovery);
            Assert.AreEqual(CameraRecovery.None, PaperFlow.Camera(CameraPresentationState.Unavailable).Recovery);
            Assert.AreEqual(CameraRecovery.None, PaperFlow.Camera(CameraPresentationState.Live).Recovery);
        }

        [Test]
        public void NoteReadinessKeepsIncompleteAndBusyDraftsFromPublishing()
        {
            Assert.IsFalse(PaperFlow.CanPresentPublish("Place", "Clue", "  ", false, false));
            Assert.IsFalse(PaperFlow.CanPresentPublish("Place", "Clue", "Note", true, true));
            Assert.IsTrue(PaperFlow.CanPresentPublish("Place", "Clue", "Note", true, false));
            Assert.IsTrue(PaperFlow.CanPresentPublish("Place", "Clue", "Note", false, false, true));
        }

        [Test]
        public void RepeatedActivationIsRejectedUntilTheFirstOperationCompletes()
        {
            PaperActivation activation = new PaperActivation();
            Assert.IsTrue(activation.TryBegin());
            Assert.IsFalse(activation.TryBegin());
            activation.End();
            Assert.IsTrue(activation.TryBegin());
        }

        [Test]
        public void RefreshDoesNotAdvanceNavigationHistoryOrReplayAnEntrance()
        {
            PaperNavigationMotion navigation = new PaperNavigationMotion();
            Assert.AreEqual(AppNavigationReason.Initial, navigation.Observe("Home", true));
            Assert.AreEqual(AppNavigationReason.Refresh, navigation.Observe("Home", true));
            Assert.AreEqual(AppNavigationReason.TopLevel, navigation.Observe("Explore", true));
            Assert.AreEqual(AppNavigationReason.Refresh, navigation.Observe("Explore", true));
        }
    }
}
