using NUnit.Framework;
using UnityEngine;

namespace Tagtag.UI.Tests
{
    public sealed class PaperCameraFlowTests
    {
        [Test]
        public void StickHidesBottomNavigation()
        {
            AppState state = new AppState { page = AppPage.Home };
            Assert.IsTrue(PaperFlow.ShowBottomNavigation(state));
            state.page = AppPage.Stick;
            Assert.IsFalse(PaperFlow.ShowBottomNavigation(state));
            state.accountOpen = true;
            Assert.IsTrue(PaperFlow.ShowBottomNavigation(state));
        }

        [Test]
        public void PlacementGuidanceNamesTrackingSurfaceTapAndAdjustmentPhases()
        {
            AppState state = new AppState { page = AppPage.Stick, selectedPreset = "taggi-1" };
            Assert.AreEqual("Move slowly to start tracking.",
                PaperFlow.StickPlacement(state, false, false, false, false).Guidance);
            Assert.AreEqual("Scan a wall or table for a surface.",
                PaperFlow.StickPlacement(state, true, false, false, false).Guidance);
            Assert.AreEqual("Surface found. Tap it to place Taggi.",
                PaperFlow.StickPlacement(state, true, true, false, false).Guidance);
            PaperStickState placed = PaperFlow.StickPlacement(state, true, true, true, false);
            Assert.AreEqual("Place Sticker", placed.Title);
            StringAssert.Contains("Pinch to resize. Twist to rotate.", placed.Guidance);
            Assert.IsTrue(placed.CanWriteNote);
            Assert.AreEqual("Taggi pose 1", PaperFlow.PresetName("taggi-1"));
        }

        [Test]
        public void NoteNeedsPlacedPreviewExceptForAnExistingPublishRetry()
        {
            AppState state = new AppState { page = AppPage.Stick, selectedPreset = "taggi-2" };
            Assert.IsFalse(PaperFlow.StickPlacement(state, true, true, false, false).CanWriteNote);
            state.hasPendingPublication = true;
            Assert.IsTrue(PaperFlow.StickPlacement(state, false, false, false, false).CanWriteNote);
            state.busy = true;
            Assert.IsFalse(PaperFlow.StickPlacement(state, true, true, true, false).CanWriteNote);
            state.busy = false;
            Assert.IsFalse(PaperFlow.StickPlacement(state, true, true, true, true).CanWriteNote);
        }

        [Test]
        public void DiscoveryRetainsTheSelectedClueAndRetryUntilAPlacementIsChosen()
        {
            AppState state = new AppState { page = AppPage.Stick,
                selected = new StickerSummary { place = "The red bridge", teaser = "Look beside the bench." } };
            StringAssert.Contains("The red bridge", PaperFlow.DiscoveryGuidance(state.selected));
            StringAssert.Contains("Look beside the bench.", PaperFlow.DiscoveryGuidance(state.selected));
            Assert.IsTrue(PaperFlow.ShowDiscoveryRetry(state));
            state.selectedPreset = "taggi-1";
            Assert.IsFalse(PaperFlow.ShowDiscoveryRetry(state));
        }

        [Test]
        public void CameraInteractionBlocksSheetsBusyAccountsAndOpaqueCamera()
        {
            AppState state = new AppState { page = AppPage.Stick };
            Assert.IsFalse(PaperFlow.BlockCameraInteraction(state, false, CameraPresentationState.Live));
            Assert.IsTrue(PaperFlow.BlockCameraInteraction(state, true, CameraPresentationState.Live));
            state.busy = true;
            Assert.IsTrue(PaperFlow.BlockCameraInteraction(state, false, CameraPresentationState.Live));
            state.busy = false;
            state.accountOpen = true;
            Assert.IsTrue(PaperFlow.BlockCameraInteraction(state, false, CameraPresentationState.Live));
            state.accountOpen = false;
            Assert.IsTrue(PaperFlow.BlockCameraInteraction(state, false, CameraPresentationState.Preparing));
        }

        [Test]
        public void ShortTapPlacesButDragReturnLongPressAndSecondContactDoNot()
        {
            var tap = new PaperSurfaceTap();
            tap.Begin(1, Vector2.zero, 0f);
            Assert.IsTrue(tap.End(1, new Vector2(6f, 4f), .2f));
            tap.Begin(1, Vector2.zero, 0f);
            tap.Move(1, new Vector2(20f, 0f));
            Assert.IsFalse(tap.End(1, Vector2.zero, .2f));
            tap.Begin(1, Vector2.zero, 0f);
            Assert.IsFalse(tap.End(1, Vector2.zero, .6f));
            tap.Begin(1, Vector2.zero, 0f);
            tap.Begin(2, Vector2.one, .1f);
            Assert.IsFalse(tap.End(1, Vector2.zero, .2f));
            tap.Begin(1, Vector2.zero, 0f);
            tap.Cancel();
            Assert.IsFalse(tap.End(1, Vector2.zero, .2f));
        }

        [Test]
        public void ThirdContactCannotBecomeFreshTapWhileSecondContactRemainsDown()
        {
            var tap = new PaperSurfaceTap();
            tap.Begin(1, Vector2.zero, 0f);
            tap.Begin(2, Vector2.one, .1f);
            Assert.IsFalse(tap.End(1, Vector2.zero, .2f));
            tap.CancelContact(1); // ordinary capture-out after release
            tap.Begin(3, Vector2.one, .25f);
            Assert.IsFalse(tap.End(3, Vector2.one, .3f));
            Assert.IsFalse(tap.End(2, Vector2.one, .35f));
            tap.Begin(4, Vector2.zero, .4f);
            Assert.IsTrue(tap.End(4, Vector2.zero, .5f));
        }

        [Test]
        public void TrackingLossBlocksCameraInteractionDespiteLiveImagery()
        {
            AppState state = new AppState { page = AppPage.Stick };
            Assert.IsTrue(PaperFlow.BlockCameraInteraction(state, false,
                CameraPresentationState.Live, false));
        }

        [Test]
        public void OneFingerMovesAndTwoFingersPinchAndTwist()
        {
            var gesture = new PaperSurfaceGesture();
            Assert.IsTrue(gesture.Begin(1, new Vector2(10f, 10f)));
            float widthCm = 20f, rotation = 0f;
            Assert.IsTrue(gesture.Move(1, new Vector2(15f, 10f), ref widthCm, ref rotation, out Vector2 center));
            Assert.IsTrue(gesture.MovesPlacement);
            Assert.AreEqual(new Vector2(15f, 10f), center);
            Assert.IsTrue(gesture.Begin(2, new Vector2(25f, 10f)));
            Assert.IsTrue(gesture.Move(2, new Vector2(15f, 30f), ref widthCm, ref rotation, out center));
            Assert.IsFalse(gesture.MovesPlacement);
            Assert.AreEqual(40f, widthCm, .01f);
            Assert.AreEqual(90f, rotation, .01f);
            Assert.IsTrue(gesture.End(1));
            gesture.Cancel();
            Assert.AreEqual(0, gesture.Count);
        }

        [Test]
        public void UnexpectedCaptureLossCancelsWholeAdjustmentButNormalReleaseKeepsRemainingFinger()
        {
            var gesture = new PaperSurfaceGesture();
            float widthCm = 20f, rotation = 0f;
            Assert.IsTrue(gesture.Begin(1, Vector2.zero));
            Assert.IsTrue(gesture.Begin(2, new Vector2(20f, 0f)));
            gesture.CaptureOut(1);
            Assert.IsFalse(gesture.Move(2, new Vector2(25f, 0f), ref widthCm, ref rotation, out _));
            Assert.AreEqual(0, gesture.Count);

            Assert.IsTrue(gesture.Begin(1, Vector2.zero));
            Assert.IsTrue(gesture.Begin(2, new Vector2(20f, 0f)));
            Assert.IsTrue(gesture.End(1)); // normal PointerUp precedes capture-out
            gesture.CaptureOut(1);
            Assert.IsTrue(gesture.Move(2, new Vector2(25f, 0f), ref widthCm, ref rotation, out _));
            Assert.IsTrue(gesture.MovesPlacement);
        }

        [Test]
        public void PanelCoordinatesConvertToBottomLeftScreenPixels()
        {
            Rect panel = new Rect(5f, 10f, 390f, 844f);
            Vector2 point = PaperCameraBounds.ToScreenPoint(new Vector2(200f, 432f), panel, 780, 1688);
            Assert.AreEqual(new Vector2(390f, 844f), point);
            Rect camera = PaperCameraBounds.ToScreenRect(new Rect(5f, 100f, 390f, 600f), panel, 780, 1688);
            Assert.AreEqual(new Rect(0f, 308f, 780f, 1200f), camera);
        }
    }
}
