using System;
using System.IO;
using NUnit.Framework;
using Tagtag.Services;
using UnityEngine;

namespace Tagtag.Tests
{
    public sealed class LocalDesignsTests
    {
        private string directory;
        private LocalDesigns store;
        [SetUp] public void Setup() { directory = Path.Combine(Path.GetTempPath(), "tagtag-design-test-" + Guid.NewGuid().ToString("N")); store = new LocalDesigns(directory); }
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        [Test] public void LibraryIsIsolatedByAccountAndSurvivesRestart()
        {
            store.Save("alice", new[] { new StickerDesign { id = "one", ownerId = "alice", width = 800, height = 1000 } });
            var restarted = new LocalDesigns(directory);
            Assert.That(restarted.Read("alice")[0].height, Is.EqualTo(1000));
            Assert.That(restarted.Read("bob"), Is.Empty);
            Assert.That(restarted.Read(null), Is.Empty);
        }
        [Test] public void GuestDraftClaimRetainsOperationAndMovesToOnlyOneAccount()
        {
            SaveImage(null);
            var operation = store.ReadDraft(null).operationId;
            store.ClaimGuest("alice");
            Assert.That(store.ReadDraft("alice").operationId, Is.EqualTo(operation));
            Assert.That(store.ReadDraft(null), Is.Null);
            store.ClaimGuest("bob");
            Assert.That(store.ReadDraft("bob"), Is.Null);
            store.ClearDraft("alice");
            Assert.That(new LocalDesigns(directory).ReadDraft("alice"), Is.Null);
        }
        [Test] public void ExistingAccountDraftIsNotOverwrittenByGuestClaim()
        {
            SaveImage("alice"); SaveImage(null);
            var original = store.ReadDraft("alice").operationId;
            store.ClaimGuest("alice");
            Assert.That(store.ReadDraft("alice").operationId, Is.EqualTo(original));
            Assert.That(store.ReadDraft(null), Is.Null, "The new draft belongs to the account that signed in.");
            store.ClaimGuest("bob");
            Assert.That(store.ReadDraft("bob"), Is.Null);
            store.ClearDraft("alice");
            Assert.That(store.ReadDraft("alice"), Is.Not.Null, "The claimed guest draft is queued after the existing account draft.");
        }
        [Test] public void ExpiredUploadCanRestartWithoutLosingItsPixels()
        {
            SaveImage("alice");
            var old = store.ReadDraft("alice");
            var restarted = store.RestartDraft("alice");
            Assert.That(restarted.operationId, Is.Not.EqualTo(old.operationId));
            Assert.That(restarted.path, Is.EqualTo(old.path));
            Assert.That(File.Exists(restarted.path), Is.True);
            Assert.That(new LocalDesigns(directory).ReadDraft("alice").operationId, Is.EqualTo(restarted.operationId));
        }
        private void SaveImage(string uid)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "source.png");
            var image = new Texture2D(2, 2);
            try { File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(image); }
            store.SaveDraft(uid, new CreatedStickerImage { kind = "image", path = path, name = "Photo", width = 999, height = 999 });
            Assert.That(store.ReadDraft(uid).width, Is.EqualTo(2));
        }
    }
}
