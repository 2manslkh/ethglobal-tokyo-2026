using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.Blockchain.Tests
{
    public sealed class Bip39WalletTests
    {
        private Bip39Wallet wallet;

        [SetUp]
        public void SetUp()
        {
            wallet = new Bip39Wallet(Resources.Load<TextAsset>("Tagtag/bip39-english").text);
        }

        [Test]
        public void PublishedZeroEntropyVectorAndEthereumDerivation()
        {
            const string phrase = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            Assert.AreEqual(phrase, wallet.Encode(new byte[16]));
            Assert.AreEqual(phrase, wallet.Normalize("  ABANDON  abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about  "));
            Assert.AreEqual("1ab42cc412b618bdea3a599e3c9bae199ebf030895b039e9db1e30dafb12b727",
                BitConverter.ToString(wallet.DerivePrivateKey(phrase)).Replace("-", "").ToLowerInvariant());
        }

        [Test]
        public void RejectsInvalidWordCountWordAndChecksum()
        {
            Assert.Throws<ArgumentException>(() => wallet.Normalize("abandon"));
            Assert.Throws<ArgumentException>(() => wallet.Normalize("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon wrongword"));
            Assert.Throws<ArgumentException>(() => wallet.Normalize("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon"));
        }

        [Test]
        public void NewWalletUsesDifferentEntropyAndDerivesAKey()
        {
            string first = wallet.Create(), second = wallet.Create();
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(32, wallet.DerivePrivateKey(first).Length);
        }
    }
}
