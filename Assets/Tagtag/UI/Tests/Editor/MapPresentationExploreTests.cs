using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.UI.Tests
{
    public sealed class MapPresentationExploreTests
    {
        [Test]
        public void SignedOutExploreCannotMountMapEvenWithLocation()
        {
            var state = new AppState { page = AppPage.Explore, location = new LocationFix { accuracyMeters = 10 } };
            Assert.That(MapPresentation.ShouldShow(state, false), Is.False);
        }

        [Test]
        public void ExploreMapCanMountBeforeFirstLocationFix()
        {
            var state = new AppState { user = new UserSession { uid = "map-review" }, page = AppPage.Explore };
            Assert.That(MapPresentation.ShouldShow(state, false), Is.True);
        }

        [Test]
        public void ExplicitRemoteTargetIsIncludedOnceAndRecenteredOnce()
        {
            var remote = new StickerSummary { id = "remote", latitude = 35.1, longitude = 139.2 };
            var state = new AppState { user = new UserSession { uid = "map-review" }, page = AppPage.Explore, mapSelection = remote };
            state.nearby.Add(new StickerSummary { id = "local" });
            var map = new RecordingMap();

            var pins = MapPresentation.Pins(state);
            Assert.That(pins.Count, Is.EqualTo(2));
            Assert.That(pins[1], Is.SameAs(remote));
            MapPresentation.SyncTarget(state, map);
            MapPresentation.SyncTarget(state, map);
            Assert.That(map.Centers, Is.EqualTo(1), "A refresh must not override a user's later pan.");

            state.nearby.Add(remote);
            Assert.That(MapPresentation.Pins(state).Count, Is.EqualTo(2));
        }

        [Test]
        public void ReopeningSamePlacedLocationAfterLeavingExploreRecentersAgain()
        {
            var target = new StickerSummary { id = "repeat", latitude = 35.1, longitude = 139.2 };
            var state = new AppState { user = new UserSession { uid = "map-review" }, page = AppPage.Explore, mapSelection = target };
            var map = new RecordingMap();

            MapPresentation.SyncTarget(state, map);
            MapPresentation.SyncTarget(state, map);
            Assert.That(map.Centers, Is.EqualTo(1), "Nearby refresh must preserve the user's map pan.");

            MapPresentation.SyncVisibility(state, true, map);
            MapPresentation.SyncTarget(state, map);
            Assert.That(map.Centers, Is.EqualTo(1), "Closing a sheet must preserve the map region.");

            state.page = AppPage.Home;
            MapPresentation.SyncVisibility(state, false, map);
            state.page = AppPage.Explore;
            MapPresentation.SyncTarget(state, map);
            Assert.That(map.Centers, Is.EqualTo(2), "Opening the same placed row again is an explicit target request.");
        }

        private sealed class RecordingMap : IMapExperience
        {
            public int Centers;
            public event Action<string> StickerSelected { add { } remove { } }
            public void Show(Rect rect, LocationFix location, IReadOnlyList<StickerSummary> pins) { }
            public void Hide() { }
            public void Recenter(LocationFix location) { Centers++; }
        }
    }
}
