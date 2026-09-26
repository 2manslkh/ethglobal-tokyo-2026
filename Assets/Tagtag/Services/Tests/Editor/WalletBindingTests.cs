using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tagtag.Services.Tests
{
    public sealed class WalletBindingTests
    {
        private const string Address = "0x1111111111111111111111111111111111111111";

        [Test]
        public async Task BindsAnOwnedWalletWithoutPuttingItInTheGameplayBusyState()
        {
            int bindings = 0;
            var service = new WalletBinding(_ => Task.FromResult(Address), message => Task.FromResult("signature"),
                () => Task.CompletedTask, _ => Task.FromResult(new WalletStatus { enabled = true, chainId = 11155111 }),
                (address, token) => Task.FromResult(new WalletChallenge { challengeId = "challenge", message = "Bind wallet" }),
                (challenge, signature, token) => { bindings++; return Task.FromResult(new WalletStatus { enabled = true, address = Address, chainId = 11155111 }); });
            await service.Ensure("collector", () => Task.FromResult("jwt"));
            Assert.AreEqual(Address, service.Address);
            Assert.AreEqual("ready", service.Status);
            await service.Ensure("collector", () => Task.FromResult("jwt"));
            Assert.AreEqual(1, bindings);
        }

        [Test]
        public async Task SigningOutDuringWalletCreationDiscardsTheOldAccountAndDisconnects()
        {
            var connection = new TaskCompletionSource<string>();
            int bindings = 0, disconnects = 0;
            var service = new WalletBinding(_ => connection.Task, _ => Task.FromResult("signature"),
                () => { disconnects++; return Task.CompletedTask; },
                _ => Task.FromResult(new WalletStatus { enabled = true, chainId = 11155111 }),
                (address, token) => { bindings++; return Task.FromResult(new WalletChallenge()); },
                (challenge, signature, token) => Task.FromResult(new WalletStatus()));
            Task pending = service.Ensure("old-account", () => Task.FromResult("jwt"));
            Task clearing = service.Clear();
            connection.SetResult(Address);
            await Task.WhenAll(pending, clearing);
            Assert.AreEqual(0, bindings);
            Assert.GreaterOrEqual(disconnects, 1);
            Assert.IsEmpty(service.Address);
            Assert.IsEmpty(service.Status);
        }

        [Test]
        public async Task ProviderFailureBecomesRetryableWalletStatus()
        {
            var service = new WalletBinding(_ => throw new Exception("sensitive provider message"),
                _ => Task.FromResult("signature"), () => Task.CompletedTask,
                _ => Task.FromResult(new WalletStatus { enabled = true, chainId = 11155111 }),
                (address, token) => Task.FromResult(new WalletChallenge()),
                (challenge, signature, token) => Task.FromResult(new WalletStatus()));
            await service.Ensure("collector", () => Task.FromResult("jwt"));
            Assert.AreEqual("delayed", service.Status);
            Assert.IsEmpty(service.Address);
        }

        [Test]
        public async Task BoundWalletMismatchNeverOverwritesTheExistingRecipient()
        {
            int challenges = 0;
            var service = new WalletBinding(_ => Task.FromResult(Address), _ => Task.FromResult("signature"),
                () => Task.CompletedTask,
                _ => Task.FromResult(new WalletStatus { enabled = true, chainId = 11155111, address = "0x2222222222222222222222222222222222222222" }),
                (address, token) => { challenges++; return Task.FromResult(new WalletChallenge()); },
                (challenge, signature, token) => Task.FromResult(new WalletStatus()));
            await service.Ensure("collector", () => Task.FromResult("jwt"));
            Assert.AreEqual("delayed", service.Status);
            Assert.AreEqual(0, challenges);
        }

        [Test]
        public async Task DisabledServerDoesNotProvisionAWallet()
        {
            int connections = 0;
            var service = new WalletBinding(_ => { connections++; return Task.FromResult(Address); },
                _ => Task.FromResult("signature"), () => Task.CompletedTask,
                _ => Task.FromResult(new WalletStatus { enabled = false }),
                (address, token) => Task.FromResult(new WalletChallenge()),
                (challenge, signature, token) => Task.FromResult(new WalletStatus()));
            await service.Ensure("collector", () => Task.FromResult("jwt"));
            Assert.AreEqual(0, connections);
            Assert.AreEqual("disabled", service.Status);
        }
    }
}
