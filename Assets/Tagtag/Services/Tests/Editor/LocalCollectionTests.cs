using System;
using System.IO;
using NUnit.Framework;

namespace Tagtag.Services.Tests
{
    public sealed class LocalCollectionTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "tagtag-collection-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ReplacingCollectionKeepsLastCompleteCopyForCorruptionFallback()
        {
            var cache = new LocalCollection(directory);
            cache.Save("uid", new[] { new CollectedSticker { id = "older", collectedAt = 1 } });
            cache.Save("uid", new[] { new CollectedSticker { id = "newer", collectedAt = 2 } });
            Assert.AreEqual("newer", cache.Read("uid")[0].id);

            string primary = Directory.GetFiles(directory, "*.json")[0];
            File.WriteAllText(primary, "{broken json");
            Assert.AreEqual("older", cache.Read("uid")[0].id);
        }

        [Test]
        public void InterruptedFirstWriteCanRecoverTemporaryCopyAndRemoveCleansAllCopies()
        {
            var cache = new LocalCollection(directory);
            cache.Save("uid", new[] { new CollectedSticker { id = "recoverable", collectedAt = 1 } });
            string primary = Directory.GetFiles(directory, "*.json")[0];
            File.Move(primary, primary + ".tmp");
            Assert.AreEqual("recoverable", cache.Read("uid")[0].id);

            cache.Remove("uid");
            Assert.AreEqual(0, Directory.GetFiles(directory).Length);
            Assert.AreEqual(0, cache.Read("uid").Count);
        }
    }
}
