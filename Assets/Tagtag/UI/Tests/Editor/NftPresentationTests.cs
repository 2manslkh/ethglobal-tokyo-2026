using NUnit.Framework;
using UnityEngine;

namespace Tagtag.UI.Tests
{
    public sealed class NftPresentationTests
    {
        [Test]
        public void NftDeletionRequiresAnExplicitAccessLossAcknowledgement()
        {
            var state = new AppState { nftEnabled = true };
            Assert.IsFalse(NftPresentation.CanDelete(state, "DELETE"));
            state.nftDeletionAcknowledged = true;
            Assert.IsTrue(NftPresentation.CanDelete(state, "DELETE"));
            state.busy = true;
            Assert.IsFalse(NftPresentation.CanDelete(state, "DELETE"));
        }

        [Test]
        public void TransferUiRejectsChecksumInvalidRecipient()
        {
            const string wallet = "0x1111111111111111111111111111111111111111";
            Assert.IsTrue(NftPresentation.ValidRecipient("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", wallet));
            Assert.IsFalse(NftPresentation.ValidRecipient("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAee", wallet));
        }

        [Test]
        public void ProfileAddressOpensOnlyAValidSepoliaEtherscanPage()
        {
            const string address = "0xBC5fc5e8EBd5611DdE4b56C88236F5878E85bACb";
            Assert.AreEqual("https://sepolia.etherscan.io/address/" + address,
                NftPresentation.WalletExplorerUrl(address));
            Assert.IsEmpty(NftPresentation.WalletExplorerUrl("https://example.com"));
            Assert.IsEmpty(NftPresentation.WalletExplorerUrl(""));
        }
        [Test]
        public void OldCollectionsRemainReadableWithoutNftFields()
        {
            var sticker = JsonUtility.FromJson<CollectedSticker>("{\"id\":\"old\",\"note\":\"private\"}");
            Assert.AreEqual("", NftPresentation.Status(sticker.nft));
            Assert.AreEqual("private", sticker.note);
        }

        [Test]
        public void PendingToConfirmedRefreshesTheSameCollectedSticker()
        {
            var sticker = new CollectedSticker { id = "one", nft = new NftStatus { status = "pending" } };
            string pending = CollectionPresentation.DetailKey(sticker, false);
            sticker.nft.status = "confirmed";
            Assert.AreNotEqual(pending, CollectionPresentation.DetailKey(sticker, false));
            StringAssert.Contains("Sepolia", NftPresentation.Status(sticker.nft));
        }

        [Test]
        public void ExplorerLinksAreRestrictedToSepoliaAndValidatedTransactionHashes()
        {
            var nft = new NftStatus { status = "confirmed", chainId = 11155111, transactionHash = "0x" + new string('a', 64) };
            Assert.AreEqual("https://sepolia.etherscan.io/tx/" + nft.transactionHash, NftPresentation.ExplorerUrl(nft));
            nft.chainId = 1;
            Assert.IsEmpty(NftPresentation.ExplorerUrl(nft));
            nft.chainId = 11155111;
            nft.transactionHash = "https://attacker.example";
            Assert.IsEmpty(NftPresentation.ExplorerUrl(nft));
        }
    }
}
