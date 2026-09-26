using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.Blockchain.Tests
{
    public sealed class PhoneSeedWalletTests
    {
        private const string TestPhrase = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string TestAddress = "0x9858EfFD232B4033E47d90003D41EC34EcaEda94";
        private sealed class MemoryStore : IPhoneSeedStore
        {
            public string Phrase;
            public int Saves;
            public string Load(string userId) => Phrase;
            public void Save(string userId, string phrase) { Phrase = phrase; Saves++; }
        }

        private static PhoneSeedWallet Create(MemoryStore store) => new PhoneSeedWallet("test-client-id", store,
            Resources.Load<TextAsset>("Tagtag/bip39-english").text);

        [Test]
        public void ConstructorRejectsMissingClientId()
        {
            Assert.Throws<ArgumentException>(() => new PhoneSeedWallet(" "));
        }

        [Test]
        public void ExistingBindingRequiresRecoveryBeforeGeneratingAnyWords()
        {
            var store = new MemoryStore();
            var wallet = Create(store);
            Assert.ThrowsAsync<WalletRecoveryRequiredException>(async () =>
                await wallet.Connect("collector", "0x1111111111111111111111111111111111111111"));
            Assert.IsNull(store.Phrase);
            Assert.AreEqual(0, store.Saves);
        }

        [Test]
        public void SignMessageRequiresConnectedWallet()
        {
            IEmbeddedWallet wallet = Create(new MemoryStore());
            Assert.ThrowsAsync<WalletTransferException>(async () => await wallet.SignMessage("bind challenge"));
        }

        [Test]
        public async Task DisconnectWithoutSessionIsIdempotent()
        {
            IEmbeddedWallet wallet = Create(new MemoryStore());
            await wallet.Disconnect();
            await wallet.Disconnect();
        }

        [Test]
        public async Task KnownPhraseConnectsToStandardEthereumAddressAndSignsBindingChallenge()
        {
            var store = new MemoryStore { Phrase = TestPhrase };
            var wallet = Create(store);
            string address = await wallet.Connect("collector", TestAddress);
            Assert.AreEqual(TestAddress, address);
            Assert.AreEqual(TestPhrase, wallet.RecoveryPhrase("collector"));
            Assert.AreEqual("0xea7f0b40faf149ffd18c941943f62b67455e06ff7d600b2dc05a18163733ba7962967c659ffb030b7467cc291271f81f3708b6ba790c45ea15065c065868fcd01c",
                await wallet.SignMessage("Bind wallet"));
            Assert.AreEqual(0, store.Saves);
            await wallet.Disconnect();
        }

        [Test]
        public void WrongStoredPhraseCannotReplaceBoundAddress()
        {
            var store = new MemoryStore { Phrase = TestPhrase };
            var wallet = Create(store);
            Assert.ThrowsAsync<WalletRecoveryRequiredException>(async () =>
                await wallet.Connect("collector", "0x1111111111111111111111111111111111111111"));
            Assert.AreEqual(0, store.Saves);
        }
    }
}
