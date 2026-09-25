using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Tagtag.Services
{
    public sealed class PendingPublication
    {
        private readonly string directory;
        public PendingPublication(string directory) { this.directory = directory; }
        public PlacementDraft Read(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            string path = PathFor(uid);
            foreach (string candidate in new[] { path, path + ".backup" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    string content = File.ReadAllText(candidate);
                    if (content == "tagtag:cleared") return null;
                    var draft = JsonUtility.FromJson<PlacementDraft>(content);
                    if (!string.IsNullOrEmpty(draft?.operationId) && !string.IsNullOrEmpty(draft.snapshot?.worldMapBase64)) return draft;
                }
                catch { /* Try the previous atomic snapshot. */ }
            }
            return null;
        }
        public void Save(string uid, PlacementDraft draft)
        {
            if (string.IsNullOrEmpty(uid)) throw new InvalidOperationException("An account is required to preserve a publication.");
            Directory.CreateDirectory(directory);
            string path = PathFor(uid);
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(draft));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".backup");
            else File.Move(path + ".tmp", path);
        }
        public void Remove(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            string path = PathFor(uid);
            if (!File.Exists(path) && !File.Exists(path + ".backup")) return;
            File.WriteAllText(path + ".tmp", "tagtag:cleared");
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
            else File.Move(path + ".tmp", path);
            foreach (string suffix in new[] { ".backup", ".tmp" })
            {
                try { if (File.Exists(path + suffix)) File.Delete(path + suffix); }
                catch { /* The committed clear marker takes precedence over obsolete backups. */ }
            }
        }
        private string PathFor(string uid)
        {
            using (var hash = SHA256.Create())
                return Path.Combine(directory, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(uid))).Replace("-", "") + ".json");
        }
    }
}
