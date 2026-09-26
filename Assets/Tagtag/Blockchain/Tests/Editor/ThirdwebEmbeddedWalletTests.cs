using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tagtag.Blockchain.Tests
{
    public sealed class ThirdwebEmbeddedWalletTests
    {
        [Test]
        public void ConstructorRejectsMissingClientId()
        {
            Assert.Throws<ArgumentException>(() => new ThirdwebEmbeddedWallet(" "));
        }

        [Test]
        public void ConnectRejectsMissingFirebaseTokenWithoutOpeningSession()
        {
            IEmbeddedWallet wallet = new ThirdwebEmbeddedWallet("test-client-id");
            Assert.ThrowsAsync<ArgumentException>(async () => { await wallet.Connect(" "); });
        }

        [Test]
        public void SignMessageRequiresConnectedWallet()
        {
            IEmbeddedWallet wallet = new ThirdwebEmbeddedWallet("test-client-id");
            Assert.ThrowsAsync<InvalidOperationException>(async () => { await wallet.SignMessage("bind challenge"); });
        }

        [Test]
        public async Task DisconnectWithoutSessionIsIdempotent()
        {
            IEmbeddedWallet wallet = new ThirdwebEmbeddedWallet("test-client-id");
            await wallet.Disconnect();
            await wallet.Disconnect();
        }

        [Test]
        public async Task SessionStorageRemovesLegacyAndAuthenticatedFiles()
        {
            var testRoot = Path.Combine(Path.GetTempPath(), "tagtag-wallet-" + Guid.NewGuid().ToString("N"));
            var cacheRoot = Path.Combine(testRoot, "cache");
            var persistentRoot = Path.Combine(testRoot, "persistent");
            var legacyDirectory = Path.Combine(persistentRoot, "Thirdweb", "InAppWallet");
            Directory.CreateDirectory(legacyDirectory);
            var legacyFile = Path.Combine(legacyDirectory, "test-client-id.txt");
            File.WriteAllText(legacyFile, "sensitive-session");
            var staleDirectory = Path.Combine(cacheRoot, "TagtagThirdwebSessions", "stale");
            Directory.CreateDirectory(staleDirectory);
            File.WriteAllText(Path.Combine(staleDirectory, "old.txt"), "sensitive-session");

            try
            {
                var storage = WalletSessionStorage.Prepare("test-client-id", cacheRoot, persistentRoot);
                Assert.False(File.Exists(legacyFile));
                Assert.False(Directory.Exists(staleDirectory));
                Assert.True(storage.SessionDirectory.StartsWith(cacheRoot, StringComparison.Ordinal));
                Assert.True(File.Exists(storage.SessionFile));

                File.WriteAllText(storage.SessionFile, "sensitive-session");
                await storage.RemoveAuthenticatedSessionFile();
                Assert.False(File.Exists(storage.SessionFile));

                storage.Cleanup();
                Assert.False(Directory.Exists(storage.SessionDirectory));
            }
            finally
            {
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            }
        }

        [Test]
        public void SessionStorageRejectsCacheInsidePersistentData()
        {
            var persistentRoot = Path.Combine(Path.GetTempPath(), "tagtag-wallet-" + Guid.NewGuid().ToString("N"));
            var nestedCache = Path.Combine(persistentRoot, "Cache");

            Assert.Throws<InvalidOperationException>(() =>
                WalletSessionStorage.Prepare("test-client-id", nestedCache, persistentRoot));
            Assert.False(Directory.Exists(nestedCache));
        }
    }
}
