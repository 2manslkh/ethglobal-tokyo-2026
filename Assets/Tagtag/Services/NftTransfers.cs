using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace Tagtag.Services
{
    public sealed class NftTransferFailure : Exception
    {
        public string Code { get; }
        public NftTransferFailure(string code) : base("NFT transfer could not complete.") { Code = code; }
    }

    public sealed class NftTransfers
    {
        private readonly Action<string, List<NftTransfer>> save;
        private readonly Func<string, string, Task<string>> owner;
        private readonly Func<string, string, string, Task<string>> send;
        private readonly Func<string, Task<string>> receipt;
        public NftTransfers(Action<string, List<NftTransfer>> save, Func<string, string, Task<string>> owner,
            Func<string, string, string, Task<string>> send, Func<string, Task<string>> receipt)
        { this.save = save; this.owner = owner; this.send = send; this.receipt = receipt; }

        public static bool ValidRecipient(string recipient, string wallet) =>
            Tagtag.Blockchain.EthereumAddress.IsValid(recipient) &&
            !string.Equals(recipient, "0x" + new string('0', 40), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(recipient, wallet, StringComparison.OrdinalIgnoreCase);

        public async Task Send(string uid, string wallet, CollectedSticker sticker, string recipient, List<NftTransfer> records)
        {
            if (!ValidRecipient(recipient, wallet) || sticker?.nft?.chainId != 11155111 || sticker.nft.status != "confirmed")
                throw new ApiFailure("Choose a confirmed Sepolia NFT and a different valid Ethereum address.");
            var previous = records.FirstOrDefault(item => item.stickerId == sticker.id);
            if (previous != null && previous.status != "failed") return;
            string currentOwner = await owner(sticker.nft.contractAddress, sticker.nft.tokenId);
            if (!string.Equals(wallet, currentOwner, StringComparison.OrdinalIgnoreCase))
                throw new ApiFailure("This NFT is no longer in your wallet.");
            var transfer = new NftTransfer { stickerId = sticker.id, contractAddress = sticker.nft.contractAddress,
                tokenId = sticker.nft.tokenId, recipient = recipient, status = "submitting", transactionHash = "", message = "Submitting transfer…" };
            // Persistence is a precondition for broadcast. A restart preserves uncertain intent.
            var proposed = records.Where(item => item != previous).ToList();
            proposed.Add(transfer);
            save(uid, proposed);
            if (previous != null) records.Remove(previous);
            records.Add(transfer);
            try
            {
                transfer.transactionHash = await send(transfer.contractAddress, transfer.tokenId, recipient);
                if (!Regex.IsMatch(transfer.transactionHash ?? "", "\\A0x[0-9a-fA-F]{64}\\z")) throw new Exception();
                transfer.status = "pending";
                transfer.message = "Transfer submitted. Waiting for Sepolia confirmation.";
            }
            catch (NftTransferFailure error)
            {
                transfer.status = error.Code == "submission_unknown" ? "unknown" : "failed";
                transfer.message = error.Code == "needs_gas"
                    ? "Add Sepolia test ETH to your souvenir wallet to pay the transfer fee, then retry."
                    : error.Code == "submission_unknown" ? "Submission uncertain. Check your wallet on the explorer before deleting your account."
                    : "Transfer was not sent. Check the recipient and connection, then retry.";
            }
            catch
            {
                transfer.status = "unknown";
                transfer.message = "Submission uncertain. Check your wallet on the explorer before deleting your account.";
            }
            save(uid, records);
        }

        public async Task Refresh(string uid, string wallet, List<NftTransfer> records)
        {
            foreach (var transfer in records.Where(item => item.status == "pending" || item.status == "submitting" || item.status == "unknown"))
            {
                if (!string.IsNullOrEmpty(transfer.transactionHash))
                {
                    string status = await receipt(transfer.transactionHash);
                    if (status == "confirmed")
                    { transfer.status = "confirmed"; transfer.message = "Transfer confirmed on Sepolia."; }
                    else if (status == "failed")
                    { transfer.status = "failed"; transfer.message = "Transfer reverted. The NFT was not transferred; check and retry."; }
                }
                else
                {
                    string currentOwner = await owner(transfer.contractAddress, transfer.tokenId);
                    if (!string.Equals(currentOwner, wallet, StringComparison.OrdinalIgnoreCase))
                    {
                        transfer.status = "confirmed";
                        transfer.message = string.Equals(currentOwner, transfer.recipient, StringComparison.OrdinalIgnoreCase)
                            ? "NFT is in the recipient wallet on finalized Sepolia state."
                            : "NFT is no longer in your wallet. Check its current owner on the explorer.";
                    }
                    else { transfer.status = "unknown"; transfer.message = "Submission uncertain. Check your wallet on the explorer before deleting your account."; }
                }
                save(uid, records);
            }
        }
    }

    public sealed class NftTransferStore
    {
        private readonly string directory;
        public NftTransferStore(string directory) { this.directory = directory; }
        [Serializable] private sealed class Saved { public List<NftTransfer> items; }
        private string PathFor(string uid)
        {
            using (var sha = SHA256.Create())
                return Path.Combine(directory, BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(uid))).Replace("-", "") + ".json");
        }
        public List<NftTransfer> Read(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return new List<NftTransfer>();
            string path = PathFor(uid);
            foreach (string candidate in new[] { path, path + ".backup", path + ".tmp" })
                if (File.Exists(candidate))
                    try { return JsonUtility.FromJson<Saved>(File.ReadAllText(candidate))?.items ?? new List<NftTransfer>(); }
                    catch { }
            return new List<NftTransfer>();
        }
        public void Save(string uid, List<NftTransfer> records)
        {
            Directory.CreateDirectory(directory);
            string path = PathFor(uid);
            try
            {
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(new Saved { items = records }));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".backup");
                else File.Move(path + ".tmp", path);
            }
            catch
            {
                try { if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); } catch { }
                throw;
            }
        }
        public void Remove(string uid)
        {
            string path = PathFor(uid);
            foreach (string candidate in new[] { path, path + ".backup", path + ".tmp" })
                if (File.Exists(candidate)) File.Delete(candidate);
        }
    }
}
