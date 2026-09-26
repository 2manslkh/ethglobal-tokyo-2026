using System;

namespace Tagtag.Blockchain
{
    /// <summary>A safe, stable code for wallet transfer UI and recovery handling.</summary>
    public sealed class WalletTransferException : Exception
    {
        public string Code { get; }

        internal WalletTransferException(string code, string message) : base(message)
        {
            Code = code;
        }
    }
}
