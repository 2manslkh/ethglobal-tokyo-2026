using System.Collections.Generic;
using NUnit.Framework;

namespace Tagtag.UI.Tests
{
    public sealed class BookPresentationTests
    {
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(20, 1)]
        [TestCase(21, 2)]
        public void PageCountKeepsExactlyTwentySlotsPerPage(int items, int pages)
        {
            Assert.AreEqual(pages, BookPaging.PageCount(items));
            Assert.AreEqual(-1, BookPaging.IndexAt(0, 19, 1));
            Assert.AreEqual(19, BookPaging.IndexAt(0, 19, 20));
            Assert.AreEqual(20, BookPaging.IndexAt(1, 0, 21));
            Assert.AreEqual(-1, BookPaging.IndexAt(1, 1, 21));
        }

        [Test]
        public void HorizontalSwipeChangesPageAndDoesNotCountAsTap()
        {
            Assert.AreEqual(1, BookPaging.PageAfterSwipe(0, 21, -48f, 6f));
            Assert.IsFalse(BookPaging.IsTap(-48f, 6f));
            Assert.AreEqual(0, BookPaging.PageAfterSwipe(0, 21, -8f, 46f));
            Assert.IsFalse(BookPaging.IsTap(-8f, 46f));
            Assert.IsTrue(BookPaging.IsTap(5f, 5f));
            Assert.AreEqual(0, BookPaging.PageAfterSwipe(0, 21, 5f, 5f));
            Assert.AreEqual(1, BookPaging.PageAfterSwipe(1, 21, -60f, 0f));
        }

        [Test]
        public void CollectionOrderDeduplicatesByRevisionAndKeepsCollectionSequence()
        {
            List<CollectedSticker> source = new List<CollectedSticker>
            {
                new CollectedSticker { id = "earlier", collectedAt = 10, revision = 1 },
                new CollectedSticker { id = "later", collectedAt = 20 },
                new CollectedSticker { id = "earlier", collectedAt = 10, revision = 2, unavailable = true },
                new CollectedSticker { id = "same-time-b", collectedAt = 20 },
                null,
                new CollectedSticker { id = "same-time-a", collectedAt = 20 }
            };

            List<CollectedSticker> result = CollectionPresentation.OrderedDistinct(source);

            Assert.AreEqual(4, result.Count);
            CollectionAssert.AreEqual(new[] { "earlier", "later", "same-time-a", "same-time-b" },
                result.ConvertAll(sticker => sticker.id));
            Assert.IsTrue(result[0].unavailable);
            Assert.AreEqual(6, source.Count);
        }

        [Test]
        public void MapStaysHiddenUnderAccountAndDetailSheets()
        {
            AppState state = new AppState { page = AppPage.Explore, location = new LocationFix() };
            Assert.IsTrue(MapPresentation.ShouldShow(state, false));
            Assert.IsFalse(MapPresentation.ShouldShow(state, true));
            state.accountOpen = true;
            Assert.IsFalse(MapPresentation.ShouldShow(state, false));
            state.accountOpen = false;
            state.page = AppPage.Stick;
            Assert.IsFalse(MapPresentation.ShouldShow(state, false));
            state.page = AppPage.Explore;
            state.location = null;
            Assert.IsTrue(MapPresentation.ShouldShow(state, false));
        }

        [Test]
        public void OpeningASheetHidesAMountedNativeMapBeforeItsOverlayAppears()
        {
            AppState state = new AppState { page = AppPage.Explore, location = new LocationFix { accuracyMeters = 10f } };
            CountingMap map = new CountingMap();
            Assert.IsTrue(MapPresentation.SyncVisibility(state, false, map));
            Assert.IsFalse(MapPresentation.SyncVisibility(state, true, map));
            Assert.AreEqual(1, map.HideCount);
            Assert.IsTrue(MapPresentation.SyncVisibility(state, false, map));
        }

        [Test]
        public void CollectedDetailRefreshesWhenSyncRevokesOrReplacesTheSameSticker()
        {
            PresenterCache cache = new PresenterCache();
            CollectedSticker original = new CollectedSticker { id = "river", revision = 1, note = "First note" };
            CollectedSticker replacement = new CollectedSticker { id = "river", revision = 2, note = "Updated note" };
            Assert.IsTrue(cache.NeedsRefresh(CollectionPresentation.DetailKey(original, false)));
            Assert.IsFalse(cache.NeedsRefresh(CollectionPresentation.DetailKey(original, false)));
            Assert.IsTrue(cache.NeedsRefresh(CollectionPresentation.DetailKey(replacement, false)));
            replacement.unavailable = true;
            replacement.note = "";
            Assert.IsTrue(cache.NeedsRefresh(CollectionPresentation.DetailKey(replacement, false)));
            replacement.designId = "custom-art";
            replacement.artworkUrl = "https://example.test/artwork";
            Assert.IsTrue(cache.NeedsRefresh(CollectionPresentation.DetailKey(replacement, false)));
            Assert.IsTrue(cache.NeedsRefresh(CollectionPresentation.DetailKey(null, false)));
        }

        private sealed class CountingMap : IMapExperience
        {
            public int HideCount;
            public event System.Action<string> StickerSelected;
            public void Show(UnityEngine.Rect bounds, LocationFix location, System.Collections.Generic.IReadOnlyList<StickerSummary> stickers) { }
            public void Hide() { HideCount++; }
            public void Recenter(LocationFix location) { }
        }
    }
}
