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

        [Test]
        public void ReturningToATabWithTheSameDataStillPopulatesItsNewHost()
        {
            PresenterCache cache = new PresenterCache();
            Assert.IsTrue(cache.NeedsRefresh("two-collected"));
            Assert.IsFalse(cache.NeedsRefresh("two-collected"));
            cache.Reset();
            Assert.IsTrue(cache.NeedsRefresh("two-collected"));
        }

        [Test]
        public void OnlyASuccessfulSubmittedPublicationClearsTheLocalDraft()
        {
            AppState state = new AppState { selectedPreset = "taggi-2", draftPlace = "River", draftTeaser = "Bridge", draftNote = "Look up" };
            Assert.IsFalse(PaperFlow.ShouldClearPublishedDraft(true, state));
            state.busy = true;
            state.selectedPreset = "";
            state.draftPlace = state.draftTeaser = state.draftNote = "";
            Assert.IsFalse(PaperFlow.ShouldClearPublishedDraft(true, state));
            state.busy = false;
            Assert.IsTrue(PaperFlow.ShouldClearPublishedDraft(true, state));
            state.error = "Cleanup failed";
            Assert.IsFalse(PaperFlow.ShouldClearPublishedDraft(true, state));
            state.error = "";
            Assert.IsFalse(PaperFlow.ShouldClearPublishedDraft(false, state));
        }

        [Test]
        public void ArtworkCommitRunsOnlyForANewCollectionFromStick()
        {
            AppState state = new AppState { page = AppPage.Home,
                detail = new CollectedSticker { id = "river" } };
            Assert.IsTrue(PaperFlow.ShouldAnimateCollection(AppPage.Stick, state, null));
            Assert.IsFalse(PaperFlow.ShouldAnimateCollection(AppPage.Home, state, null));
            Assert.IsFalse(PaperFlow.ShouldAnimateCollection(AppPage.Stick, state, "river"));
            state.detail = null;
            Assert.IsFalse(PaperFlow.ShouldAnimateCollection(AppPage.Stick, state, null));
        }
    }
}
