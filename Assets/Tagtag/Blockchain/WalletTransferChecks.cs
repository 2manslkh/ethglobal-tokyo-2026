using System;
using System.Globalization;
using System.Numerics;

namespace Tagtag.Blockchain
{
    internal static class WalletTransferChecks
    {
        internal const int SepoliaChainId = 11155111;
        private static readonly BigInteger MaxTokenId = (BigInteger.One << 256) - BigInteger.One;

        internal static string ValidateContract(string address)
        {
            if (!EthereumAddress.IsValid(address))
            {
                throw new WalletTransferException("invalid_contract", "The NFT contract address is invalid.");
            }

            return address;
        }

        internal static string ValidateRecipient(string recipient, string owner)
        {
            if (!EthereumAddress.IsValid(recipient) ||
                (owner != null && string.Equals(recipient, owner, StringComparison.OrdinalIgnoreCase)))
            {
                throw new WalletTransferException("invalid_recipient", "Choose a different valid Ethereum address.");
            }

            return recipient;
        }

        internal static BigInteger ParseTokenId(string tokenId)
        {
            BigInteger value;
            if (string.IsNullOrEmpty(tokenId) || !BigInteger.TryParse(tokenId, NumberStyles.None,
                CultureInfo.InvariantCulture, out value) || value < 0 || value > MaxTokenId)
            {
                throw new WalletTransferException("invalid_token_id", "The NFT token ID is invalid.");
            }

            return value;
        }

        internal static BigInteger ParseRpcQuantity(string quantity)
        {
            if (string.IsNullOrEmpty(quantity) || !quantity.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                quantity.Length < 3 || !BigInteger.TryParse("0" + quantity.Substring(2),
                    NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value) || value < 0)
            {
                throw new WalletTransferException("provider_error", "The Sepolia provider returned invalid data.");
            }

            return value;
        }

        internal static void RequireSepolia(string rpcChainId)
        {
            if (ParseRpcQuantity(rpcChainId) != SepoliaChainId)
            {
                throw new WalletTransferException("wrong_chain", "Wallet transfers are available on Sepolia only.");
            }
        }

        internal static void RequireGas(BigInteger balanceWei, BigInteger maximumGasWei)
        {
            if (maximumGasWei <= 0 || balanceWei < maximumGasWei)
            {
                throw new WalletTransferException("needs_gas", "Add Sepolia ETH to this wallet for transfer gas.");
            }
        }

        internal static string ValidateTransactionHash(string transactionHash)
        {
            if (!IsHex(transactionHash, 64))
            {
                throw new WalletTransferException("invalid_transaction_hash", "The transfer hash is invalid.");
            }

            return transactionHash;
        }

        internal static string DecodeOwner(string result)
        {
            if (!IsHex(result, 64) || result.Substring(2, 24) != "000000000000000000000000")
            {
                throw new WalletTransferException("provider_error", "The Sepolia provider returned invalid NFT owner data.");
            }

            var address = "0x" + result.Substring(26, 40).ToLowerInvariant();
            if (IsZeroAddress(address))
            {
                throw new WalletTransferException("provider_error", "The Sepolia provider returned invalid NFT owner data.");
            }

            return address;
        }

        internal static string EvaluateReceipt(string receiptBlockHash, BigInteger receiptBlockNumber,
            int receiptStatus, string canonicalBlockHash, BigInteger finalizedBlockNumber)
        {
            if (!IsHex(receiptBlockHash, 64) || receiptBlockNumber < 0 || finalizedBlockNumber < 0 ||
                (receiptStatus != 0 && receiptStatus != 1))
            {
                throw new WalletTransferException("provider_error", "The Sepolia provider returned invalid receipt data.");
            }

            if (finalizedBlockNumber < receiptBlockNumber || !string.Equals(receiptBlockHash,
                    canonicalBlockHash, StringComparison.OrdinalIgnoreCase))
            {
                if (finalizedBlockNumber >= receiptBlockNumber && !IsHex(canonicalBlockHash, 64))
                {
                    throw new WalletTransferException("provider_error", "The Sepolia provider returned invalid block data.");
                }

                return "pending";
            }

            return receiptStatus == 1 ? "confirmed" : "failed";
        }

        private static bool IsZeroAddress(string address)
        {
            for (var i = 2; i < address.Length; i++)
            {
                if (address[i] != '0') return false;
            }

            return true;
        }

        private static bool IsHex(string value, int digits)
        {
            if (value == null || value.Length != digits + 2 || value[0] != '0' || value[1] != 'x') return false;
            for (var i = 2; i < value.Length; i++)
            {
                var c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
