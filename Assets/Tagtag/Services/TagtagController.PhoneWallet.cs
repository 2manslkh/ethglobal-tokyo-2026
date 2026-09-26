using System;
using System.Threading.Tasks;

namespace Tagtag.Services
{
    public sealed partial class TagtagController
    {
        public string RevealWalletPhrase()
        {
            if (disposed || State.user == null || wallet == null || wallet.Status != "ready" || revealWalletPhrase == null)
                throw new InvalidOperationException("Your wallet is not ready.");
            return revealWalletPhrase(State.user.uid);
        }

        public async void RestoreWalletPhrase(string phrase)
        {
            if (disposed || State.user == null || wallet == null || string.IsNullOrWhiteSpace(phrase)) return;
            string uid = State.user.uid;
            int generation = accountGeneration;
            try
            {
                await wallet.Restore(uid, phrase, async () =>
                {
                    if (disposed || generation != accountGeneration || session.Current?.uid != uid)
                        throw new OperationCanceledException();
                    string token = await session.Token();
                    if (disposed || generation != accountGeneration || session.Current?.uid != uid)
                        throw new OperationCanceledException();
                    return token;
                });
                if (disposed || generation != accountGeneration || session.Current?.uid != uid) return;
                State.status = wallet.Status == "ready" ? "Wallet restored on this phone." : "Wallet restoration is still pending.";
            }
            catch (Tagtag.Blockchain.WalletRecoveryRequiredException)
            {
                if (generation == accountGeneration) State.status = "These words do not match the wallet already linked to this account.";
            }
            catch (ArgumentException)
            {
                if (generation == accountGeneration) State.status = "Check the 12 recovery words and try again.";
            }
            catch (Exception)
            {
                if (generation == accountGeneration) State.status = "Wallet restoration could not finish. Try again when the phone is unlocked and online.";
            }
            if (generation == accountGeneration) Notify();
        }
    }
}
