using System;
using System.Threading;
using System.Threading.Tasks;
using Thirdweb;
using Thirdweb.Unity;
using UnityEngine;

namespace Tagtag.Blockchain
{
    /// <summary>
    /// A Firebase JWT-backed thirdweb in-app EOA. The coordinator owns Firebase
    /// token refresh, backend binding, and account-generation checks.
    /// </summary>
    public sealed class ThirdwebEmbeddedWallet : IEmbeddedWallet
    {
        private const string SdkVersion = "6.1.3";

        private readonly string clientId;
        private readonly SemaphoreSlim sessionGate = new SemaphoreSlim(1, 1);
        private InAppWallet wallet;
        private WalletSessionStorage sessionStorage;

        public ThirdwebEmbeddedWallet(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("A thirdweb client ID is required.", nameof(clientId));
            }

            this.clientId = clientId;
        }

        public async Task<string> Connect(string firebaseIdToken)
        {
            if (string.IsNullOrWhiteSpace(firebaseIdToken))
            {
                throw new ArgumentException("A Firebase ID token is required.", nameof(firebaseIdToken));
            }

            // Unity application properties must be read before an asynchronous
            // continuation can leave the Unity thread.
            var bundleId = Application.identifier;
            var platform = Application.platform.ToString();
            var temporaryCachePath = Application.temporaryCachePath;
            var persistentDataPath = Application.persistentDataPath;

            await sessionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Always authenticate the supplied JWT. A cached thirdweb session
                // could belong to a different Firebase user on a shared device.
                if (wallet != null)
                {
                    await DisconnectCurrentWallet().ConfigureAwait(false);
                }

                var storage = WalletSessionStorage.Prepare(clientId, temporaryCachePath, persistentDataPath);

                InAppWallet candidate = null;
                try
                {
                    var client = ThirdwebClient.Create(
                        clientId: clientId,
                        bundleId: bundleId,
                        httpClient: new CrossPlatformUnityHttpClient(),
                        sdkName: "UnitySDK",
                        sdkOs: platform,
                        sdkPlatform: "unity",
                        sdkVersion: SdkVersion
                    );

                    candidate = await InAppWallet.Create(
                        client: client,
                        authProvider: AuthProvider.JWT,
                        storageDirectoryPath: storage.SessionDirectory,
                        executionMode: ExecutionMode.EOA
                    ).ConfigureAwait(false);

                    var address = await candidate.LoginWithJWT(firebaseIdToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        throw new InvalidOperationException("Thirdweb did not return a wallet address.");
                    }

                    // The SDK has already loaded its auth token into memory.
                    // Its file is no longer needed for signing or disconnect.
                    await storage.RemoveAuthenticatedSessionFile().ConfigureAwait(false);
                    wallet = candidate;
                    sessionStorage = storage;
                    return address;
                }
                catch
                {
                    if (candidate != null)
                    {
                        try
                        {
                            await candidate.Disconnect().ConfigureAwait(false);
                        }
                        catch
                        {
                            // Preserve the authentication failure for the caller.
                        }
                    }

                    storage.Cleanup();
                    throw;
                }
            }
            finally
            {
                sessionGate.Release();
            }
        }

        public async Task<string> SignMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A message is required.", nameof(message));
            }

            await sessionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (wallet == null || !await wallet.IsConnected().ConfigureAwait(false))
                {
                    throw new InvalidOperationException("The embedded wallet is not connected.");
                }

                return await wallet.PersonalSign(message).ConfigureAwait(false);
            }
            finally
            {
                sessionGate.Release();
            }
        }

        public async Task Disconnect()
        {
            await sessionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisconnectCurrentWallet().ConfigureAwait(false);
            }
            finally
            {
                sessionGate.Release();
            }
        }

        private async Task DisconnectCurrentWallet()
        {
            try
            {
                if (wallet != null) await wallet.Disconnect().ConfigureAwait(false);
            }
            finally
            {
                wallet = null;
                try
                {
                    sessionStorage?.Cleanup();
                }
                finally
                {
                    sessionStorage = null;
                }
            }
        }
    }
}
