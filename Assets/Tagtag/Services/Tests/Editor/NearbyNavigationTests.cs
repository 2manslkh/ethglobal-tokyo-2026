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
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity { StoredSession = ValidSession },
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
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity { StoredSession = ValidSession },
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
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity { StoredSession = ValidSession },
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
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity { StoredSession = ValidSession },
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
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity { StoredSession = ValidSession },
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
            controller.State.authored.Add(new StickerSummary { id = "old-private-sticker" });
            controller.State.draftNote = "Old account private draft";
            controller.State.selectedPreset = "taggi-1";
            // Simulate Firebase rejecting the in-flight refresh; no network or live credential is needed.
            var field = typeof(TagtagController).GetField("session", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ((FirebaseSession)field.GetValue(controller)).SignOut();
            response.SetException(new ApiFailure("Your session expired. Sign in again.", 401));
            await Task.Yield();
            Assert.That(controller.State.user, Is.Null);
            Assert.That(controller.State.accountOpen, Is.True);
            Assert.That(controller.State.authored, Is.Empty, "Session loss must not expose the old account during a later failed sync.");
            Assert.That(controller.State.draftNote, Is.Empty);
            Assert.That(controller.State.selectedPreset, Is.Empty);
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
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(), new Identity { StoredSession = ValidSession },
                stickerCreation: new FailedCreation());
            controller.State.error = "Existing note guidance";
            controller.State.draftNote = "Keep this note";

            controller.CreateSticker("ai");

            Assert.That(controller.State.designError, Is.EqualTo("Creation unavailable for this test."));
            Assert.That(controller.State.error, Is.EqualTo("Existing note guidance"));
            Assert.That(controller.State.draftNote, Is.EqualTo("Keep this note"));
            Assert.That(controller.State.hasPendingDesign, Is.False);
        }

        [Test]
        public void SignedOutStartupAndNavigationStayAtLogin()
        {
            controller.SignOut();
            controller.Start();
            foreach (var page in new[] { AppPage.Home, AppPage.Explore, AppPage.Stick })
            {
                controller.Navigate(page);
                controller.SetAccountOpen(false);
                Assert.That(controller.State.accountOpen, Is.True);
                Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
                Assert.That(controller.State.nearbyLoading, Is.False);
            }
        }

        [Test]
        public void SignedOutCannotOpenCreationOrCamera()
        {
            controller.SignOut();
            controller.OpenCreation();
            controller.CreateSticker("import");
            controller.SelectPreset("taggi-1");
            Assert.That(controller.State.creationOpen, Is.False);
            Assert.That(controller.State.selectedPreset, Is.Empty);
            Assert.That(controller.State.accountOpen, Is.True);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
        }

        [Test]
        public void RestoredSessionStartsAtHomeAndSignOutReturnsToLogin()
        {
            controller.Dispose();
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(),
                new Identity { StoredSession = ValidSession });
            Assert.That(controller.State.accountOpen, Is.False);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
            controller.Navigate(AppPage.Stick);
            controller.SignOut();
            Assert.That(controller.State.accountOpen, Is.True);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
        }

        [Test]
        public void ProviderCancellationAllowsRetryWithoutLeavingLogin()
        {
            controller.Dispose();
            var identity = new Identity();
            controller = new TagtagController(new ServiceConfiguration { apiBaseUrl = "https://invalid.test", firebaseApiKey = "test" },
                new Camera(), new Map(), identity);
            controller.SignIn("apple");
            controller.SignIn("google");
            Assert.That(identity.Attempts, Is.EqualTo(1), "Repeated activation must not open a second provider.");
            Assert.That(controller.State.busy, Is.True);
            identity.Fail("Sign-in cancelled.");
            Assert.That(controller.State.busy, Is.False);
            Assert.That(controller.State.user, Is.Null);
            Assert.That(controller.State.accountOpen, Is.True);
            controller.SignIn("google");
            Assert.That(identity.Attempts, Is.EqualTo(2));
            Assert.That(controller.State.error, Is.Empty);
            identity.Fail("Provider unavailable. Try again.");
            Assert.That(controller.State.error, Does.Contain("Provider unavailable"));
            Assert.That(controller.State.accountOpen, Is.True);
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
        public async Task DiscoveryOpensCameraBeforeLocationCompletesAndKeepsRetryOnFailure()
        {
            controller.Dispose();
            var delay = new TaskCompletionSource<bool>();
            var runtime = new BrowsingLocationRuntime { Accuracy = 500, PendingDelay = delay.Task };
            var camera = new Camera();
            controller = new TagtagController(new ServiceConfiguration(), camera, new Map(),
                new Identity { StoredSession = ValidSession },
                deviceLocation: new DeviceLocation(runtime));
            controller.State.page = AppPage.Explore;
            controller.State.selected = new StickerSummary { id = "find-me" };
            controller.State.selectedPreset = "taggi-1";
            controller.State.selectedDesign = "old-design";

            controller.StartDiscovery();
            try
            {
                Assert.That(controller.State.page, Is.EqualTo(AppPage.Stick),
                    "Find in AR must open the camera while GPS is still pending.");
                Assert.That(camera.EnterCount, Is.EqualTo(1));
                Assert.That(controller.State.selectedPreset, Is.Empty);
                Assert.That(controller.State.selectedDesign, Is.Empty);
                Assert.That(controller.State.busy, Is.True);
                Assert.That(controller.State.status, Does.Contain("location"));
                controller.StartDiscovery();
                Assert.That(camera.EnterCount, Is.EqualTo(1), "Repeated taps must not restart the camera.");
            }
            finally { delay.TrySetException(new ApiFailure("Location unavailable. Try again.")); }
            await Task.Yield();
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Stick));
            Assert.That(controller.State.busy, Is.False);
            Assert.That(controller.State.error, Does.Contain("Location unavailable"));
            Assert.That(controller.State.selected.id, Is.EqualTo("find-me"), "Keep the target available for retry.");
        }

        [Test]
        public async Task ClosingDiscoveryDuringLocationWaitLeavesCameraAndIgnoresLateFailure()
        {
            controller.Dispose();
            var delay = new TaskCompletionSource<bool>();
            var runtime = new BrowsingLocationRuntime { Accuracy = 500, PendingDelay = delay.Task };
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(),
                new Identity { StoredSession = ValidSession }, deviceLocation: new DeviceLocation(runtime));
            controller.State.page = AppPage.Explore;
            controller.State.selected = new StickerSummary { id = "find-me" };
            controller.StartDiscovery();
            try
            {
                controller.Navigate(AppPage.Home);
                Assert.That(controller.State.page, Is.EqualTo(AppPage.Home), "Close must leave AR without waiting for GPS.");
                Assert.That(controller.State.busy, Is.False);
            }
            finally { delay.TrySetException(new ApiFailure("Late GPS failure")); }
            await Task.Yield();
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
            Assert.That(controller.State.error, Is.Empty, "A cancelled search must not show a late error.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CancelledRecoveryCannotOverwriteOrUnlockANewerSearch(bool lateFailure)
        {
            controller.Dispose();
            var oldResponse = new TaskCompletionSource<RecoveryData>();
            var newResponse = new TaskCompletionSource<RecoveryData>();
            CancellationToken oldToken = default;
            var camera = new Camera();
            controller = new TagtagController(new ServiceConfiguration(), camera, new Map(),
                new Identity { StoredSession = ValidSession },
                locateNearby: _ => Task.FromResult(new LocationFix { accuracyMeters = 5 }),
                loadNearby: _ => Task.FromResult(Array.Empty<StickerSummary>()),
                deviceLocation: new DeviceLocation(new BrowsingLocationRuntime { Accuracy = 5 }),
                loadRecovery: (id, fix, token) =>
                {
                    if (id == "old") { oldToken = token; return oldResponse.Task; }
                    return newResponse.Task;
                });
            controller.State.page = AppPage.Explore;
            controller.State.selected = new StickerSummary { id = "old" };
            controller.StartDiscovery();
            Assert.That(controller.State.discoveryLoading, Is.True);
            controller.Navigate(AppPage.Explore);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Explore));
            Assert.That(oldToken.IsCancellationRequested, Is.True);
            Assert.That(controller.State.busy, Is.False);
            controller.State.selected = new StickerSummary { id = "new" };
            controller.StartDiscovery();
            if (lateFailure) oldResponse.SetException(new ApiFailure("Old download failed"));
            else oldResponse.SetResult(new RecoveryData { sticker = new StickerSummary { id = "old" } });
            await Task.Yield();
            Assert.That(controller.State.busy, Is.True, "An old completion must not unlock the new search.");
            Assert.That(controller.State.discoveryLoading, Is.True);
            Assert.That(controller.State.error, Is.Empty);
            Assert.That(camera.LastRecovery, Is.Null, "Cancelled results must not reach AR.");
            newResponse.SetResult(new RecoveryData { sticker = new StickerSummary { id = "new" } });
            await Task.Yield();
            Assert.That(controller.State.busy, Is.False);
            Assert.That(controller.State.discoveryLoading, Is.False);
            Assert.That(camera.LastRecovery.sticker.id, Is.EqualTo("new"));
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
            public Task PendingDelay;
            public LocationAuthorization Authorization => LocationAuthorization.FullAccuracy;
            public bool ServicesEnabled => true;
            public LocationServiceStatus Status { get; private set; } = LocationServiceStatus.Stopped;
            public LocationFix LastFix => new LocationFix { latitude = 35.68, longitude = 139.76,
                accuracyMeters = Accuracy, measuredUnixSeconds = MeasuredAt };
            public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.FromUnixTimeSeconds(100);
            public void Start(float desiredAccuracyMeters, float updateDistanceMeters) { Status = LocationServiceStatus.Running; }
            public void Stop() { StopCount++; Status = LocationServiceStatus.Stopped; }
            public Task Delay(CancellationToken cancellation)
            { cancellation.ThrowIfCancellationRequested(); DelayCount++; UtcNow = UtcNow.AddSeconds(1); return PendingDelay ?? Task.CompletedTask; }
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
            public int EnterCount;
            public void Enter() { EnterCount++; }
            public void Exit() { }
            public void SelectPreset(string id) { }
            public void CancelPlacement() { }
            public void SetCameraInteraction(Rect rect, bool blocked) { }
            public void Place(Vector2 point) { }
            public void AdjustPlacement(float width, float rotation, Vector2? point = null) { }
            public void Capture(Action<SpatialSnapshot> success, Action<string> failure) { }
            public RecoveryData LastRecovery;
            public void Recover(RecoveryData recovery) { LastRecovery = recovery; }
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
            public int Attempts;
            public Action<string> Fail;
            public void SignIn(string provider, ServiceConfiguration config, Action<IdentityCredential> success, Action<string> failure) { Attempts++; Fail = failure; }
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
