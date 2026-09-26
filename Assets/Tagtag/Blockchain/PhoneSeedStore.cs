using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Tagtag.Blockchain
{
    public interface IPhoneSeedStore
    {
        string Load(string userId);
        void Save(string userId, string phrase);
    }

    public sealed class PhoneSeedStore : IPhoneSeedStore
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern IntPtr TagtagWalletSeedLoad(string userId, out int status);
        [DllImport("__Internal")] private static extern bool TagtagWalletSeedSave(string userId, string phrase);
        [DllImport("__Internal")] private static extern void TagtagWalletSeedFree(IntPtr pointer);
#endif

        public string Load(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("A user ID is required.");
#if UNITY_IOS && !UNITY_EDITOR
            IntPtr pointer = TagtagWalletSeedLoad(userId, out int status);
            if (status == 1) return null;
            if (status != 0 || pointer == IntPtr.Zero)
                throw new InvalidOperationException("Unable to read the wallet from iOS Keychain.");
            try { return Marshal.PtrToStringAnsi(pointer); }
            finally { TagtagWalletSeedFree(pointer); }
#else
            throw new PlatformNotSupportedException("Phone wallet storage requires iOS Keychain.");
#endif
        }

        public void Save(string userId, string phrase)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(phrase))
                throw new ArgumentException("A user ID and recovery phrase are required.");
#if UNITY_IOS && !UNITY_EDITOR
            if (!TagtagWalletSeedSave(userId, phrase))
                throw new InvalidOperationException("Unable to save the wallet in iOS Keychain.");
#else
            throw new PlatformNotSupportedException("Phone wallet storage requires iOS Keychain.");
#endif
        }
    }
}
