using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.Services.Tests
{
    public sealed class PublishingLocationTests
    {
        private static readonly List<string> createdUserIds = new List<string>();

        [TearDown]
        public void RemoveTestDrafts()
        {
            foreach (string uid in createdUserIds)
            {
                new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications")).Remove(uid);
                RemoveEditable(uid);
            }
            createdUserIds.Clear();
        }

        [Test]
        public void AllTwelveDefaultStickersCanBeSelectedAndUnknownIdsAreIgnored()
        {
            var camera = new Camera();
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            try
            {
                for (int index = 1; index <= 12; index++)
                {
                    string id = "taggi-" + index;
                    controller.SelectPreset(id);
                    Assert.That(controller.State.selectedPreset, Is.EqualTo(id));
                    Assert.That(camera.SelectedPresetId, Is.EqualTo(id));
                }
                foreach (string invalid in new[] { null, "", "taggi-0", "taggi-13", "taggi-01", "TAGGI-1", "taggi-12-extra" })
                {
                    controller.SelectPreset(invalid);
                    Assert.That(controller.State.selectedPreset, Is.EqualTo("taggi-12"));
                    Assert.That(camera.SelectedPresetId, Is.EqualTo("taggi-12"));
                }
            }
            finally { controller.Dispose(); RemoveEditable(identity.UserId); }
        }

        [Test]
        public void PrecisePublicationDoesNotSerializeAnUnconfirmedMapPin()
        {
            string encoded = JsonUtility.ToJson(new PrepareRequest { location = new LocationFix
                { latitude = 35.68, longitude = 139.76, accuracyMeters = 20, measuredUnixSeconds = 100 } });
            Assert.That(encoded, Does.Contain("\"locationConfirmed\":false"));
        }

        [Test]
        public async Task StickCaptureRequiresReadyScanAndReusesSnapshotWhileEditingNote()
        {
            var camera = new Camera { Scan = PlacementScanState.Placed, SucceedCapture = true };
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-1");
                controller.CaptureSpot();
                Assert.That(camera.CaptureCount, Is.Zero, "A placed sticker still needs Scan ready.");

                camera.Scan = PlacementScanState.Ready;
                controller.CaptureSpot();
                await Task.Yield();
                Assert.That(camera.CaptureCount, Is.EqualTo(1));
                Assert.That(controller.State.hasCapturedSpot, Is.True);
                Assert.That(controller.State.hasPendingPublication, Is.False);
                Assert.That(controller.State.draftPlace, Is.EqualTo("Sticker spot"));
                Assert.That(controller.State.draftTeaser, Is.EqualTo("Find this sticker to read its note"));

                controller.SetDraft(controller.State.draftPlace, controller.State.draftTeaser, "A private note");
                Assert.That(controller.State.hasCapturedSpot, Is.True, "Editing text must retain the captured map and photo.");
                controller.State.error = "Old publish failure";
                controller.State.locationSettingsRequired = true;
                controller.CaptureSpot();
                Assert.That(camera.CaptureCount, Is.EqualTo(1), "Reopening the note must reuse the valid capture.");
                Assert.That(controller.State.error, Is.Empty);
                Assert.That(controller.State.locationSettingsRequired, Is.False);
            }
            finally { controller.Dispose(); RemoveEditable(identity.UserId); }
        }

        [Test]
        public async Task CaptureFailureAndDuplicateTapStayInCamera()
        {
            var camera = new Camera { HoldCapture = true };
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-1");
                controller.CaptureSpot();
                controller.CaptureSpot();
                Assert.That(camera.CaptureCount, Is.EqualTo(1));
                camera.FailCapture("Map failed");
                await Task.Yield();
                Assert.That(controller.State.hasCapturedSpot, Is.False);
                Assert.That(controller.State.page, Is.EqualTo(AppPage.Stick));
                Assert.That(controller.State.error, Does.Contain("Map failed"));
            }
            finally { controller.Dispose(); RemoveEditable(identity.UserId); }
        }

        [Test]
        public async Task LateCaptureAfterNavigationOrPlacementAdjustmentIsIgnored()
        {
            var camera = new Camera { HoldCapture = true };
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-1");
                controller.CaptureSpot();
                controller.Navigate(AppPage.Home);
                camera.CompleteCapture();
                await Task.Yield();
                Assert.That(controller.State.hasCapturedSpot, Is.False);

                controller.Navigate(AppPage.Stick);
                controller.CaptureSpot();
                camera.ChangePlacement();
                camera.CompleteCapture();
                await Task.Yield();
                Assert.That(controller.State.hasCapturedSpot, Is.False);
            }
            finally { controller.Dispose(); RemoveEditable(identity.UserId); }
        }

        [Test]
        public async Task CaptureCallbackAfterAccountSheetOrSuspensionIsIgnored()
        {
            var camera = new Camera { HoldCapture = true };
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            try
            {
                controller.SelectPreset("taggi-1");
                controller.CaptureSpot();
                controller.SetAccountOpen(true);
                camera.CompleteCapture();
                await Task.Yield();
                Assert.That(controller.State.hasCapturedSpot, Is.False);

                controller.SetAccountOpen(false);
                controller.CaptureSpot();
                controller.SetSuspended(true);
                camera.CompleteCapture();
                await Task.Yield();
                Assert.That(controller.State.hasCapturedSpot, Is.False);
                Assert.That(controller.State.capturingSpot, Is.False);
            }
            finally { controller.Dispose(); RemoveEditable(identity.UserId); }
        }

        [Test]
        public void PublishUsesCapturedSnapshotAfterLiveScanDrops()
        {
            var camera = new Camera { SucceedCapture = true };
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(new ReducedLocationRuntime()));
            try
            {
                controller.SelectPreset("taggi-1");
                controller.CaptureSpot();
                controller.SetDraft(controller.State.draftPlace, controller.State.draftTeaser, "My note");
                camera.CanPublishEnabled = false;
                camera.Scan = PlacementScanState.Placed;
                controller.Publish();
                Assert.That(camera.CaptureCount, Is.EqualTo(1));
                Assert.That(controller.State.locationSettingsRequired, Is.True,
                    "Publishing advanced to location permission without fresh scan tracking.");
            }
            finally { controller.Dispose(); RemoveEditable(identity.UserId); }
        }

        [Test]
        public void CancelledFirstPlacementStartsSecondDraftWithPublicDefaults()
        {
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), identity,
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            try
            {
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Old place", "Old teaser", "Private words");
                controller.CancelPlacement();
                controller.SelectPreset("taggi-2");
                Assert.That(controller.State.draftPlace, Is.EqualTo("Sticker spot"));
                Assert.That(controller.State.draftTeaser, Is.EqualTo("Find this sticker to read its note"));
                Assert.That(controller.State.draftNote, Is.Empty);
            }
            finally { controller.Dispose(); RemoveEditable(identity.UserId); }
        }

        [Test]
        public void RestoredPendingDraftCanEditNoteAndPublishFromItsSavedSnapshot()
        {
            var identity = new Identity();
            var saved = new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications"));
            saved.Save(identity.UserId, new PlacementDraft { operationId = "legacy-note-edit", presetId = "taggi-1",
                place = "Existing place", teaser = "Existing teaser", note = "Old private note",
                snapshot = new SpatialSnapshot { worldMapBase64 = "AQ==", widthMeters = .2f } });
            var camera = new Camera { CanPublishEnabled = false };
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(new ReducedLocationRuntime()));
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SetDraft("Existing place", "Existing teaser", "Revised private note");
                Assert.That(controller.State.hasPendingPublication, Is.False,
                    "Changing the payload requires a new operation ID.");
                Assert.That(controller.State.hasCapturedSpot, Is.True);
                Assert.That(controller.State.draftPlace, Is.EqualTo("Existing place"));
                Assert.That(controller.State.draftTeaser, Is.EqualTo("Existing teaser"));
                controller.Publish();
                Assert.That(camera.CaptureCount, Is.Zero, "The saved world map is reused after note editing.");
                Assert.That(controller.State.locationSettingsRequired, Is.True,
                    "Publish advanced to location permission without live AR tracking.");
            }
            finally { controller.Dispose(); saved.Remove(identity.UserId); RemoveEditable(identity.UserId); }
        }

        [Test]
        public void PartialLegacyEditableDraftGetsOnlyMissingPublicDefaults()
        {
            var identity = new Identity();
            var editable = new EditablePublication(System.IO.Path.Combine(Application.persistentDataPath, "editable-publications"));
            editable.Save(identity.UserId, new EditablePublicationDraft { presetId = "taggi-1",
                place = "   ", teaser = "Keep this public teaser", note = "Private note" });
            var controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), identity,
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            try
            {
                Assert.That(controller.State.draftPlace, Is.EqualTo("Sticker spot"));
                Assert.That(controller.State.draftTeaser, Is.EqualTo("Keep this public teaser"));
                Assert.That(controller.State.draftNote, Is.EqualTo("Private note"));
            }
            finally { controller.Dispose(); editable.Remove(identity.UserId); }

            editable.Save(identity.UserId, new EditablePublicationDraft { presetId = "taggi-2",
                place = "Existing place", teaser = "\t", note = "Another private note" });
            var second = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), identity,
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            try
            {
                Assert.That(second.State.draftPlace, Is.EqualTo("Existing place"));
                Assert.That(second.State.draftTeaser, Is.EqualTo("Find this sticker to read its note"));
                Assert.That(second.State.draftNote, Is.EqualTo("Another private note"));
            }
            finally { second.Dispose(); editable.Remove(identity.UserId); }
        }


        [Test]
        public void ReducedPrecisionIsReportedAfterCaptureBeforeSubmittingPublication()
        {
            var camera = new Camera { SucceedCapture = true };
            var runtime = new ReducedLocationRuntime();
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(runtime));
            try
            {
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Park", "Find Taggi", "Under the tree");
                controller.CaptureSpot();
                controller.Publish();

                Assert.That(camera.CaptureCount, Is.EqualTo(1));
                Assert.That(controller.State.locationSettingsRequired, Is.True);
                Assert.That(controller.State.status, Does.Not.Contain("Checking location access"));
                controller.Navigate(AppPage.Home);
                Assert.That(controller.State.locationSettingsRequired, Is.False);
            }
            finally
            {
                controller.Dispose();
                RemoveEditable(identity.UserId);
            }
        }

        [Test]
        public void SelectingAStickerPrewarmsOnceAndLeavingStickStopsUpdates()
        {
            var runtime = new TrackingLocationRuntime();
            var controller = Controller(runtime);
            try
            {
                controller.Navigate(AppPage.Stick);
                Assert.That(runtime.StartCount, Is.Zero, "Opening the camera alone does not need GPS.");

                controller.SelectPreset("taggi-1");
                controller.SelectPreset("taggi-1");
                Assert.That(runtime.StartCount, Is.EqualTo(1));
                Assert.That(runtime.UpdateDistanceMeters, Is.Zero);

                controller.Navigate(AppPage.Home);
                Assert.That(runtime.StopCount, Is.EqualTo(1));
            }
            finally { controller.Dispose(); }
        }

        [Test]
        public void CancelingPlacementStopsPrewarmedLocation()
        {
            var runtime = new TrackingLocationRuntime();
            var controller = Controller(runtime);
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-1");

                controller.CancelPlacement();

                Assert.That(runtime.StopCount, Is.EqualTo(1));
            }
            finally { controller.Dispose(); }
        }

        [Test]
        public void PauseStopsLocationAndResumeOnlyRestartsActivePlacement()
        {
            var runtime = new TrackingLocationRuntime();
            var controller = Controller(runtime);
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-1");
                controller.SetSuspended(true);
                Assert.That(runtime.StopCount, Is.EqualTo(1));

                controller.SetSuspended(false);
                Assert.That(runtime.StartCount, Is.EqualTo(2));

                controller.Navigate(AppPage.Home);
                controller.SetSuspended(true);
                controller.SetSuspended(false);
                Assert.That(runtime.StartCount, Is.EqualTo(2));
            }
            finally { controller.Dispose(); }
        }

        [Test]
        public async Task CaptureCallbackAfterDisposeCannotSaveDraftOrRestartLocation()
        {
            string uid = "late-capture-" + Guid.NewGuid().ToString("N");
            var runtime = new TrackingLocationRuntime();
            var camera = new Camera { HoldCapture = true };
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), new Identity(uid),
                deviceLocation: new DeviceLocation(runtime));
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Park", "Find Taggi", "Under the tree");
                controller.CaptureSpot();
                Assert.That(camera.CaptureCount, Is.EqualTo(1));

                controller.Dispose();
                int startsAtDispose = runtime.StartCount;
                camera.CompleteCapture();
                await Task.Yield();
                await Task.Yield();

                Assert.That(runtime.StartCount, Is.EqualTo(startsAtDispose));
                Assert.That(controller.State.hasPendingPublication, Is.False);
            }
            finally
            {
                controller.Dispose();
                new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications")).Remove(uid);
                RemoveEditable(uid);
            }
        }

        [Test]
        public void ColdRestartRestoresEditableTextAfterPrecisionSettingsFailure()
        {
            string uid = "settings-draft-" + Guid.NewGuid().ToString("N");
            var identity = new Identity(uid);
            var controller = new TagtagController(new ServiceConfiguration(), new Camera { SucceedCapture = true }, new Map(), identity,
                deviceLocation: new DeviceLocation(new ReducedLocationRuntime()));
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-2");
                controller.SetDraft("Garden", "Look by the gate", "The flowers are lovely.");
                controller.CaptureSpot();
                controller.Publish();
                Assert.That(controller.State.locationSettingsRequired, Is.True);
                Assert.That(controller.State.hasPendingPublication, Is.False);
                controller.Dispose();

                var restartedCamera = new Camera { CanPublishEnabled = false };
                var restarted = new TagtagController(new ServiceConfiguration(), restartedCamera, new Map(), identity,
                    deviceLocation: new DeviceLocation(new ReducedLocationRuntime()));
                try
                {
                    Assert.That(restarted.State.selectedPreset, Is.EqualTo("taggi-2"));
                    Assert.That(restarted.State.draftPlace, Is.EqualTo("Garden"));
                    Assert.That(restarted.State.draftTeaser, Is.EqualTo("Look by the gate"));
                    Assert.That(restarted.State.draftNote, Is.EqualTo("The flowers are lovely."));
                    Assert.That(restarted.State.hasPendingPublication, Is.False);
                    restarted.Navigate(AppPage.Stick);
                    Assert.That(restartedCamera.SelectedPresetId, Is.EqualTo("taggi-2"));
                    restarted.Publish();
                    Assert.That(restartedCamera.CaptureCount, Is.Zero,
                        "Restored editable text needs a new placement and map before publishing.");
                    Assert.That(restarted.State.hasPendingPublication, Is.False);
                }
                finally { restarted.Dispose(); }
            }
            finally
            {
                controller.Dispose();
                RemoveEditable(uid);
            }
        }

        [Test]
        public async Task EditDuringPendingPublicationKeepsTextAndOperationIdUnchanged()
        {
            string uid = "busy-draft-" + Guid.NewGuid().ToString("N");
            var runtime = new TrackingLocationRuntime { HoldDelay = true };
            var camera = new Camera { SucceedCapture = true };
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), new Identity(uid),
                deviceLocation: new DeviceLocation(runtime));
            var saved = new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications"));
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Park", "Find Taggi", "Original note");
                controller.CaptureSpot();
                controller.Publish();
                await Task.Yield();
                Assert.That(controller.State.busy, Is.True);
                PlacementDraft before = saved.Read(uid);
                Assert.That(before, Is.Not.Null);

                controller.SetDraft("Park", "Find Taggi", "Edited while uploading");

                Assert.That(controller.State.draftNote, Is.EqualTo("Original note"));
                Assert.That(controller.State.hasPendingPublication, Is.True);
                Assert.That(saved.Read(uid).operationId, Is.EqualTo(before.operationId));
            }
            finally
            {
                controller.Dispose();
                runtime.ReleaseDelay();
                saved.Remove(uid);
                RemoveEditable(uid);
            }
        }

        [Test]
        public async Task PausingPendingCaptureDiscardsSnapshotAndKeepsEditableDraft()
        {
            string uid = "pause-capture-" + Guid.NewGuid().ToString("N");
            var camera = new Camera { HoldCapture = true };
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), new Identity(uid),
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Park", "Find Taggi", "Keep this note");
                controller.CaptureSpot();
                Assert.That(controller.State.capturingSpot, Is.True);

                controller.SetSuspended(true);
                await Task.Yield();

                Assert.That(controller.State.capturingSpot, Is.False);
                Assert.That(controller.State.hasCapturedSpot, Is.False);
                Assert.That(controller.State.draftNote, Is.EqualTo("Keep this note"));
            }
            finally
            {
                controller.Dispose();
                RemoveEditable(uid);
            }
        }

        [Test]
        public async Task ResumeSyncPreservesUnresolvedPrecisionSettingsRecovery()
        {
            string uid = "resume-settings-" + Guid.NewGuid().ToString("N");
            var controller = new TagtagController(new ServiceConfiguration
                { apiBaseUrl = "http://invalid.test", firebaseApiKey = "test" },
                new Camera { SucceedCapture = true }, new Map(), new Identity(uid), deviceLocation: new DeviceLocation(new ReducedLocationRuntime()));
            try
            {
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Park", "Find Taggi", "Keep this note");
                controller.CaptureSpot();
                controller.Publish();
                string error = controller.State.error;
                Assert.That(controller.State.locationSettingsRequired, Is.True);

                controller.SetSuspended(true);
                controller.SetSuspended(false);
                controller.Resume();
                await Task.Yield();

                Assert.That(controller.State.locationSettingsRequired, Is.True);
                Assert.That(controller.State.error, Is.EqualTo(error));
            }
            finally
            {
                controller.Dispose();
                RemoveEditable(uid);
            }
        }

        [Test]
        public void CustomDesignSelectionRetainsCreatorOwnershipAndRestoresDraft()
        {
            var camera = new Camera();
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()));
            string id = "design-" + Guid.NewGuid().ToString("N");
            var texture = new Texture2D(2, 4);
            try
            {
                StickerArtwork.Store(id, texture.EncodeToPNG());
                var design = new StickerDesign { id = id, ownerId = "somebody-else", width = 2, height = 4 };
                controller.State.designs.Add(design);
                controller.SelectDesign(id);
                Assert.That(controller.State.selectedDesign, Is.Empty, "Collected artwork does not grant publication rights.");
                design.ownerId = identity.UserId;
                controller.OpenCreation();
                controller.SelectDesign(id);
                Assert.That(controller.State.selectedDesign, Is.EqualTo(id));
                Assert.That(controller.State.selectedPreset, Is.Empty);
                Assert.That(controller.State.page, Is.EqualTo(AppPage.Stick));
                Assert.That(controller.State.creationOpen, Is.False);
                Assert.That(camera.SelectedDesignId, Is.EqualTo(id));
                controller.SetDraft("Cafe", "By the window", "A quiet afternoon");
                var saved = new EditablePublication(System.IO.Path.Combine(Application.persistentDataPath, "editable-publications")).Read(identity.UserId);
                Assert.That(saved.designId, Is.EqualTo(id));
                controller.SelectPreset("taggi-1");
                Assert.That(controller.State.selectedDesign, Is.Empty);
                Assert.That(new EditablePublication(System.IO.Path.Combine(Application.persistentDataPath, "editable-publications")).Read(identity.UserId).designId, Is.Empty);
            }
            finally { controller.Dispose(); StickerArtwork.Forget(id); UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void NativeCreationCancellationRestoresCameraAndKeepsExistingNote()
        {
            var camera = new Camera();
            var creator = new CancellingCreator();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), new Identity(),
                deviceLocation: new DeviceLocation(new TrackingLocationRuntime()), stickerCreation: creator);
            try
            {
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Cafe", "Here", "Keep this note");
                controller.CreateSticker("import");
                Assert.That(camera.CreationSuspensions, Is.EqualTo(2));
                Assert.That(camera.CreationSuspended, Is.False);
                Assert.That(controller.State.draftNote, Is.EqualTo("Keep this note"));
                Assert.That(controller.State.selectedPreset, Is.EqualTo("taggi-1"));
                Assert.That(controller.State.hasPendingDesign, Is.False);
            }
            finally { controller.Dispose(); }
        }
        [Test]
        public async Task CancelingMapConfirmationPreservesCapturedPlacementAndRetryDoesNotRecapture()
        {
            var runtime = new TrackingLocationRuntime { LastFix = new LocationFix
                { latitude = 35.68, longitude = 139.76, accuracyMeters = 2000.149f, measuredUnixSeconds = 100 } };
            var picker = new LocationPicker();
            var camera = new Camera { SucceedCapture = true };
            var identity = new Identity();
            var saved = new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications"));
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(runtime), locationConfirmation: picker);
            try
            {
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Cafe", "By the door", "My note");
                controller.CaptureSpot();
                controller.Publish();
                Assert.That(picker.OpenCount, Is.EqualTo(1));
                Assert.That(picker.Measured.accuracyMeters, Is.EqualTo(2000.149f));
                Assert.That(saved.Read(identity.UserId).locationConfirmed, Is.False);
                string operation = saved.Read(identity.UserId).operationId;
                picker.Complete(null);
                await Task.Yield();
                Assert.That(controller.State.busy, Is.False);
                Assert.That(controller.State.draftNote, Is.EqualTo("My note"));
                Assert.That(controller.State.hasPendingPublication, Is.True);
                controller.Publish();
                Assert.That(picker.OpenCount, Is.EqualTo(2));
                Assert.That(camera.CaptureCount, Is.EqualTo(1));
                Assert.That(saved.Read(identity.UserId).operationId, Is.EqualTo(operation));
                picker.Complete(new ConfirmedLocation { latitude = 35.681, longitude = 139.761 });
                await Task.Yield();
                var draft = saved.Read(identity.UserId);
                Assert.That(draft.locationConfirmed, Is.True);
                Assert.That(draft.confirmedLocation.latitude, Is.EqualTo(35.681));
                Assert.That(draft.location.accuracyMeters, Is.EqualTo(2000.149f));
                Assert.That(draft.location.latitude, Is.EqualTo(35.68));
                Assert.That(draft.location.measuredUnixSeconds, Is.EqualTo(100));
            }
            finally { controller.Dispose(); }
        }

        [Test]
        public async Task EditingNoteAfterFailedLocationConfirmationReusesSnapshotWithNewOperation()
        {
            var runtime = new TrackingLocationRuntime { LastFix = new LocationFix
                { latitude = 35.68, longitude = 139.76, accuracyMeters = 2000, measuredUnixSeconds = 100 } };
            var picker = new LocationPicker();
            var camera = new Camera { SucceedCapture = true };
            var identity = new Identity();
            var saved = new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications"));
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(runtime), locationConfirmation: picker);
            try
            {
                controller.SelectPreset("taggi-1");
                controller.CaptureSpot();
                controller.SetDraft("Existing place", "Existing teaser", "First note");
                controller.Publish();
                string oldOperation = saved.Read(identity.UserId).operationId;
                picker.Complete(null);
                await Task.Yield();

                controller.SetDraft("Existing place", "Existing teaser", "Edited note");
                Assert.That(controller.State.hasCapturedSpot, Is.True);
                Assert.That(controller.State.hasPendingPublication, Is.False);
                controller.Publish();
                Assert.That(camera.CaptureCount, Is.EqualTo(1));
                Assert.That(saved.Read(identity.UserId).operationId, Is.Not.EqualTo(oldOperation));
                Assert.That(saved.Read(identity.UserId).note, Is.EqualTo("Edited note"));
            }
            finally { controller.Dispose(); saved.Remove(identity.UserId); RemoveEditable(identity.UserId); }
        }

        [Test]
        public async Task MovingPlacementAfterFailedPublicationRequiresFreshCaptureAndOperation()
        {
            var runtime = new TrackingLocationRuntime { LastFix = new LocationFix
                { latitude = 35.68, longitude = 139.76, accuracyMeters = 2000, measuredUnixSeconds = 100 } };
            var picker = new LocationPicker();
            var camera = new Camera { SucceedCapture = true };
            var identity = new Identity();
            var saved = new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications"));
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(runtime), locationConfirmation: picker);
            try
            {
                controller.SelectPreset("taggi-1");
                controller.CaptureSpot();
                controller.SetDraft(controller.State.draftPlace, controller.State.draftTeaser, "Keep this note");
                controller.Publish();
                string firstOperation = saved.Read(identity.UserId).operationId;
                picker.Complete(null);
                await Task.Yield();

                camera.ChangePlacement();
                Assert.That(controller.State.hasPendingPublication, Is.False);
                Assert.That(controller.State.hasCapturedSpot, Is.False);
                Assert.That(saved.Read(identity.UserId), Is.Null);
                Assert.That(controller.State.draftNote, Is.EqualTo("Keep this note"));
                controller.Publish();
                Assert.That(saved.Read(identity.UserId), Is.Null,
                    "The old operation cannot submit after a placement transform.");

                controller.CaptureSpot();
                controller.Publish();
                Assert.That(camera.CaptureCount, Is.EqualTo(2));
                Assert.That(saved.Read(identity.UserId).operationId, Is.Not.EqualTo(firstOperation));
            }
            finally { controller.Dispose(); saved.Remove(identity.UserId); RemoveEditable(identity.UserId); }
        }

        [Test]
        public async Task CameraExitRevisionChangeKeepsSavedPublicationRetry()
        {
            var runtime = new TrackingLocationRuntime { LastFix = new LocationFix
                { latitude = 35.68, longitude = 139.76, accuracyMeters = 2000, measuredUnixSeconds = 100 } };
            var picker = new LocationPicker();
            var camera = new Camera { SucceedCapture = true };
            var identity = new Identity();
            var saved = new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications"));
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(runtime), locationConfirmation: picker);
            try
            {
                controller.SelectPreset("taggi-1");
                controller.CaptureSpot();
                controller.SetDraft(controller.State.draftPlace, controller.State.draftTeaser, "A note");
                controller.Publish();
                picker.Complete(null);
                await Task.Yield();
                string operation = saved.Read(identity.UserId).operationId;

                controller.Navigate(AppPage.Home);
                Assert.That(camera.HasPlacementPreview, Is.False);
                Assert.That(controller.State.hasPendingPublication, Is.True);
                Assert.That(saved.Read(identity.UserId).operationId, Is.EqualTo(operation));
                controller.Navigate(AppPage.Stick);
                controller.CaptureSpot();
                Assert.That(controller.State.hasCapturedSpot, Is.True);
                Assert.That(camera.CaptureCount, Is.EqualTo(1));
            }
            finally { controller.Dispose(); saved.Remove(identity.UserId); RemoveEditable(identity.UserId); }
        }

        [Test]
        public async Task PausingMapConfirmationCancelsPickerAndKeepsDraft()
        {
            var runtime = new TrackingLocationRuntime { LastFix = new LocationFix
                { latitude = 35.68, longitude = 139.76, accuracyMeters = 2000, measuredUnixSeconds = 100 } };
            var picker = new LocationPicker();
            var controller = new TagtagController(new ServiceConfiguration(), new Camera { SucceedCapture = true },
                new Map(), new Identity(), deviceLocation: new DeviceLocation(runtime), locationConfirmation: picker);
            try
            {
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Cafe", "Door", "Keep this");
                controller.CaptureSpot();
                controller.Publish();
                Assert.That(picker.OpenCount, Is.EqualTo(1));
                controller.SetSuspended(true);
                await Task.Yield();
                Assert.That(picker.CancelCount, Is.EqualTo(1));
                Assert.That(controller.State.busy, Is.False);
                Assert.That(controller.State.hasPendingPublication, Is.True);
                Assert.That(controller.State.draftNote, Is.EqualTo("Keep this"));
                Assert.That(controller.State.error, Does.Contain("interrupted"));
            }
            finally { controller.Dispose(); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task ApproximateRetryConfirmsOriginalPreparedPinEvenAfterDraftReload(bool modernDraft)
        {
            var identity = new Identity();
            var original = new ConfirmedLocation { latitude = 35.681, longitude = 139.761 };
            var saved = new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications"));
            saved.Save(identity.UserId, new PlacementDraft { operationId = "saved-precise", presetId = "taggi-1",
                place = "Cafe", teaser = "Door", note = "Keep me", hasPublicationLocation = modernDraft,
                confirmedLocation = modernDraft ? original : null, locationConfirmed = false,
                location = new LocationFix { latitude = modernDraft ? original.latitude : 35.6808, longitude = original.longitude,
                    accuracyMeters = 20, measuredUnixSeconds = 90 },
                snapshot = new SpatialSnapshot { worldMapBase64 = "AQ==", widthMeters = .2f } });
            var runtime = new TrackingLocationRuntime { LastFix = new LocationFix
                { latitude = 35.68, longitude = 139.76, accuracyMeters = 2000, measuredUnixSeconds = 100 } };
            var picker = new LocationPicker();
            var camera = new Camera { SucceedCapture = true };
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(runtime), locationConfirmation: picker,
                loadPublicationLocation: _ => Task.FromResult(new PublicationLocationResult
                    { found = true, publicationLocation = original }));
            try
            {
                // A precise retry must not replace the saved target with the new measurement.
                runtime.LastFix = new LocationFix { latitude = 35.6808, longitude = 139.7608,
                    accuracyMeters = 20, measuredUnixSeconds = 100 };
                controller.Publish();
                await Task.Yield();
                Assert.That(saved.Read(identity.UserId).confirmedLocation.latitude, Is.EqualTo(original.latitude));
                runtime.LastFix = new LocationFix { latitude = 35.68, longitude = 139.76,
                    accuracyMeters = 2000, measuredUnixSeconds = 100 };
                controller.Publish();
                Assert.That(picker.OpenCount, Is.EqualTo(1));
                Assert.That(picker.FixedSpot.latitude, Is.EqualTo(original.latitude));
                Assert.That(picker.FixedSpot.longitude, Is.EqualTo(original.longitude));
                Assert.That(camera.CaptureCount, Is.Zero);
            }
            finally { controller.Dispose(); }
        }

        [Test]
        public async Task LocationIsRefreshedWhenPublishingAfterAnEarlierCapture()
        {
            var runtime = new TrackingLocationRuntime { LastFix = new LocationFix
                { latitude = 35.68, longitude = 139.76, accuracyMeters = 2000, measuredUnixSeconds = 75 } };
            var picker = new LocationPicker();
            var camera = new Camera { HoldCapture = true };
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), new Identity(),
                deviceLocation: new DeviceLocation(runtime), locationConfirmation: picker);
            try
            {
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Cafe", "Door", "Note");
                controller.CaptureSpot();
                runtime.Advance(10);
                runtime.LastFix = new LocationFix { latitude = 35.681, longitude = 139.761,
                    accuracyMeters = 1800, measuredUnixSeconds = runtime.UtcNow.ToUnixTimeSeconds() };
                camera.CompleteCapture();
                await Task.Yield();
                controller.Publish();
                Assert.That(picker.OpenCount, Is.EqualTo(1));
                Assert.That(picker.Measured.latitude, Is.EqualTo(35.681));
                Assert.That(picker.Measured.accuracyMeters, Is.EqualTo(1800));
                Assert.That(picker.Measured.measuredUnixSeconds, Is.GreaterThan(100));
            }
            finally { controller.Dispose(); }
        }

        private sealed class LocationPicker : ILocationConfirmation
        {
            public int OpenCount, CancelCount;
            public LocationFix Measured;
            public ConfirmedLocation FixedSpot;
            private Action<ConfirmedLocation> callback;
            public void Open(LocationFix measured, Action<ConfirmedLocation> completed, ConfirmedLocation fixedSpot = null)
            { OpenCount++; Measured = measured; FixedSpot = fixedSpot; callback = completed; }
            public void Cancel() { CancelCount++; Complete(null); }
            public void Complete(ConfirmedLocation result)
            { var pending = callback; callback = null; pending?.Invoke(result); }
        }

        private sealed class CancellingCreator : IStickerCreation
        {
            public int Capabilities => 1;
            public void Open(string source, Action<CreatedStickerImage> completed) => completed(new CreatedStickerImage { status = "cancelled" });
            public void Cancel() { }
        }

        private static TagtagController Controller(ILocationRuntime runtime) =>
            new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity(),
                deviceLocation: new DeviceLocation(runtime));

        private static void RemoveEditable(string uid) =>
            new EditablePublication(System.IO.Path.Combine(Application.persistentDataPath, "editable-publications")).Remove(uid);

        private sealed class TrackingLocationRuntime : ILocationRuntime
        {
            public bool HoldDelay;
            private readonly TaskCompletionSource<bool> heldDelay = new TaskCompletionSource<bool>();
            public LocationAuthorization Authorization => LocationAuthorization.FullAccuracy;
            public bool ServicesEnabled => true;
            public LocationServiceStatus Status { get; private set; } = LocationServiceStatus.Stopped;
            public LocationFix LastFix { get; set; }
            public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.FromUnixTimeSeconds(100);
            public int StartCount { get; private set; }
            public int StopCount { get; private set; }
            public float UpdateDistanceMeters { get; private set; } = -1;
            public void Start(float desiredAccuracyMeters, float updateDistanceMeters)
            {
                StartCount++;
                UpdateDistanceMeters = updateDistanceMeters;
                Status = LocationServiceStatus.Running;
            }
            public void Stop()
            {
                StopCount++;
                Status = LocationServiceStatus.Stopped;
            }
            public Task Delay(CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                if (HoldDelay) return heldDelay.Task;
                UtcNow = UtcNow.AddSeconds(1);
                return Task.CompletedTask;
            }
            public void Advance(int seconds) { UtcNow = UtcNow.AddSeconds(seconds); }
            public void ReleaseDelay()
            {
                HoldDelay = false;
                heldDelay.TrySetResult(true);
            }
        }

        private sealed class ReducedLocationRuntime : ILocationRuntime
        {
            public LocationAuthorization Authorization => LocationAuthorization.ReducedAccuracy;
            public bool ServicesEnabled => true;
            public LocationServiceStatus Status => LocationServiceStatus.Running;
            public LocationFix LastFix => new LocationFix { latitude = 35.68, longitude = 139.76,
                accuracyMeters = 1000, measuredUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UtcNow;
            public void Start(float desiredAccuracyMeters, float updateDistanceMeters) { }
            public void Stop() { }
            public Task Delay(CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                UtcNow = UtcNow.AddSeconds(1);
                return Task.CompletedTask;
            }
        }

        private sealed class Camera : IArExperience, IPlacementRevision, ICustomArtworkAr
        {
            public string SelectedDesignId;
            public bool CreationSuspended;
            public int CreationSuspensions;
            public void SelectArtwork(StickerDesign design, Texture2D texture) { SelectedDesignId = design.id; }
            public void SuspendForCreation(bool value) { CreationSuspended = value; CreationSuspensions++; }
            public int CaptureCount;
            public bool HoldCapture;
            public bool SucceedCapture;
            public bool CanPublishEnabled = true;
            public bool Preview = true;
            public PlacementScanState Scan = PlacementScanState.Ready;
            public int PlacementRevision { get; private set; }
            public string SelectedPresetId;
            private Action<SpatialSnapshot> captureSuccess;
            private Action<string> captureFailure;
            public event Action Changed;
            public event Action<string> StickerTapped { add { } remove { } }
            public CameraPresentationState CameraPresentation => CameraPresentationState.Live;
            public PlacementScanState ScanState => Scan;
            public bool IsTracking => true;
            public bool CanPublish => CanPublishEnabled;
            public bool CanCollect => false;
            public bool HasPlacementSurface => true;
            public bool HasPlacementPreview => Preview;
            public bool HasTrackedPlacement => true;
            public bool PlacementBusy => false;
            public float PlacementWidthMeters => .2f;
            public float PlacementRotationDegrees => 0;
            public string Status => "";
            public void Enter() { }
            public void Exit() { Preview = false; PlacementRevision++; Changed?.Invoke(); }
            public void SelectPreset(string id) { SelectedPresetId = id; PlacementRevision++; }
            public void CancelPlacement() { PlacementRevision++; }
            public void SetCameraInteraction(Rect rect, bool blocked) { }
            public void Place(Vector2 point) { }
            public void AdjustPlacement(float width, float rotation, Vector2? point = null) { PlacementRevision++; }
            public void ChangePlacement() { PlacementRevision++; Changed?.Invoke(); }
            public void Capture(Action<SpatialSnapshot> success, Action<string> failure)
            {
                CaptureCount++;
                if (HoldCapture) { captureSuccess = success; captureFailure = failure; }
                else if (SucceedCapture) success(new SpatialSnapshot
                    { worldMapBase64 = Convert.ToBase64String(new byte[] { 1 }), widthMeters = .2f });
                else failure("Intentional capture stop for sequencing test.");
            }
            public void CompleteCapture() => captureSuccess?.Invoke(new SpatialSnapshot
            { worldMapBase64 = Convert.ToBase64String(new byte[] { 1 }), widthMeters = .2f });
            public void FailCapture(string error) => captureFailure?.Invoke(error);
            public void Recover(RecoveryData recovery) { }
        }

        private sealed class Map : IMapExperience
        {
            public event Action<string> StickerSelected { add { } remove { } }
            public void Show(Rect rect, LocationFix fix, IReadOnlyList<StickerSummary> stickers) { }
            public void Hide() { }
            public void Recenter(LocationFix fix) { }
        }

        private sealed class Identity : INativeIdentity
        {
            private readonly string uid;
            public Identity(string uid = null)
            {
                this.uid = uid ?? "publish-location-test-" + Guid.NewGuid().ToString("N");
                createdUserIds.Add(this.uid);
            }
            public string UserId => uid;
            public void SignIn(string provider, ServiceConfiguration config, Action<IdentityCredential> success, Action<string> failure) { }
            public void StoreSession(string value) { }
            public string LoadSession() => JsonUtility.ToJson(new UserSession { uid = uid, idToken = "token",
                refreshToken = "refresh", expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600 });
            public void ClearSession() { }
        }
    }
}
