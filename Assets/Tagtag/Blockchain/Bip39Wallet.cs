using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Math;

namespace Tagtag.Blockchain
{
    // BIP-39 English, BIP-32, and Ethereum's first BIP-44 account: m/44'/60'/0'/0/0.
    // The caller supplies the checked-in BIP-39 word list so this code is also testable off-device.
    public sealed class Bip39Wallet
    {
        private readonly string[] words;
        private readonly Dictionary<string, int> indices;
        private static readonly uint[] Path = { 0x8000002c, 0x8000003c, 0x80000000, 0, 0 };
        private static readonly BigInteger CurveOrder = SecNamedCurves.GetByName("secp256k1").N;

        public Bip39Wallet(string wordList)
        {
            words = (wordList ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 2048 || words.Distinct(StringComparer.Ordinal).Count() != 2048)
                throw new ArgumentException("The BIP-39 English word list must have 2048 unique words.");
            indices = words.Select((word, index) => new { word, index })
                .ToDictionary(item => item.word, item => item.index, StringComparer.Ordinal);
        }

        public string Create()
        {
            var entropy = new byte[16];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(entropy);
            try { return Encode(entropy); }
            finally { Array.Clear(entropy, 0, entropy.Length); }
        }

        public string Encode(byte[] entropy)
        {
            if (entropy == null || entropy.Length != 16) throw new ArgumentException("A 128-bit entropy value is required.");
            var checksum = Sha256(entropy);
            var result = new string[12];
            for (int word = 0; word < result.Length; word++)
            {
                int index = 0;
                for (int bit = 0; bit < 11; bit++)
                {
                    int position = word * 11 + bit;
                    int value = position < 128 ? (entropy[position / 8] >> (7 - position % 8)) & 1
                        : (checksum[0] >> (7 - (position - 128))) & 1;
                    index = (index << 1) | value;
                }
                result[word] = words[index];
            }
            Array.Clear(checksum, 0, checksum.Length);
            return string.Join(" ", result);
        }

        public string Normalize(string phrase)
        {
            var parts = (phrase ?? "").Trim().ToLowerInvariant()
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 12) throw new ArgumentException("Enter all 12 recovery words in order.");
            var entropy = new byte[16];
            int checksum = 0;
            for (int word = 0; word < parts.Length; word++)
            {
                if (!indices.TryGetValue(parts[word], out int index))
                    throw new ArgumentException("The recovery phrase contains an unknown word.");
                for (int bit = 0; bit < 11; bit++)
                {
                    int position = word * 11 + bit;
                    int value = (index >> (10 - bit)) & 1;
                    if (position < 128) entropy[position / 8] |= (byte)(value << (7 - position % 8));
                    else checksum = (checksum << 1) | value;
                }
            }
            var digest = Sha256(entropy);
            bool valid = checksum == (digest[0] >> 4);
            Array.Clear(digest, 0, digest.Length);
            Array.Clear(entropy, 0, entropy.Length);
            if (!valid) throw new ArgumentException("The recovery phrase checksum is invalid.");
            return string.Join(" ", parts);
        }

        public byte[] DerivePrivateKey(string phrase)
        {
            var normalized = Normalize(phrase).Normalize(NormalizationForm.FormKD);
            var password = Encoding.UTF8.GetBytes(normalized);
            var salt = Encoding.UTF8.GetBytes("mnemonic");
            byte[] seed;
            using (var derivation = new Rfc2898DeriveBytes(password, salt, 2048, HashAlgorithmName.SHA512))
                seed = derivation.GetBytes(64);
            Array.Clear(password, 0, password.Length);
            byte[] master;
            using (var hmac = new HMACSHA512(Encoding.ASCII.GetBytes("Bitcoin seed"))) master = hmac.ComputeHash(seed);
            Array.Clear(seed, 0, seed.Length);
            var key = new byte[32];
            var chainCode = new byte[32];
            Buffer.BlockCopy(master, 0, key, 0, 32);
            Buffer.BlockCopy(master, 32, chainCode, 0, 32);
            Array.Clear(master, 0, master.Length);
            foreach (uint index in Path)
            {
                byte[] input = new byte[37];
                if ((index & 0x80000000) != 0) Buffer.BlockCopy(key, 0, input, 1, 32);
                else
                {
                    var point = SecNamedCurves.GetByName("secp256k1").G
                        .Multiply(new BigInteger(1, key)).Normalize().GetEncoded(true);
                    Buffer.BlockCopy(point, 0, input, 0, 33);
                    Array.Clear(point, 0, point.Length);
                }
                input[33] = (byte)(index >> 24); input[34] = (byte)(index >> 16);
                input[35] = (byte)(index >> 8); input[36] = (byte)index;
                byte[] child;
                using (var hmac = new HMACSHA512(chainCode)) child = hmac.ComputeHash(input);
                var increment = new BigInteger(1, child.Take(32).ToArray());
                if (increment.CompareTo(CurveOrder) >= 0) throw new CryptographicException("Invalid wallet derivation.");
                var next = increment.Add(new BigInteger(1, key)).Mod(CurveOrder);
                if (next.SignValue == 0) throw new CryptographicException("Invalid wallet derivation.");
                Array.Clear(key, 0, key.Length);
                var encoded = next.ToByteArrayUnsigned();
                Buffer.BlockCopy(encoded, 0, key, 32 - encoded.Length, encoded.Length);
                Buffer.BlockCopy(child, 32, chainCode, 0, 32);
                Array.Clear(input, 0, input.Length);
                Array.Clear(child, 0, child.Length);
                Array.Clear(encoded, 0, encoded.Length);
            }
            Array.Clear(chainCode, 0, chainCode.Length);
            return key;
        }

        private static byte[] Sha256(byte[] value)
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(value);
        }
    }
}
