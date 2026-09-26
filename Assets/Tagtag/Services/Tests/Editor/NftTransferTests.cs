using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tagtag.Services.Tests
{
    public sealed class NftTransferTests
    {
        private const string Owner = "0x1111111111111111111111111111111111111111";
        private const string Recipient = "0x2222222222222222222222222222222222222222";
        private static CollectedSticker Sticker => new CollectedSticker { id = "one", nft = new NftStatus
            { status = "confirmed", chainId = 11155111, contractAddress = Owner, tokenId = "42" } };

        [Test]
        public async Task TransferIntentIsSavedBeforeBroadcastAndPendingHashIsRetained()
        {
            var records = new List<NftTransfer>();
            var transfers = new NftTransfers((uid, items) => records = new List<NftTransfer>(items),
                (contract, id) => Task.FromResult(Owner),
                (contract, id, recipient) =>
                {
                    Assert.AreEqual("submitting", records[0].status);
                    Assert.AreEqual(Recipient, records[0].recipient);
                    return Task.FromResult("0x" + new string('a', 64));
                }, _ => Task.FromResult("pending"));
            await transfers.Send("user", Owner, Sticker, Recipient, records);
            Assert.AreEqual("pending", records[0].status);
            Assert.IsNotEmpty(records[0].transactionHash);
        }

        [Test]
        public async Task UncertainSubmissionIsNeverAutomaticallySentAgain()
        {
            int sends = 0;
            var records = new List<NftTransfer>();
            var transfers = new NftTransfers((uid, items) => { }, (contract, id) => Task.FromResult(Owner),
                (contract, id, recipient) => { sends++; throw new Exception("provider message"); }, _ => Task.FromResult("pending"));
            await transfers.Send("user", Owner, Sticker, Recipient, records);
            Assert.AreEqual("unknown", records[0].status);
            await transfers.Send("user", Owner, Sticker, Recipient, records);
            Assert.AreEqual(1, sends);
        }

        [Test]
        public async Task FinalizedReceiptMarksTransferConfirmed()
        {
            var records = new List<NftTransfer> { new NftTransfer { stickerId = "one", contractAddress = Owner,
                tokenId = "42", recipient = Recipient, transactionHash = "0x" + new string('a', 64), status = "pending" } };
            var transfers = new NftTransfers((uid, items) => { }, (contract, id) => Task.FromResult(Recipient),
                (contract, id, recipient) => throw new Exception(), _ => Task.FromResult("confirmed"));
            await transfers.Refresh("user", Owner, records);
            Assert.AreEqual("confirmed", records[0].status);
        }

        [Test]
        public void InvalidOrSelfRecipientsAreRejected()
        {
            Assert.IsFalse(NftTransfers.ValidRecipient(Owner, Owner));
            Assert.IsFalse(NftTransfers.ValidRecipient("0x" + new string('0', 40), Owner));
            Assert.IsFalse(NftTransfers.ValidRecipient("not an address", Owner));
            Assert.IsTrue(NftTransfers.ValidRecipient(Recipient, Owner));
            Assert.IsTrue(NftTransfers.ValidRecipient("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", Owner));
            Assert.IsFalse(NftTransfers.ValidRecipient("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAee", Owner));
        }

        [Test]
        public async Task FailedIntentPersistenceDoesNotBlockALaterSafeRetry()
        {
            var records = new List<NftTransfer>();
            bool failSave = true;
            int sends = 0;
            var transfers = new NftTransfers((uid, items) => { if (failSave) throw new IOException(); },
                (contract, id) => Task.FromResult(Owner),
                (contract, id, recipient) => { sends++; return Task.FromResult("0x" + new string('a', 64)); },
                _ => Task.FromResult("pending"));
            Assert.ThrowsAsync<IOException>(async () => await transfers.Send("user", Owner, Sticker, Recipient, records));
            Assert.IsEmpty(records);
            Assert.AreEqual(0, sends);
            failSave = false;
            await transfers.Send("user", Owner, Sticker, Recipient, records);
            Assert.AreEqual(1, sends);
        }

        [Test]
        public async Task RestartRetainsUncertainIntentWithoutLeakingItToAnotherAccount()
        {
            string directory = Path.Combine(Path.GetTempPath(), "tagtag-transfers-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new NftTransferStore(directory);
                store.Save("first", new List<NftTransfer> { new NftTransfer { stickerId = "one", status = "submitting",
                    contractAddress = Owner, tokenId = "42", recipient = Recipient } });
                var records = new NftTransferStore(directory).Read("first");
                Assert.IsEmpty(store.Read("second"));
                int sends = 0;
                var transfers = new NftTransfers(store.Save, (contract, id) => Task.FromResult(Owner),
                    (contract, id, recipient) => { sends++; return Task.FromResult("unused"); }, _ => Task.FromResult("pending"));
                await transfers.Send("first", Owner, Sticker, Recipient, records);
                Assert.AreEqual(0, sends);
                await transfers.Refresh("first", Owner, records);
                Assert.AreEqual("unknown", store.Read("first")[0].status);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
