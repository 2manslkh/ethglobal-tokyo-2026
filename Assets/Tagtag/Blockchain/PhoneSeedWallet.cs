using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Signer;
using Thirdweb;
using Thirdweb.Unity;
using UnityEngine;

namespace Tagtag.Blockchain
{
    public sealed class WalletRecoveryRequiredException : Exception { }

    /// <summary>
    /// A BIP-39 wallet generated on the phone and stored in iOS Keychain. Thirdweb
    /// provides Sepolia RPC and transaction signing only; no Thirdweb auth is used.
    /// </summary>
    public sealed partial class PhoneSeedWallet : IEmbeddedWallet
    {
        private const string SdkVersion = "6.1.3";
        private readonly string clientId;
        private readonly string bundleId;
        private readonly string platform;
        private readonly IPhoneSeedStore seedStore;
        private readonly Bip39Wallet mnemonic;
        private readonly SemaphoreSlim sessionGate = new SemaphoreSlim(1, 1);
        private EthECKey wallet;
        private ThirdwebClient client;
        private string connectedUser;

        public PhoneSeedWallet(string clientId, IPhoneSeedStore seedStore = null, string wordList = null)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("A Sepolia RPC client ID is required.", nameof(clientId));
            this.clientId = clientId;
            this.seedStore = seedStore ?? new PhoneSeedStore();
            mnemonic = new Bip39Wallet(wordList ?? Resources.Load<TextAsset>("Tagtag/bip39-english").text);
            bundleId = Application.identifier;
            platform = Application.platform.ToString();
        }

        public async Task<string> Connect(string userId, string expectedAddress)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("A user ID is required.", nameof(userId));
            await sessionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisconnectCurrentWallet().ConfigureAwait(false);
                string phrase = seedStore.Load(userId);
                if (phrase == null && !string.IsNullOrEmpty(expectedAddress))
                    throw new WalletRecoveryRequiredException();
                bool newWallet = phrase == null;
                if (newWallet) phrase = mnemonic.Create();
                var candidate = OpenWallet(phrase);
                string address = candidate.GetPublicAddress();
                if (!string.IsNullOrEmpty(expectedAddress) &&
                    !string.Equals(expectedAddress, address, StringComparison.OrdinalIgnoreCase))
                    throw new WalletRecoveryRequiredException();
                if (newWallet) seedStore.Save(userId, phrase);
                wallet = candidate;
                connectedUser = userId;
                return address;
            }
            finally { sessionGate.Release(); }
        }

        public async Task<string> Restore(string userId, string phrase, string expectedAddress)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("A user ID is required.");
            string normalized = mnemonic.Normalize(phrase);
            await sessionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var candidate = OpenWallet(normalized);
                string address = candidate.GetPublicAddress();
                if (!string.IsNullOrEmpty(expectedAddress) &&
                    !string.Equals(expectedAddress, address, StringComparison.OrdinalIgnoreCase))
                    throw new WalletRecoveryRequiredException();
                seedStore.Save(userId, normalized);
                await DisconnectCurrentWallet().ConfigureAwait(false);
                wallet = candidate;
                connectedUser = userId;
                return address;
            }
            finally { sessionGate.Release(); }
        }

        public string RecoveryPhrase(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || userId != connectedUser || wallet == null)
                throw new InvalidOperationException("Sign in and connect this wallet first.");
            string phrase = seedStore.Load(userId);
            if (phrase == null) throw new WalletRecoveryRequiredException();
            return mnemonic.Normalize(phrase);
        }

        public async Task<string> SignMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("A message is required.");
            await sessionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var connected = await RequireConnectedWallet().ConfigureAwait(false);
                return new EthereumMessageSigner().EncodeUTF8AndSign(message, connected);
            }
            finally { sessionGate.Release(); }
        }

        public async Task Disconnect()
        {
            await sessionGate.WaitAsync().ConfigureAwait(false);
            try { await DisconnectCurrentWallet().ConfigureAwait(false); }
            finally { sessionGate.Release(); }
        }

        private EthECKey OpenWallet(string phrase)
        {
            byte[] key = mnemonic.DerivePrivateKey(phrase);
            try
            {
                client = ThirdwebClient.Create(clientId: clientId, bundleId: bundleId,
                    httpClient: new CrossPlatformUnityHttpClient(), sdkName: "UnitySDK",
                    sdkOs: platform, sdkPlatform: "unity", sdkVersion: SdkVersion);
                return new EthECKey(key, true);
            }
            finally { Array.Clear(key, 0, key.Length); }
        }

        private Task DisconnectCurrentWallet()
        {
            wallet = null;
            client = null;
            connectedUser = null;
            return Task.CompletedTask;
        }
    }
}
