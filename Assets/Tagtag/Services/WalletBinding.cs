using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tagtag.Services
{
    // Serializes SDK access and invalidates in-flight results when identity changes.
    // Wallet errors are deliberately separate from gameplay errors and busy state.
    public sealed class WalletBinding
    {
        private readonly Func<string, string, Task<string>> connect;
        private readonly Func<string, Task<string>> sign;
        private readonly Func<string, string, string, Task<string>> restore;
        private readonly Func<Task> disconnect;
        private readonly Func<string, Task<WalletStatus>> read;
        private readonly Func<string, string, Task<WalletChallenge>> challenge;
        private readonly Func<string, string, string, Task<WalletStatus>> bind;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private int generation;
        private string userId;
        private Task pending;
        public string Address { get; private set; } = "";
        public string Status { get; private set; } = "";
        public event Action Changed;

        public WalletBinding(Func<string, string, Task<string>> connect, Func<string, Task<string>> sign,
            Func<Task> disconnect, Func<string, Task<WalletStatus>> read,
            Func<string, string, Task<WalletChallenge>> challenge,
            Func<string, string, string, Task<WalletStatus>> bind,
            Func<string, string, string, Task<string>> restore = null)
        {
            this.connect = connect; this.sign = sign; this.disconnect = disconnect;
            this.read = read; this.challenge = challenge; this.bind = bind; this.restore = restore;
        }

        public Task Ensure(string uid, Func<Task<string>> token)
        {
            if (string.IsNullOrEmpty(uid)) return Clear();
            if (userId == uid && pending != null && !pending.IsCompleted) return pending;
            if (userId == uid && (Status == "ready" || Status == "needsRecovery")) return Task.CompletedTask;
            bool changedUser = userId != uid;
            if (changedUser) generation++;
            userId = uid;
            int current = generation;
            Address = ""; Status = "pending"; Changed?.Invoke();
            pending = Provision(current, changedUser, token);
            return pending;
        }

        public Task Clear()
        {
            generation++; userId = null; pending = null;
            Address = ""; Status = ""; Changed?.Invoke();
            return ClearSdk();
        }

        public async Task Restore(string uid, string phrase, Func<Task<string>> token)
        {
            if (restore == null || string.IsNullOrEmpty(uid) || uid != userId)
                throw new InvalidOperationException("The wallet cannot be restored for this account.");
            int current = generation;
            await gate.WaitAsync();
            try
            {
                if (current != generation || uid != userId) return;
                WalletStatus existing = await read(await token());
                if (current != generation || uid != userId) return;
                if (existing == null || !existing.enabled || existing.chainId != 11155111)
                    throw new InvalidOperationException("Wallet service is unavailable.");
                await restore(uid, phrase, existing.address);
                if (current != generation || uid != userId) return;
                Status = "";
            }
            finally { gate.Release(); }
            await Ensure(uid, token);
        }

        private async Task ClearSdk()
        {
            await gate.WaitAsync();
            try { await disconnect(); }
            catch { /* Identity remains cleared even if provider cleanup fails. */ }
            finally { gate.Release(); }
        }

        private async Task Provision(int current, bool changedUser, Func<Task<string>> token)
        {
            await gate.WaitAsync();
            try
            {
                if (current != generation) return;
                if (changedUser) await disconnect();
                if (current != generation) return;
                string jwt = await token();
                if (current != generation) return;
                WalletStatus existing = await read(jwt);
                if (current != generation) return;
                if (existing == null) throw new InvalidOperationException();
                if (!existing.enabled) { Status = "disabled"; return; }
                if (existing.chainId != 11155111) throw new InvalidOperationException();
                string address = await connect(userId, existing.address);
                if (current != generation) return;
                if (string.IsNullOrEmpty(address)) throw new InvalidOperationException();
                if (!string.IsNullOrEmpty(existing.address))
                {
                    if (!string.Equals(existing.address, address, StringComparison.OrdinalIgnoreCase))
                        throw new Tagtag.Blockchain.WalletRecoveryRequiredException();
                }
                else
                {
                    jwt = await token();
                    if (current != generation) return;
                    WalletChallenge proof = await challenge(address, jwt);
                    if (current != generation) return;
                    if (string.IsNullOrEmpty(proof?.challengeId) || string.IsNullOrEmpty(proof.message))
                        throw new InvalidOperationException();
                    string signature = await sign(proof.message);
                    if (current != generation) return;
                    jwt = await token();
                    if (current != generation) return;
                    existing = await bind(proof.challengeId, signature, jwt);
                    if (current != generation) return;
                    if (existing == null || !existing.enabled || existing.chainId != 11155111 ||
                        !string.Equals(existing.address, address, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException();
                }
                Address = existing.address; Status = "ready";
            }
            catch (Tagtag.Blockchain.WalletRecoveryRequiredException)
            {
                if (current == generation) { Address = ""; Status = "needsRecovery"; }
                try { await disconnect(); } catch { }
            }
            catch
            {
                if (current == generation) { Address = ""; Status = "delayed"; }
                try { await disconnect(); } catch { }
            }
            finally
            {
                gate.Release();
                if (current == generation) Changed?.Invoke();
            }
        }
    }

    [Serializable] public sealed class WalletStatus { public bool enabled; public string address; public int chainId; }
    [Serializable] public sealed class WalletChallenge { public string challengeId, message; public long expiresAt; }
    [Serializable] public sealed class WalletChallengeRequest { public string address; }
    [Serializable] public sealed class WalletBindRequest { public string challengeId, signature; }
}
