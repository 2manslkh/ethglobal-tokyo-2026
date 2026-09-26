namespace Tagtag
{
    public interface IPhoneWalletController
    {
        string RevealWalletPhrase();
        void RestoreWalletPhrase(string phrase);
    }
}
