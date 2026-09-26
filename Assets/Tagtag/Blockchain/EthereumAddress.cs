using Nethereum.Util;

namespace Tagtag.Blockchain
{
    /// <summary>Validates Ethereum addresses without changing the user's input.</summary>
    public static class EthereumAddress
    {
        private static readonly AddressUtil AddressUtility = new AddressUtil();

        public static bool IsValid(string address)
        {
            if (address == null || address.Length != 42 || address[0] != '0' || address[1] != 'x')
            {
                return false;
            }

            var hasLowercaseLetter = false;
            var hasUppercaseLetter = false;
            var hasNonzeroDigit = false;
            for (var i = 2; i < address.Length; i++)
            {
                var character = address[i];
                if (character >= '1' && character <= '9')
                {
                    hasNonzeroDigit = true;
                }
                else if (character >= 'a' && character <= 'f')
                {
                    hasLowercaseLetter = true;
                    hasNonzeroDigit = true;
                }
                else if (character >= 'A' && character <= 'F')
                {
                    hasUppercaseLetter = true;
                    hasNonzeroDigit = true;
                }
                else if (character != '0')
                {
                    return false;
                }
            }

            if (!hasNonzeroDigit) return false;
            if (!hasLowercaseLetter || !hasUppercaseLetter) return true;
            return AddressUtility.IsChecksumAddress(address);
        }
    }
}
