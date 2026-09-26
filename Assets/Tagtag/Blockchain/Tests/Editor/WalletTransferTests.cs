using System;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tagtag.Blockchain.Tests
{
    public sealed class WalletTransferTests
    {
        private const string Owner = "0x1111111111111111111111111111111111111111";
        private const string Recipient = "0x2222222222222222222222222222222222222222";
        private const string Zero = "0x0000000000000000000000000000000000000000";
        private const string BlockHash = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Test]
        public void RecipientMustBeDistinctNonzeroEthereumAddress()
        {
            Assert.AreEqual("invalid_recipient", Assert.Throws<WalletTransferException>(() =>
                WalletTransferChecks.ValidateRecipient("not-an-address", Owner)).Code);
            Assert.AreEqual("invalid_recipient", Assert.Throws<WalletTransferException>(() =>
                WalletTransferChecks.ValidateRecipient(Zero, Owner)).Code);
            Assert.AreEqual("invalid_recipient", Assert.Throws<WalletTransferException>(() =>
                WalletTransferChecks.ValidateRecipient(Owner.ToUpperInvariant().Replace("0X", "0x"), Owner)).Code);
            Assert.AreEqual(Recipient, WalletTransferChecks.ValidateRecipient(Recipient, Owner));
        }

        [Test]
        public void TokenIdMustBeDecimalUint256()
        {
            var max = (BigInteger.One << 256) - BigInteger.One;
            Assert.AreEqual(max, WalletTransferChecks.ParseTokenId(max.ToString()));
            Assert.AreEqual("invalid_token_id", Assert.Throws<WalletTransferException>(() =>
                WalletTransferChecks.ParseTokenId("-1")).Code);
            Assert.AreEqual("invalid_token_id", Assert.Throws<WalletTransferException>(() =>
                WalletTransferChecks.ParseTokenId((max + 1).ToString())).Code);
        }

        [Test]
        public void GasPreflightRequiresSepoliaEth()
        {
            Assert.AreEqual("needs_gas", Assert.Throws<WalletTransferException>(() =>
                WalletTransferChecks.RequireGas(new BigInteger(99), new BigInteger(100))).Code);
            Assert.DoesNotThrow(() => WalletTransferChecks.RequireGas(new BigInteger(100), new BigInteger(100)));
        }

        [Test]
        public void ReceiptIsConfirmedOnlyInCanonicalFinalizedBlock()
        {
            Assert.AreEqual("pending", WalletTransferChecks.EvaluateReceipt(BlockHash, new BigInteger(10), 1,
                BlockHash, new BigInteger(9)));
            Assert.AreEqual("pending", WalletTransferChecks.EvaluateReceipt(BlockHash, new BigInteger(10), 1,
                "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", new BigInteger(10)));
            Assert.AreEqual("confirmed", WalletTransferChecks.EvaluateReceipt(BlockHash, new BigInteger(10), 1,
                BlockHash, new BigInteger(10)));
            Assert.AreEqual("failed", WalletTransferChecks.EvaluateReceipt(BlockHash, new BigInteger(10), 0,
                BlockHash, new BigInteger(10)));
        }

        [Test]
        public void WrongChainFailsClosed()
        {
            Assert.AreEqual("wrong_chain", Assert.Throws<WalletTransferException>(() =>
                WalletTransferChecks.RequireSepolia("0x1")).Code);
            Assert.DoesNotThrow(() => WalletTransferChecks.RequireSepolia("0xaa36a7"));
        }

        [Test]
        public void FinalizedOwnerResponseDecodesToAddress()
        {
            var encoded = "0x000000000000000000000000" + Owner.Substring(2);
            Assert.AreEqual(Owner, WalletTransferChecks.DecodeOwner(encoded));
            Assert.AreEqual("provider_error", Assert.Throws<WalletTransferException>(() =>
                WalletTransferChecks.DecodeOwner("0x")).Code);
        }

        [Test]
        public void InvalidRecipientAndHashFailBeforeNetwork()
        {
            IEmbeddedWallet wallet = new ThirdwebEmbeddedWallet("test-client-id");
            var transfer = Assert.ThrowsAsync<WalletTransferException>(async () =>
            {
                await wallet.TransferNft(Owner, "1", Zero);
            });
            Assert.AreEqual("invalid_recipient", transfer.Code);

            var status = Assert.ThrowsAsync<WalletTransferException>(async () =>
            {
                await wallet.GetTransferStatus("bad-hash");
            });
            Assert.AreEqual("invalid_transaction_hash", status.Code);
        }
    }
}
