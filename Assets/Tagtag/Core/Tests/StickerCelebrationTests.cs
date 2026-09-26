using NUnit.Framework;

namespace Tagtag.Tests
{
    public sealed class StickerCelebrationTests
    {
        private readonly StickerSummary sticker = new StickerSummary { id = "one", presetId = "taggi-2" };

        [Test]
        public void PublicationCarriesArtworkAndIsConsumedOnlyByItsOwnToken()
        {
            var rewards = new StickerCelebrations();
            rewards.Published("owner", "operation", sticker);
            var reward = rewards.Pending;
            Assert.AreEqual(StickerCelebrationKind.Placed, reward.Kind);
            Assert.AreSame(sticker, reward.Sticker);
            rewards.Acknowledge("other");
            Assert.AreSame(reward, rewards.Pending);
            rewards.Acknowledge(reward.Token);
            Assert.IsNull(rewards.Pending);
            rewards.Published("owner", "operation", sticker);
            Assert.IsNull(rewards.Pending, "An acknowledged retry must not celebrate again.");
        }

        [Test]
        public void ExistingCollectionDoesNotCelebrateButANewCollectionDoes()
        {
            var rewards = new StickerCelebrations();
            rewards.Collected("owner", "discovery", sticker, true);
            Assert.IsNull(rewards.Pending);
            rewards.Collected("owner", "discovery", sticker, false);
            Assert.AreEqual(StickerCelebrationKind.Found, rewards.Pending.Kind);
            var reward = rewards.Pending;
            rewards.Collected("owner", "discovery", sticker, false);
            Assert.AreSame(reward, rewards.Pending);
        }

        [Test]
        public void PresentationIsClaimedOnlyOnceAcrossRemounts()
        {
            var rewards = new StickerCelebrations();
            rewards.Published("owner", "one", sticker);
            Assert.IsTrue(rewards.Pending.TryPresent());
            Assert.IsFalse(rewards.Pending.TryPresent());
            Assert.IsNotNull(rewards.Pending, "Presentation does not dismiss the reward.");
        }

        [Test]
        public void AccountChangeClearsPrivateRewardsAndDeduplication()
        {
            var rewards = new StickerCelebrations();
            rewards.Published("owner", "one", sticker);
            rewards.SetAccount("other");
            Assert.IsNull(rewards.Pending);
            rewards.Published("other", "one", sticker);
            Assert.IsNotNull(rewards.Pending);
            rewards.SetAccount(null);
            Assert.IsNull(rewards.Pending);
        }

        [Test]
        public void StaleLocalAbsenceUsesServerMembershipAndUnknownReadsDoNotCelebrate()
        {
            Assert.IsFalse(StickerCelebrations.IsNewInServerSnapshot("one", null));
            Assert.IsFalse(StickerCelebrations.IsNewInServerSnapshot("one",
                new[] { new CollectedSticker { id = "one" } }));
            Assert.IsTrue(StickerCelebrations.IsNewInServerSnapshot("one",
                new[] { new CollectedSticker { id = "other" } }));
            Assert.IsTrue(StickerCelebrations.IsNewInServerSnapshot("one", new CollectedSticker[0]));
            Assert.IsFalse(StickerCelebrations.IsNewInServerSnapshot("one", new CollectedSticker[1000]),
                "The existing server endpoint may truncate a full page.");
        }

        [Test]
        public void NoSuccessOrMissingIdentityProducesNoReward()
        {
            var rewards = new StickerCelebrations();
            rewards.SetAccount("owner");
            Assert.IsNull(rewards.Pending);
            rewards.Published(null, "one", sticker);
            rewards.Published("owner", "", sticker);
            rewards.Published("owner", "one", null);
            Assert.IsNull(rewards.Pending);
        }
    }
}
