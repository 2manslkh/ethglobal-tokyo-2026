using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.Tests
{
    public sealed class StickerPresetsTests
    {
        [Test]
        public void ThirteenOrderedDefaultStickersHaveArtworkAndUniqueNames()
        {
            Assert.That(StickerPresets.Ids, Is.EqualTo(Enumerable.Range(1, 13).Select(index => "taggi-" + index)));
            Assert.That(StickerPresets.Ids.Select(StickerPresets.DisplayName).Distinct().Count(), Is.EqualTo(13));
            foreach (string id in StickerPresets.Ids)
            {
                Assert.That(StickerPresets.Contains(id), Is.True);
                var texture = StickerArtwork.Get(id, "", null, null);
                Assert.That(texture, Is.Not.Null, id + " must ship in Resources");
                Assert.That(texture.width, Is.EqualTo(texture.height), id + " must preserve square proportions");
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("taggi-0")]
        [TestCase("taggi-14")]
        [TestCase("taggi-01")]
        [TestCase("TAGGI-1")]
        [TestCase("taggi-13-extra")]
        public void UnknownDefaultStickerIdsAreRejected(string id)
        {
            Assert.That(StickerPresets.Contains(id), Is.False);
            Assert.That(StickerPresets.DisplayName(id), Is.EqualTo("Taggi"));
        }
    }
}
