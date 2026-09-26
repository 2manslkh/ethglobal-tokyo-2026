using System.Text.RegularExpressions;

namespace Tagtag.UI
{
    public static class NftPresentation
    {
        public static bool RequiresAcknowledgement(AppState state) => state.nftEnabled || state.collection.Exists(item => !string.IsNullOrEmpty(item.nft?.status));
        public static bool CanDelete(AppState state, string confirmation) => confirmation == "DELETE" && !state.busy &&
            (!RequiresAcknowledgement(state) || state.nftDeletionAcknowledged);
        public static string WalletExplorerUrl(string address) => Regex.IsMatch(address ?? "", "\\A0x[0-9a-fA-F]{40}\\z")
            ? "https://sepolia.etherscan.io/address/" + address : "";
        public static bool ValidRecipient(string address, string wallet) => Tagtag.Blockchain.EthereumAddress.IsValid(address) &&
            !string.Equals(address, wallet, System.StringComparison.OrdinalIgnoreCase) && address != "0x" + new string('0', 40);
        public static string Status(NftStatus nft)
        {
            if (nft == null || string.IsNullOrEmpty(nft.status)) return "";
            switch (nft.status)
            {
                case "confirmed": return "Souvenir NFT minted on Sepolia";
                case "cancelled": return "Souvenir NFT unavailable";
                case "delayed": return "Souvenir NFT delayed. Your discovery is saved; minting will retry.";
                default: return "Souvenir NFT pending. Your discovery is already in your book.";
            }
        }

        public static string ExplorerUrl(NftStatus nft)
        {
            return nft != null && nft.chainId == 11155111 &&
                Regex.IsMatch(nft.transactionHash ?? "", "\\A0x[0-9a-fA-F]{64}\\z")
                ? "https://sepolia.etherscan.io/tx/" + nft.transactionHash : "";
        }

        public static string WalletStatus(AppState state)
        {
            switch (state.walletStatus)
            {
                case "ready": return state.walletAddress;
                case "disabled": return "NFT minting is not enabled yet.";
                case "needsRecovery": return "Restore your 12-word wallet phrase to receive your NFTs on this phone.";
                case "delayed": return "Wallet setup delayed. Your discoveries are safe; setup will retry.";
                default: return "Preparing your wallet…";
            }
        }
    }
}
