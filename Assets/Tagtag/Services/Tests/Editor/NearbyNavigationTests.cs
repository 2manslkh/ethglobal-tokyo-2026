using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.Services.Tests
{
    public sealed class NearbyNavigationTests
    {
        private TagtagController controller;
        private TaskCompletionSource<LocationFix> location;
        private CancellationToken requestToken;

        [SetUp]
        public void SetUp()
        {
            location = new TaskCompletionSource<LocationFix>();
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity(),
                token => { requestToken = token; return location.Task; },
                _ => Task.FromResult(Array.Empty<StickerSummary>()));
        }

        [TearDown] public void TearDown() { controller.Dispose(); location.TrySetCanceled(); }

        [Test]
        public void WaitingForLocationDoesNotBlockLeavingExplore()
        {
            controller.Navigate(AppPage.Explore);
            Assert.That(controller.State.busy, Is.False, "Nearby reads must not acquire the global action lock.");
            Assert.That(controller.State.nearbyLoading, Is.True);
            Assert.That(controller.State.nearbyFindingLocation, Is.True);
            controller.Navigate(AppPage.Stick);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Stick));
            Assert.That(controller.State.nearbyLoading, Is.False);
            Assert.That(controller.State.nearbyFindingLocation, Is.False);
            Assert.That(requestToken.IsCancellationRequested, Is.True);
        }

        [Test]
        public async Task LocationFixSwitchesLoadingPhaseBeforeNearbyCompletes()
        {
            var response = new TaskCompletionSource<StickerSummary[]>();
            controller.Dispose();
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity(),
                _ => Task.FromResult(new LocationFix { latitude = 35, longitude = 139, accuracyMeters = 5 }),
                _ => response.Task);

            controller.Navigate(AppPage.Explore);
            Assert.That(controller.State.nearbyLoading, Is.True);
            Assert.That(controller.State.nearbyFindingLocation, Is.False);
            response.SetResult(Array.Empty<StickerSummary>());
            await Task.Yield();
            Assert.That(controller.State.nearbyLoading, Is.False);
        }

        [Test]
        public async Task EmptySuccessfulNearbyResultIsCachedOnQuickReturnButManualRefreshFetches()
        {
            controller.Dispose();
            int calls = 0;
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity(),
                _ => Task.FromResult(new LocationFix { latitude = 35, longitude = 139, accuracyMeters = 5 }),
                _ => { calls++; return Task.FromResult(Array.Empty<StickerSummary>()); });

            controller.Navigate(AppPage.Explore);
            await Task.Yield();
            controller.Navigate(AppPage.Home);
            controller.Navigate(AppPage.Explore);
            await Task.Yield();
            Assert.That(calls, Is.EqualTo(1));

            controller.RefreshNearby();
            await Task.Yield();
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public async Task FailedRefreshPreservesPinsAndExplicitRemoteSelection()
        {
            controller.Dispose();
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity(),
                _ => Task.FromResult(new LocationFix { latitude = 35, longitude = 139, accuracyMeters = 5 }),
                _ => Task.FromException<StickerSummary[]>(new ApiFailure("Nearby unavailable")));
            var oldPin = new StickerSummary { id = "old" };
            var remote = new StickerSummary { id = "remote", latitude = 36, longitude = 140 };
            controller.State.nearby.Add(oldPin);
            controller.State.mapSelection = remote;
            controller.State.selected = remote;

            controller.Navigate(AppPage.Explore);
            controller.RefreshNearby();
            await Task.Yield();

            Assert.That(controller.State.nearby, Does.Contain(oldPin));
            Assert.That(controller.State.mapSelection, Is.SameAs(remote));
            Assert.That(controller.State.selected, Is.SameAs(remote));
            Assert.That(controller.State.error, Is.EqualTo("Nearby unavailable"));
            Assert.That(controller.State.nearbyFindingLocation, Is.False);
        }

        [Test]
        public async Task LateLocationFailureCannotReplaceAnotherScreensStatus()
        {
            controller.Navigate(AppPage.Explore);
            controller.Navigate(AppPage.Home);
            controller.State.status = "Your book";
            location.SetException(new ApiFailure("We need a more accurate location."));
            await Task.Yield();
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
            Assert.That(controller.State.error, Is.Empty);
            Assert.That(controller.State.status, Is.EqualTo("Your book"));
        }

        [Test]
        public async Task LeavingDuringRequestDiscardsLateNearbyResults()
        {
            var response = new TaskCompletionSource<StickerSummary[]>();
            controller.Dispose();
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity(),
                _ => Task.FromResult(new LocationFix { latitude = 35, longitude = 139, accuracyMeters = 5 }),
                _ => response.Task);
            controller.Navigate(AppPage.Explore);
            controller.Navigate(AppPage.Home);
            response.SetResult(new[] { new StickerSummary { id = "late" } });
            await Task.Yield();
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
            Assert.That(controller.State.nearby, Is.Empty);
            Assert.That(controller.State.nearbyLoading, Is.False);
        }

        [Test]
        public void ReturningFromAccountRestartsInterruptedNearbyLookup()
        {
            controller.Navigate(AppPage.Explore);
            controller.SetAccountOpen(true);
            Assert.That(controller.State.nearbyLoading, Is.False);
            controller.SetAccountOpen(false);
            Assert.That(controller.State.nearbyLoading, Is.True);
            Assert.That(requestToken.IsCancellationRequested, Is.False);
        }

        [Test]
        public void ReturningToExplorePreservesForegroundFailureWhileRestartingNearby()
        {
            controller.Navigate(AppPage.Explore);
            controller.SetAccountOpen(true);
            controller.State.error = "Your account could not synchronize. Try again.";
            controller.SetAccountOpen(false);
            Assert.That(controller.State.nearbyLoading, Is.True);
            Assert.That(controller.State.error, Is.EqualTo("Your account could not synchronize. Try again."));
        }

        [Test]
        public void ResumeSynchronizationRestartsInterruptedNearbyLookup()
        {
            controller.Dispose();
            int attempts = 0;
            controller = new TagtagController(new ServiceConfiguration { apiBaseUrl = "http://invalid.test", firebaseApiKey = "test" },
                new Camera(), new Map(), new Identity { StoredSession = ValidSession },
                token => { attempts++; requestToken = token; return location.Task; },
                _ => Task.FromResult(Array.Empty<StickerSummary>()));
            controller.Navigate(AppPage.Explore);
            controller.Resume(); // The invalid URL fails synchronously without sending a request.
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(controller.State.nearbyLoading, Is.True);
            Assert.That(requestToken.IsCancellationRequested, Is.False);
        }

        [Test]
        public async Task NearbySessionInvalidationUpdatesIdentityEvenAfterNavigation()
        {
            controller.Dispose();
            var response = new TaskCompletionSource<StickerSummary[]>();
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity { StoredSession = ValidSession },
                _ => Task.FromResult(new LocationFix { accuracyMeters = 5 }), _ => response.Task);
            controller.Navigate(AppPage.Explore);
            controller.Navigate(AppPage.Home);
            // Simulate Firebase rejecting the in-flight refresh; no network or live credential is needed.
            var field = typeof(TagtagController).GetField("session", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ((FirebaseSession)field.GetValue(controller)).SignOut();
            response.SetException(new ApiFailure("Your session expired. Sign in again.", 401));
            await Task.Yield();
            Assert.That(controller.State.user, Is.Null);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
            Assert.That(controller.State.error, Is.Empty);
        }

        [Test]
        public void DesignRefreshFailurePreservesWriteNoteAndPendingDraftState()
        {
            controller.Dispose();
            controller = new TagtagController(new ServiceConfiguration
                { apiBaseUrl = "http://invalid.test", firebaseApiKey = "test" },
                new Camera(), new Map(), new Identity { StoredSession = ValidSession });
            controller.Navigate(AppPage.Stick);
            controller.State.draftNote = "Keep this unfinished note";
            controller.State.hasPendingDesign = true;
            controller.State.error = "Existing note guidance";

            controller.RefreshDesigns();

            Assert.That(controller.State.designError, Does.Contain("secure HTTPS address"));
            Assert.That(controller.State.error, Is.EqualTo("Existing note guidance"));
            Assert.That(controller.State.draftNote, Is.EqualTo("Keep this unfinished note"));
            Assert.That(controller.State.hasPendingDesign, Is.True);
            Assert.That(controller.State.designsLoading, Is.False);
            Assert.That(controller.State.busy, Is.False);
        }

        [Test]
        public void CreationFailureDoesNotReplaceWriteNoteError()
        {
            controller.Dispose();
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity(),
                stickerCreation: new FailedCreation());
            controller.State.error = "Existing note guidance";
            controller.State.draftNote = "Keep this note";

            controller.CreateSticker("ai");

            Assert.That(controller.State.designError, Is.EqualTo("Creation unavailable for this test."));
            Assert.That(controller.State.error, Is.EqualTo("Existing note guidance"));
            Assert.That(controller.State.draftNote, Is.EqualTo("Keep this note"));
            Assert.That(controller.State.hasPendingDesign, Is.False);
        }

        [TestCase(75f)]
        [TestCase(500f)]
        [TestCase(2000f)]
        [TestCase(2000.149f)]
        [TestCase(5000f)]
        public void ExploreLoadsWithFreshApproximateLocation(float accuracy)
        {
            controller.Dispose();
            var runtime = new BrowsingLocationRuntime { Accuracy = accuracy };
            LocationFix queried = null;
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(),
                new Identity { StoredSession = ValidSession },
                loadNearby: fix => { queried = fix; return Task.FromResult(new[] { new StickerSummary { id = "nearby" } }); },
                deviceLocation: new DeviceLocation(runtime));

            controller.Navigate(AppPage.Explore);

            Assert.That(controller.State.error, Is.Empty, "Browsing must not wait for discovery-grade GPS accuracy.");
            Assert.That(controller.State.location, Is.Not.Null, "Explore must have a map center.");
            Assert.That(controller.State.location.accuracyMeters, Is.EqualTo(accuracy), "Never misreport the measured precision.");
            Assert.That(queried, Is.SameAs(controller.State.location));
            Assert.That(controller.State.nearby.Count, Is.EqualTo(1));
            Assert.That(controller.State.nearbyLoading, Is.False);
            Assert.That(runtime.DelayCount, Is.Zero);
            Assert.That(runtime.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void BrowsingStillRejectsStaleLocation()
        {
            controller.Dispose();
            var runtime = new BrowsingLocationRuntime { Accuracy = 500, MeasuredAt = 0 };
            bool queried = false;
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(),
                new Identity { StoredSession = ValidSession },
                loadNearby: fix => { queried = true; return Task.FromResult(Array.Empty<StickerSummary>()); },
                deviceLocation: new DeviceLocation(runtime));
            controller.Navigate(AppPage.Explore);
            Assert.That(queried, Is.False);
            Assert.That(controller.State.location, Is.Null);
            Assert.That(controller.State.nearbyLoading, Is.False);
            Assert.That(controller.State.error, Is.Not.Empty);
        }

        private sealed class BrowsingLocationRuntime : ILocationRuntime
        {
            public float Accuracy;
            public long MeasuredAt = 100;
            public int DelayCount, StopCount;
            public LocationAuthorization Authorization => LocationAuthorization.FullAccuracy;
            public bool ServicesEnabled => true;
            public LocationServiceStatus Status { get; private set; } = LocationServiceStatus.Stopped;
            public LocationFix LastFix => new LocationFix { latitude = 35.68, longitude = 139.76,
                accuracyMeters = Accuracy, measuredUnixSeconds = MeasuredAt };
            public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.FromUnixTimeSeconds(100);
            public void Start(float desiredAccuracyMeters, float updateDistanceMeters) { Status = LocationServiceStatus.Running; }
            public void Stop() { StopCount++; Status = LocationServiceStatus.Stopped; }
            public Task Delay(CancellationToken cancellation)
            { cancellation.ThrowIfCancellationRequested(); DelayCount++; UtcNow = UtcNow.AddSeconds(1); return Task.CompletedTask; }
        }

        private static string ValidSession => JsonUtility.ToJson(new UserSession
        { uid = "nearby-test", idToken = "test-token", refreshToken = "test-refresh", expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600 });

        private sealed class Camera : IArExperience
        {
            public event Action Changed { add { } remove { } }
            public event Action<string> StickerTapped { add { } remove { } }
            public CameraPresentationState CameraPresentation => CameraPresentationState.Inactive;
            public PlacementScanState ScanState => PlacementScanState.FindingSurface;
            public bool IsTracking => false;
            public bool CanPublish => false;
            public bool CanCollect => false;
            public bool HasPlacementSurface => false;
            public bool HasPlacementPreview => false;
            public bool HasTrackedPlacement => false;
            public bool PlacementBusy => false;
            public float PlacementWidthMeters => .2f;
            public float PlacementRotationDegrees => 0f;
            public string Status => "";
            public void Enter() { }
            public void Exit() { }
            public void SelectPreset(string id) { }
            public void CancelPlacement() { }
            public void SetCameraInteraction(Rect rect, bool blocked) { }
            public void Place(Vector2 point) { }
            public void AdjustPlacement(float width, float rotation, Vector2? point = null) { }
            public void Capture(Action<SpatialSnapshot> success, Action<string> failure) { }
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
            public string StoredSession = "{}";
            public void SignIn(string provider, ServiceConfiguration config, Action<IdentityCredential> success, Action<string> failure) { }
            public void StoreSession(string value) { }
            public string LoadSession() => StoredSession;
            public void ClearSession() { }
        }

        private sealed class FailedCreation : IStickerCreation
        {
            public int Capabilities => 0;
            public void Open(string source, Action<CreatedStickerImage> completed) =>
                completed(new CreatedStickerImage { status = "error", error = "Creation unavailable for this test." });
            public void Cancel() { }
        }
    }
}
