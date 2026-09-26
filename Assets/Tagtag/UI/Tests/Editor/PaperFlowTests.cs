using NUnit.Framework;
using UnityEngine;

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
        public void ScanProgressFollowsObservedArStagesWithoutUsingLocationAsReadiness()
        {
            Assert.AreEqual(0, PaperScan.StageIndex(PlacementScanState.FindingSurface));
            Assert.AreEqual(1, PaperScan.StageIndex(PlacementScanState.SurfaceReady));
            Assert.AreEqual(2, PaperScan.StageIndex(PlacementScanState.Placed));
            Assert.AreEqual(3, PaperScan.StageIndex(PlacementScanState.Ready));
            Assert.AreEqual("Scan ready", PaperScan.Label(PlacementScanState.Ready));
        }

        [Test]
        public void DesignFailuresStayOutOfGeneralNoticeAndNoteFlow()
        {
            AppState state = new AppState { page = AppPage.Stick, designError = "Could not save design." };
            Assert.AreEqual("", PaperFlow.StatusMessage(state, false));
            state.error = "Could not publish sticker.";
            Assert.AreEqual("Could not publish sticker.", PaperFlow.StatusMessage(state, false));
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
        public void PublishNoticeNamesThePrerequisiteThatKeepsTheButtonDisabled()
        {
            Assert.AreEqual("Still needed: place, clue, and note.",
                PaperFlow.PublishNotice("", "", "", false, false, false, false, false, false));
            Assert.AreEqual("Move slowly until AR tracking is stable.",
                PaperFlow.PublishNotice("Place", "Clue", "Note", false, true, false, false, false, false));
            Assert.AreEqual("Place Taggi on a tracked surface before publishing.",
                PaperFlow.PublishNotice("Place", "Clue", "Note", true, false, false, false, false, false));
            Assert.AreEqual("Keep Taggi visible until its surface anchor is tracked.",
                PaperFlow.PublishNotice("Place", "Clue", "Note", true, true, false, false, false, false));
            Assert.AreEqual("Scan around Taggi from more angles until the spatial map is ready.",
                PaperFlow.PublishNotice("Place", "Clue", "Note", true, true, true, false, false, false));
            Assert.AreEqual("Ready to publish. Location is checked after you tap.",
                PaperFlow.PublishNotice("Place", "Clue", "Note", true, true, true, true, false, false));
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

        [Test]
        public void EmptyHomeInvitationDoesNotRepeatAsStatusButOtherNoticesRemain()
        {
            AppState state = new AppState { page = AppPage.Home,
                status = "Your next little discovery is out there." };
            Assert.AreEqual("", PaperFlow.StatusMessage(state, true));
            Assert.AreEqual(state.status, PaperFlow.StatusMessage(state, false));
            state.status = "Find your places, Collect your moments";
            Assert.AreEqual("", PaperFlow.StatusMessage(state, true));
            state.status = "Nearby stickers updated";
            Assert.AreEqual(state.status, PaperFlow.StatusMessage(state, true));
            state.error = "Could not load your book";
            Assert.AreEqual(state.error, PaperFlow.StatusMessage(state, true));
        }

        [Test]
        public void NearbyReadStateDrivesExploreMessagesAndRetryWithoutBlockingDiscovery()
        {
            AppState state = new AppState { page = AppPage.Explore, nearbyLoading = true,
                servicesConfigured = true };
            NearbyStatus loading = PaperFlow.Nearby(state, true);
            Assert.AreEqual("Looking for nearby stickers", loading.EmptyTitle);
            Assert.AreEqual("Finding your location…", loading.MapMessage);
            Assert.AreEqual("", loading.LocationNotice);
            Assert.IsFalse(loading.CanRefresh);

            state.nearbyLoading = false;
            state.busy = true;
            NearbyStatus writing = PaperFlow.Nearby(state, true);
            Assert.AreEqual("No stickers in view yet", writing.EmptyTitle);
            Assert.AreEqual("Location is unavailable.", writing.MapMessage);
            Assert.AreEqual("Location is not ready yet. Try refreshing nearby.", writing.LocationNotice);
            Assert.IsTrue(writing.CanRefresh);

            state.error = "Location access was denied.";
            Assert.AreEqual("", PaperFlow.Nearby(state, true).LocationNotice);
            state.error = "";
            state.location = new LocationFix { accuracyMeters = 0f };
            Assert.AreEqual("Location is not ready yet. Try refreshing nearby.", PaperFlow.Nearby(state, true).LocationNotice);
            state.location.accuracyMeters = 10f;
            Assert.AreEqual("", PaperFlow.Nearby(state, true).LocationNotice);

            state.nearby.Add(new StickerSummary { id = "river" });
            Assert.AreEqual("Tap a sticker on the map", PaperFlow.Nearby(state, true).EmptyTitle);
            Assert.AreEqual("Map is unavailable on this device.", PaperFlow.Nearby(state, false).MapMessage);
            state.servicesConfigured = false;
            Assert.AreEqual("Nearby stickers need a configured service.", PaperFlow.Nearby(state, true).LocationNotice);
        }

        [Test]
        public void ExploreLabelsApproximateLocationWithoutHidingMap()
        {
            var state = new AppState { servicesConfigured = true,
                location = new LocationFix { accuracyMeters = 500 } };
            NearbyStatus result = PaperFlow.Nearby(state, true);
            Assert.That(result.MapMessage, Is.Empty);
            Assert.That(result.LocationNotice, Does.Contain("Approximate location"));
            Assert.That(result.CanRefresh, Is.True);
        }

        [Test]
        public void NavigationUsesDedicatedTaggiArtworkResources()
        {
            Assert.AreEqual("Tagtag/Navigation/home", PaperNavigationArt.ResourcePath(AppPage.Home));
            Assert.AreEqual("Tagtag/Navigation/stick", PaperNavigationArt.ResourcePath(AppPage.Stick));
            Assert.AreEqual("Tagtag/Navigation/explore", PaperNavigationArt.ResourcePath(AppPage.Explore));
            Assert.IsNotNull(Resources.Load<Texture2D>(PaperNavigationArt.ResourcePath(AppPage.Home)));
            Assert.IsNotNull(Resources.Load<Texture2D>(PaperNavigationArt.ResourcePath(AppPage.Stick)));
            Assert.IsNotNull(Resources.Load<Texture2D>(PaperNavigationArt.ResourcePath(AppPage.Explore)));
        }
    }
}
