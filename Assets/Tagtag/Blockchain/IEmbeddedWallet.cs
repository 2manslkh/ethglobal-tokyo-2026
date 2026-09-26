using System.Threading.Tasks;

namespace Tagtag.Blockchain
{
    public interface IEmbeddedWallet
    {
        Task<string> Connect(string firebaseIdToken);
        Task<string> SignMessage(string message);
        Task<string> GetNftOwner(string contractAddress, string tokenId);
        Task<string> TransferNft(string contractAddress, string tokenId, string recipient);
        Task<string> GetTransferStatus(string transactionHash);
        Task Disconnect();
    }
}
