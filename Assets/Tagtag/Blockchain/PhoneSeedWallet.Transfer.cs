using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nethereum.Signer;
using Thirdweb;
using Thirdweb.Unity;

namespace Tagtag.Blockchain
{
    public sealed partial class PhoneSeedWallet
    {
        private const string Erc721TransferAbi =
            "[{\"type\":\"function\",\"name\":\"ownerOf\",\"stateMutability\":\"view\",\"inputs\":[{\"name\":\"tokenId\",\"type\":\"uint256\"}],\"outputs\":[{\"name\":\"owner\",\"type\":\"address\"}]}," +
            "{\"type\":\"function\",\"name\":\"safeTransferFrom\",\"stateMutability\":\"nonpayable\",\"inputs\":[{\"name\":\"from\",\"type\":\"address\"},{\"name\":\"to\",\"type\":\"address\"},{\"name\":\"tokenId\",\"type\":\"uint256\"}],\"outputs\":[]}]";

        /// <summary>Returns the ERC-721 owner at Sepolia's finalized block.</summary>
        public async Task<string> GetNftOwner(string contractAddress, string tokenId)
        {
            var contractAddressChecked = WalletTransferChecks.ValidateContract(contractAddress);
            var token = WalletTransferChecks.ParseTokenId(tokenId);

            await sessionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                try
                {
                    var connectedWallet = await RequireConnectedWallet().ConfigureAwait(false);
                    var rpc = ThirdwebRPC.GetRpcInstance(client, WalletTransferChecks.SepoliaChainId);
                    await RequireSepolia(rpc).ConfigureAwait(false);
                    var contract = await ThirdwebContract.Create(client, contractAddressChecked,
                        WalletTransferChecks.SepoliaChainId, Erc721TransferAbi).ConfigureAwait(false);
                    return await ReadOwner(contract, rpc, token, "finalized").ConfigureAwait(false);
                }
                catch (WalletTransferException) { throw; }
                catch (Exception error) { throw PreflightError(error); }
            }
            finally
            {
                sessionGate.Release();
            }
        }

        /// <summary>
        /// Returns the hash when Sepolia accepts the broadcast. This does not
        /// wait for mining or finality. The caller must persist intent first.
        /// </summary>
        public async Task<string> TransferNft(string contractAddress, string tokenId, string recipient)
        {
            var contractAddressChecked = WalletTransferChecks.ValidateContract(contractAddress);
            var token = WalletTransferChecks.ParseTokenId(tokenId);
            WalletTransferChecks.ValidateRecipient(recipient, null);

            await sessionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                string signedTransaction;
                ThirdwebRPC rpc;
                try
                {
                    var connectedWallet = await RequireConnectedWallet().ConfigureAwait(false);
                    rpc = ThirdwebRPC.GetRpcInstance(client, WalletTransferChecks.SepoliaChainId);
                    await RequireSepolia(rpc).ConfigureAwait(false);
                    var owner = connectedWallet.GetPublicAddress();
                    WalletTransferChecks.ValidateRecipient(recipient, owner);

                    var contract = await ThirdwebContract.Create(client, contractAddressChecked,
                        WalletTransferChecks.SepoliaChainId, Erc721TransferAbi).ConfigureAwait(false);
                    var chainOwner = await ReadOwner(contract, rpc, token, "latest").ConfigureAwait(false);
                    if (!string.Equals(chainOwner, owner, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new WalletTransferException("not_owner", "This wallet no longer owns the NFT.");
                    }

                    string data = contract.CreateCallData("safeTransferFrom", owner, recipient, token);
                    string gasHex = await rpc.SendRequestAsync<string>("eth_estimateGas",
                        new { from = owner, to = contractAddressChecked, data }).ConfigureAwait(false);
                    var gasLimit = WalletTransferChecks.ParseRpcQuantity(gasHex);
                    // A modest buffer covers state changes between estimation and inclusion.
                    gasLimit = gasLimit * 120 / 100;
                    string priceHex = await rpc.SendRequestAsync<string>("eth_gasPrice").ConfigureAwait(false);
                    var gasPrice = WalletTransferChecks.ParseRpcQuantity(priceHex);
                    if (gasLimit <= 0 || gasPrice <= 0)
                    {
                        throw new WalletTransferException("provider_error", "Sepolia gas estimation is unavailable.");
                    }

                    var balanceHex = await rpc.SendRequestAsync<string>("eth_getBalance", owner, "latest")
                        .ConfigureAwait(false);
                    var balanceWei = WalletTransferChecks.ParseRpcQuantity(balanceHex);
                    WalletTransferChecks.RequireGas(balanceWei, gasLimit * gasPrice);
                    string nonceHex = await rpc.SendRequestAsync<string>("eth_getTransactionCount", owner, "pending")
                        .ConfigureAwait(false);
                    var nonce = WalletTransferChecks.ParseRpcQuantity(nonceHex);
                    byte[] signingKey = connectedWallet.GetPrivateKeyAsBytes();
                    try
                    {
                        signedTransaction = new LegacyTransactionSigner().SignTransaction(signingKey,
                            WalletTransferChecks.SepoliaChainId, contractAddressChecked,
                            BigInteger.Zero, nonce, gasPrice, gasLimit, data);
                    }
                    finally { Array.Clear(signingKey, 0, signingKey.Length); }
                }
                catch (WalletTransferException) { throw; }
                catch (Exception error) { throw PreflightError(error); }

                try
                {
                    var hash = await rpc.SendRequestAsync<string>("eth_sendRawTransaction", signedTransaction)
                        .ConfigureAwait(false);
                    if (string.IsNullOrEmpty(hash))
                    {
                        throw new WalletTransferException("submission_unknown", "Transfer submission is uncertain. Check its status before retrying.");
                    }

                    WalletTransferChecks.ValidateTransactionHash(hash);
                    return hash;
                }
                catch (WalletTransferException error)
                {
                    if (error.Code == "submission_unknown") throw;
                    throw new WalletTransferException("submission_unknown", "Transfer submission is uncertain. Check its status before retrying.");
                }
                catch (Exception error)
                {
                    if (LooksLikeInsufficientFunds(error))
                    {
                        throw new WalletTransferException("needs_gas", "Add Sepolia ETH to this wallet for transfer gas.");
                    }

                    throw new WalletTransferException("submission_unknown", "Transfer submission is uncertain. Check its status before retrying.");
                }
            }
            finally
            {
                sessionGate.Release();
            }
        }

        /// <summary>Returns pending until a canonical Sepolia receipt is finalized.</summary>
        public async Task<string> GetTransferStatus(string transactionHash)
        {
            WalletTransferChecks.ValidateTransactionHash(transactionHash);

            await sessionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Receipt polling is read-only and remains available after
                // wallet disconnect or Firebase account deletion.
                var receiptClient = client ?? ThirdwebClient.Create(
                    clientId: clientId,
                    bundleId: bundleId,
                    httpClient: new CrossPlatformUnityHttpClient(),
                    sdkName: "UnitySDK",
                    sdkOs: platform,
                    sdkPlatform: "unity",
                    sdkVersion: SdkVersion);
                var rpc = ThirdwebRPC.GetRpcInstance(receiptClient, WalletTransferChecks.SepoliaChainId);
                await RequireSepolia(rpc).ConfigureAwait(false);
                var receipt = await rpc.SendRequestAsync<ThirdwebTransactionReceipt>(
                    "eth_getTransactionReceipt", transactionHash).ConfigureAwait(false);
                if (receipt == null) return "pending";
                if (receipt.BlockNumber == null || receipt.Status == null ||
                    !string.Equals(receipt.TransactionHash, transactionHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new WalletTransferException("provider_error", "The Sepolia provider returned invalid receipt data.");
                }

                var finalized = await rpc.SendRequestAsync<JObject>("eth_getBlockByNumber",
                    "finalized", false).ConfigureAwait(false);
                if (finalized == null)
                {
                    throw new WalletTransferException("provider_error", "Sepolia finality data is unavailable.");
                }

                var finalizedNumber = WalletTransferChecks.ParseRpcQuantity(finalized.Value<string>("number"));
                if (finalizedNumber < receipt.BlockNumber.Value) return "pending";

                // Read the canonical block after finality. A receipt from a
                // replaced block must never be reported as confirmed.
                var block = await rpc.SendRequestAsync<JObject>("eth_getBlockByNumber",
                    ToRpcQuantity(receipt.BlockNumber.Value), false).ConfigureAwait(false);
                if (block == null) return "pending";

                var status = receipt.Status.Value == BigInteger.One ? 1 :
                    receipt.Status.Value == BigInteger.Zero ? 0 : -1;
                return WalletTransferChecks.EvaluateReceipt(receipt.BlockHash, receipt.BlockNumber.Value,
                    status, block.Value<string>("hash"), finalizedNumber);
            }
            catch (WalletTransferException) { throw; }
            catch (Exception) { throw new WalletTransferException("provider_error", "Unable to check Sepolia transfer status."); }
            finally
            {
                sessionGate.Release();
            }
        }

        private Task<EthECKey> RequireConnectedWallet()
        {
            if (wallet == null)
            {
                throw new WalletTransferException("not_connected", "Connect the wallet before using NFTs.");
            }
            return Task.FromResult(wallet);
        }

        private static async Task RequireSepolia(ThirdwebRPC rpc)
        {
            var chainId = await rpc.SendRequestAsync<string>("eth_chainId").ConfigureAwait(false);
            WalletTransferChecks.RequireSepolia(chainId);
        }

        private static async Task<string> ReadOwner(ThirdwebContract contract, ThirdwebRPC rpc,
            BigInteger tokenId, string blockTag)
        {
            var data = contract.CreateCallData("ownerOf", tokenId);
            var result = await rpc.SendRequestAsync<string>("eth_call",
                new { to = contract.Address, data }, blockTag).ConfigureAwait(false);
            return WalletTransferChecks.DecodeOwner(result);
        }

        private static string ToRpcQuantity(BigInteger value)
        {
            var hex = value.ToString("x", CultureInfo.InvariantCulture).TrimStart('0');
            return "0x" + (hex.Length == 0 ? "0" : hex);
        }

        private static WalletTransferException PreflightError(Exception error)
        {
            if (LooksLikeInsufficientFunds(error))
            {
                return new WalletTransferException("needs_gas", "Add Sepolia ETH to this wallet for transfer gas.");
            }

            var detail = error.Message ?? string.Empty;
            if (detail.IndexOf("revert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detail.IndexOf("execution failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new WalletTransferException("failed", "The NFT transfer preflight was rejected.");
            }

            return new WalletTransferException("provider_error", "The Sepolia provider is unavailable.");
        }

        private static bool LooksLikeInsufficientFunds(Exception error)
        {
            var detail = error.Message ?? string.Empty;
            return detail.IndexOf("insufficient funds", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detail.IndexOf("insufficient balance", StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }
}
