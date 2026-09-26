using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Tagtag.Services
{
    [Serializable]
    public sealed class EditablePublicationDraft
    {
        public string presetId, place, teaser, note;
    }

    public sealed class EditablePublication
    {
        private readonly string directory;
        public EditablePublication(string directory) { this.directory = directory; }

        public EditablePublicationDraft Read(string uid)
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
                    var draft = JsonUtility.FromJson<EditablePublicationDraft>(content);
                    if (draft != null) return draft;
                }
                catch { /* Try the previous complete copy. */ }
            }
            return null;
        }

        public void Save(string uid, EditablePublicationDraft draft)
        {
            if (string.IsNullOrEmpty(uid)) throw new InvalidOperationException("An account is required to save a draft.");
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
                catch { /* The clear marker takes precedence over older copies. */ }
            }
        }

        private string PathFor(string uid)
        {
            using (var hash = SHA256.Create())
                return Path.Combine(directory, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(uid))).Replace("-", "") + ".json");
        }
    }
}
