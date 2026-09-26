using NUnit.Framework;
using UnityEngine;

namespace Tagtag.Tests
{
    public sealed class StickerArtworkTests
    {
        [Test]
        public void PortraitArtworkKeepsItsAspectRatioAtPlacementWidth()
        {
            Assert.That(StickerArtwork.Scale(0.2f, 800, 1000), Is.EqualTo(new Vector3(0.2f, 0.25f, 0.2f)));
        }
        [Test]
        public void MissingLegacyDimensionsKeepSquarePresetScale()
        {
            Assert.That(StickerArtwork.Scale(0.2f, 0, 0), Is.EqualTo(Vector3.one * 0.2f));
        }
        [Test]
        public void DecodeRejectsOversizedPngBeforeAllocatingTexture()
        {
            var png = new byte[24]; png[0] = 137; png[1] = 80; png[2] = 78; png[3] = 71;
            png[18] = 8; png[23] = 1;
            Assert.Throws<System.InvalidOperationException>(() => StickerArtwork.Decode(png));
        }
        [Test]
        public void DecodePreservesTransparentPixels()
        {
            var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D decoded = null;
            try
            {
                original.SetPixels(new[] { Color.clear, Color.red, Color.white, Color.clear }); original.Apply();
                decoded = StickerArtwork.Decode(original.EncodeToPNG());
                Assert.That(decoded.GetPixel(0, 0).a, Is.Zero);
                Assert.That(decoded.GetPixel(1, 0).r, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(original); if (decoded != null) Object.DestroyImmediate(decoded); }
        }
    }
}
