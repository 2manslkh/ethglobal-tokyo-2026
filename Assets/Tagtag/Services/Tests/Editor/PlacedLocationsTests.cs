using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.Services.Tests
{
    public sealed class PlacedLocationsTests
    {
        private TagtagController controller;
        [TearDown] public void TearDown() => controller?.Dispose();
        private void Create(Func<string, Task<PlacementPage>> fetch)
        {
            controller = new TagtagController(new ServiceConfiguration(), new Camera(), new Map(),
                new Identity { StoredSession = JsonUtility.ToJson(new UserSession { uid = "placed-test", idToken = "test",
                    refreshToken = "test", expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600 }) },
                locateNearby: _ => new TaskCompletionSource<LocationFix>().Task, loadPlacements: fetch);
        }
        private static StickerSummary Sticker(string id, long date = 1, string status = "published") =>
            new StickerSummary { id = id, authorId = "placed-test", status = status, createdAt = date, latitude = 35, longitude = 139 };

        [Test] public void PagesMergeWithoutDuplicatesAndExcludeInactivePlacements()
        {
            var cursors = new List<string>();
            Create(cursor => { cursors.Add(cursor); return Task.FromResult(cursor == "" ?
                new PlacementPage { items = new[] { Sticker("a", 3), Sticker("withdrawn", 4, "withdrawn") }, nextCursor = "next" } :
                new PlacementPage { items = new[] { Sticker("a", 3), Sticker("b", 2) } }); });
            controller.RefreshPlacements();
            controller.LoadMorePlacements();
            Assert.That(cursors, Is.EqualTo(new[] { "", "next" }));
            Assert.That(controller.State.placements.ConvertAll(x => x.id), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(controller.State.placementsLoaded, Is.True);
            Assert.That(controller.State.placementsNextCursor, Is.Empty);
        }
        [Test] public async Task SignOutDiscardsAnInFlightPage()
        {
            var pending = new TaskCompletionSource<PlacementPage>();
            Create(_ => pending.Task);
            controller.RefreshPlacements();
            Assert.That(controller.State.placementsLoading, Is.True);
            controller.SignOut();
            pending.SetResult(new PlacementPage { items = new[] { Sticker("old-account") } });
            await Task.Yield();
            Assert.That(controller.State.placements, Is.Empty);
            Assert.That(controller.State.placementsLoading, Is.False);
            Assert.That(controller.State.placementsLoaded, Is.False);
        }
        [Test] public void FailedNextPageKeepsExistingRowsAndAllowsRetry()
        {
            int calls = 0;
            Create(cursor => { calls++; return cursor == "" ? Task.FromResult(new PlacementPage {
                items = new[] { Sticker("first") }, nextCursor = "next" }) :
                Task.FromException<PlacementPage>(new ApiFailure("Try again.")); });
            controller.RefreshPlacements(); controller.LoadMorePlacements(); controller.LoadMorePlacements();
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(controller.State.placements.Count, Is.EqualTo(1));
            Assert.That(controller.State.placementsNextCursor, Is.EqualTo("next"));
            Assert.That(controller.State.placementsError, Is.EqualTo("Try again."));
            Assert.That(controller.State.busy, Is.False);
        }
        [Test] public async Task SuccessfulWithdrawalDiscardsOlderPages()
        {
            var pending = new TaskCompletionSource<PlacementPage>();
            int calls = 0;
            Create(_ => ++calls == 1 ? pending.Task : Task.FromResult(new PlacementPage { items = Array.Empty<StickerSummary>() }));
            controller.State.placements.Add(Sticker("withdrawn"));
            controller.RefreshPlacements();
            typeof(TagtagController).GetMethod("PlacementWithdrawn", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).Invoke(controller, new object[] { "withdrawn" });
            pending.SetResult(new PlacementPage { items = new[] { Sticker("withdrawn") } });
            await Task.Yield();
            Assert.That(controller.State.placements, Is.Empty);
            Assert.That(controller.State.placementsLoading, Is.False);
        }
        [Test] public void OpeningDistantPlacementDoesNotChangePhysicalLocation()
        {
            Create(_ => Task.FromResult(new PlacementPage()));
            var fix = new LocationFix { latitude = 1, longitude = 2 };
            controller.State.location = fix;
            var sticker = Sticker("distant");
            controller.OpenPlacedLocation(sticker);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Explore));
            Assert.That(controller.State.location, Is.SameAs(fix));
            Assert.That(controller.State.selected, Is.SameAs(sticker));
            Assert.That(controller.State.mapSelection, Is.SameAs(sticker));
        }
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

    }
}
