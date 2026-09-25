using System;
using System.IO;
using NUnit.Framework;
using Tagtag.Services;

namespace Tagtag.Tests
{
    public sealed class PendingPublicationTests
    {
        private string directory;
        [SetUp] public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "tagtag-tests-" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test]
        public void RestartRestoresSameOperationAndSeparatesAccounts()
        {
            var original = new PlacementDraft { operationId = "operation", presetId = "taggi-1", note = "private", snapshot = new SpatialSnapshot { worldMapBase64 = "AQID" } };
            new PendingPublication(directory).Save("alice", original);
            var restarted = new PendingPublication(directory);
            Assert.That(restarted.Read("alice").operationId, Is.EqualTo("operation"));
            Assert.That(restarted.Read("alice").snapshot.worldMapBase64, Is.EqualTo("AQID"));
            Assert.That(restarted.Read("bob"), Is.Null);
        }

        [Test]
        public void DamagedLatestSaveRecoversBackupAndCancelRemovesBoth()
        {
            var storage = new PendingPublication(directory);
            var draft = new PlacementDraft { operationId = "same", snapshot = new SpatialSnapshot { worldMapBase64 = "AQID" } };
            storage.Save("alice", draft); storage.Save("alice", draft);
            File.WriteAllText(Directory.GetFiles(directory, "*.json")[0], "broken");
            Assert.That(storage.Read("alice").operationId, Is.EqualTo("same"));
            string oldBackup = File.ReadAllText(Directory.GetFiles(directory, "*.backup")[0]);
            storage.Remove("alice");
            File.WriteAllText(Directory.GetFiles(directory, "*.json")[0] + ".backup", oldBackup);
            Assert.That(storage.Read("alice"), Is.Null);
        }
    }
}
