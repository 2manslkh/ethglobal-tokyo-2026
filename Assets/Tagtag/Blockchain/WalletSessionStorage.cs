using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Tagtag.Blockchain
{
    /// <summary>
    /// Thirdweb v6.1.3 requires a file-backed session store. Keep its brief
    /// authentication write in a disposable cache directory, then remove it
    /// after login while the SDK retains the active session in memory.
    /// </summary>
    internal sealed class WalletSessionStorage
    {
        private static readonly object RootGate = new object();
        private static readonly HashSet<string> CleanedRoots = new HashSet<string>(StringComparer.Ordinal);

        internal string SessionDirectory { get; }
        internal string SessionFile { get; }

        private WalletSessionStorage(string directory, string file)
        {
            SessionDirectory = directory;
            SessionFile = file;
        }

        internal static WalletSessionStorage Prepare(string clientId, string temporaryCachePath, string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(clientId) || clientId.IndexOf('/') >= 0 || clientId.IndexOf('\\') >= 0)
            {
                throw new ArgumentException("Invalid thirdweb client ID.", nameof(clientId));
            }

            var root = Path.Combine(temporaryCachePath, "TagtagThirdwebSessions");
            var persistentRoot = Path.GetFullPath(persistentDataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(root).StartsWith(persistentRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Thirdweb session cache must be outside persistent data storage.");
            }

            lock (RootGate)
            {
                if (!CleanedRoots.Contains(root))
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                    Directory.CreateDirectory(root);
                    CleanedRoots.Add(root);
                }
            }

            // Remove the cache used by the previous adapter version before any
            // new authentication can proceed.
            var legacyFile = Path.Combine(persistentDataPath, "Thirdweb", "InAppWallet", clientId + ".txt");
            if (File.Exists(legacyFile)) File.Delete(legacyFile);

            var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
            var file = Path.Combine(directory, clientId + ".txt");

#if UNITY_IOS && !UNITY_EDITOR
            if (TagtagPrepareProtectedWalletSessionFile(directory, file) == 0)
            {
                throw new IOException("Unable to create protected thirdweb session storage.");
            }
#elif UNITY_EDITOR
            Directory.CreateDirectory(directory);
            using (File.Create(file)) { }
#else
            throw new PlatformNotSupportedException("Protected thirdweb session storage is implemented only for iOS.");
#endif

            return new WalletSessionStorage(directory, file);
        }

        internal async Task RemoveAuthenticatedSessionFile()
        {
            // Thirdweb starts this write from an async void method. Wait until
            // its file handle closes before removing the authenticated token.
            for (var attempt = 0; attempt < 250; attempt++)
            {
                try
                {
                    var written = false;
                    using (var stream = new FileStream(SessionFile, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        written = stream.Length > 0;
                    }

                    if (written)
                    {
                        File.Delete(SessionFile);
                        return;
                    }
                }
                catch (IOException)
                {
                    // The SDK still owns its file handle.
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            throw new IOException("Thirdweb session cache could not be removed after authentication.");
        }

        internal void Cleanup()
        {
            if (Directory.Exists(SessionDirectory)) Directory.Delete(SessionDirectory, true);
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int TagtagPrepareProtectedWalletSessionFile(string directory, string file);
#endif
    }
}
