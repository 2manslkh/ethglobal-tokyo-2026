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
        public void ReducedPrecisionIsReportedBeforeCapturingTheArMap()
        {
            var camera = new Camera();
            var runtime = new ReducedLocationRuntime();
            var identity = new Identity();
            var controller = new TagtagController(new ServiceConfiguration(), camera, new Map(), identity,
                deviceLocation: new DeviceLocation(runtime));
            try
            {
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Park", "Find Taggi", "Under the tree");

                controller.Publish();

                Assert.That(camera.CaptureCount, Is.Zero,
                    "A known precision permission failure must happen before expensive AR map capture.");
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
                controller.Publish();
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
            var controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), identity,
                deviceLocation: new DeviceLocation(new ReducedLocationRuntime()));
            try
            {
                controller.Navigate(AppPage.Stick);
                controller.SelectPreset("taggi-2");
                controller.SetDraft("Garden", "Look by the gate", "The flowers are lovely.");
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
        public async Task PausingPendingCaptureFinishesPromptlyAndKeepsEditableDraft()
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
                controller.Publish();
                Assert.That(controller.State.busy, Is.True);

                controller.SetSuspended(true);
                await Task.Yield();

                Assert.That(controller.State.busy, Is.False);
                Assert.That(controller.State.error, Does.Contain("interrupted"));
                Assert.That(controller.State.status, Does.Not.Contain("Saving this spot"));
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
                new Camera(), new Map(), new Identity(uid), deviceLocation: new DeviceLocation(new ReducedLocationRuntime()));
            try
            {
                controller.SelectPreset("taggi-1");
                controller.SetDraft("Park", "Find Taggi", "Keep this note");
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
            public LocationFix LastFix => null;
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

        private sealed class Camera : IArExperience
        {
            public int CaptureCount;
            public bool HoldCapture;
            public bool SucceedCapture;
            public bool CanPublishEnabled = true;
            public string SelectedPresetId;
            private Action<SpatialSnapshot> captureSuccess;
            public event Action Changed { add { } remove { } }
            public event Action<string> StickerTapped { add { } remove { } }
            public CameraPresentationState CameraPresentation => CameraPresentationState.Live;
            public bool IsTracking => true;
            public bool CanPublish => CanPublishEnabled;
            public bool CanCollect => false;
            public bool HasPlacementSurface => true;
            public bool HasPlacementPreview => true;
            public bool PlacementBusy => false;
            public float PlacementWidthMeters => .2f;
            public float PlacementRotationDegrees => 0;
            public string Status => "";
            public void Enter() { }
            public void Exit() { }
            public void SelectPreset(string id) { SelectedPresetId = id; }
            public void CancelPlacement() { }
            public void SetCameraInteraction(Rect rect, bool blocked) { }
            public void Place(Vector2 point) { }
            public void AdjustPlacement(float width, float rotation, Vector2? point = null) { }
            public void Capture(Action<SpatialSnapshot> success, Action<string> failure)
            {
                CaptureCount++;
                if (HoldCapture) captureSuccess = success;
                else if (SucceedCapture) success(new SpatialSnapshot
                    { worldMapBase64 = Convert.ToBase64String(new byte[] { 1 }), widthMeters = .2f });
                else failure("Intentional capture stop for sequencing test.");
            }
            public void CompleteCapture() => captureSuccess?.Invoke(new SpatialSnapshot
            { worldMapBase64 = Convert.ToBase64String(new byte[] { 1 }), widthMeters = .2f });
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
