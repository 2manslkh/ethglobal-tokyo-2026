using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.UI.Tests
{
    public sealed class MapPresentationExploreTests
    {
        [Test]
        public void ExploreMapCanMountBeforeFirstLocationFix()
        {
            var state = new AppState { page = AppPage.Explore };
            Assert.That(MapPresentation.ShouldShow(state, false), Is.True);
        }

        [Test]
        public void ExplicitRemoteTargetIsIncludedOnceAndRecenteredOnce()
        {
            var remote = new StickerSummary { id = "remote", latitude = 35.1, longitude = 139.2 };
            var state = new AppState { page = AppPage.Explore, mapSelection = remote };
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
