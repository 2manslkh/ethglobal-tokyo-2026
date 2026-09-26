using NUnit.Framework;

namespace Tagtag.Tests
{
    public sealed class CollectionBookTests
    {
        [TestCase(0, 1)] [TestCase(1, 1)] [TestCase(20, 1)] [TestCase(21, 2)]
        public void BooksHaveTwentySpacesPerPage(int count, int expected)
        {
            Assert.That(CollectionBook.PageCount(count), Is.EqualTo(expected));
        }

        [Test]
        public void SyncKeepsLatestRevisionAndCollectionOrder()
        {
            var result = CollectionBook.Normalize(new[] {
                new CollectedSticker { id = "b", collectedAt = 20, revision = 1, note = "old" },
                new CollectedSticker { id = "a", collectedAt = 10, revision = 1 },
                new CollectedSticker { id = "b", collectedAt = 20, revision = 2, unavailable = true, note = "" }
            });
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].id, Is.EqualTo("a"));
            Assert.That(result[1].unavailable, Is.True);
            Assert.That(result[1].note, Is.Empty);
        }

        [Test]
        public void ProximityCannotUnlockWithoutTrackedTapAndSession()
        {
            var recovery = new RecoveryData { sticker = new StickerSummary { id = "one" }, discoveryId = "session", expiresAt = 200 };
            Assert.That(CollectionBook.CanUnlock(recovery, "one", false, 100), Is.False);
            Assert.That(CollectionBook.CanUnlock(recovery, "other", true, 100), Is.False);
            Assert.That(CollectionBook.CanUnlock(recovery, "one", true, 200), Is.False);
            Assert.That(CollectionBook.CanUnlock(recovery, "one", true, 100), Is.True);
        }

        [Test]
        public void StaleAndInaccurateLocationsAreRejected()
        {
            var fix = new LocationFix { latitude = 35, longitude = 139, accuracyMeters = 10, measuredUnixSeconds = 100 };
            Assert.That(CollectionBook.FreshLocation(fix, 120), Is.True);
            Assert.That(CollectionBook.FreshLocation(fix, 131), Is.False);
            fix.accuracyMeters = 51;
            Assert.That(CollectionBook.FreshLocation(fix, 120), Is.False);
            Assert.That(CollectionBook.FreshLocation(fix, 120, 100), Is.True);
        }
    }
}
