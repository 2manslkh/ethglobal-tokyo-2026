using System.Threading.Tasks;

namespace Tagtag.Blockchain
{
    public interface IEmbeddedWallet
    {
        Task<string> Connect(string firebaseIdToken);
        Task<string> SignMessage(string message);
        Task Disconnect();
    }
}
