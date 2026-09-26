using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Tagtag.Services
{
    [Serializable] public sealed class DesignList { public StickerDesign[] items; }
    [Serializable] public sealed class DesignResult { public StickerDesign design; }
    [Serializable] public sealed class DesignUploadDraft
    {
        public string operationId, name, kind, path;
        public int width, height;
    }
    [Serializable] public sealed class DesignPrepareRequest
    {
        public string operationId, name, kind;
        public int imageBytes, width, height;
    }
    public sealed class LocalDesigns
    {
        private readonly string root;
        public LocalDesigns(string root) { this.root = root; }
        private string DirectoryFor(string uid)
        {
            using (var hash = SHA256.Create())
                return System.IO.Path.Combine(root, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(uid ?? "guest"))).Replace("-", ""));
        }
        private string PathFor(string uid, string file) => System.IO.Path.Combine(DirectoryFor(uid), file);
        public StickerDesign[] Read(string uid) => ReadJson<DesignList>(PathFor(uid, "library.json"))?.items ?? Array.Empty<StickerDesign>();
        public void Save(string uid, StickerDesign[] items) => WriteJson(PathFor(uid, "library.json"), new DesignList { items = items });
        public DesignUploadDraft ReadDraft(string uid)
        {
            var draft = ReadJson<DesignUploadDraft>(PathFor(uid, "draft.json"));
            if (draft == null && Directory.Exists(DirectoryFor(uid)))
            {
                foreach (var queued in Directory.GetFiles(DirectoryFor(uid), "queued-*.json").OrderBy(path => path))
                {
                    draft = ReadJson<DesignUploadDraft>(queued);
                    if (draft == null || !File.Exists(draft.path)) continue;
                    WriteJson(PathFor(uid, "draft.json"), draft);
                    TryDelete(queued); TryDelete(queued + ".backup");
                    break;
                }
            }
            if (draft == null || string.IsNullOrEmpty(draft.operationId) || !File.Exists(draft.path)) return null;
            return draft;
        }
        public void SaveDraft(string uid, CreatedStickerImage image)
        {
            byte[] bytes = File.ReadAllBytes(image.path);
            var texture = StickerArtwork.Decode(bytes);
            int width = texture.width, height = texture.height;
            StickerArtwork.Release(texture);
            if (image.kind != "ai" && image.kind != "image" && image.kind != "polaroid") throw new InvalidOperationException("Unknown sticker kind.");
            Directory.CreateDirectory(DirectoryFor(uid));
            string operation = Guid.NewGuid().ToString("N");
            string target = PathFor(uid, operation + ".png");
            File.WriteAllBytes(target, bytes);
            var previous = ReadDraft(uid);
            try
            {
                WriteJson(PathFor(uid, "draft.json"), new DesignUploadDraft { operationId = operation, path = target,
                    name = DesignName(image.name),
                    kind = image.kind, width = width, height = height });
            }
            catch { File.Delete(target); throw; }
            if (previous != null && previous.path != target) TryDelete(previous.path);
        }
        private static string DesignName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "My sticker";
            int length = Math.Min(80, name.Length);
            if (length < name.Length && char.IsHighSurrogate(name[length - 1])) length--;
            return name.Substring(0, length);
        }
        public void ClaimGuest(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            bool occupied = ReadDraft(uid) != null;
            var draft = ReadDraft(null);
            if (draft == null) return;
            Directory.CreateDirectory(DirectoryFor(uid));
            string target = PathFor(uid, draft.operationId + ".png");
            File.Copy(draft.path, target, true);
            draft.path = target;
            WriteJson(PathFor(uid, occupied ? "queued-" + draft.operationId + ".json" : "draft.json"), draft);
            ClearDraft(null);
        }
        public DesignUploadDraft RestartDraft(string uid)
        {
            var draft = ReadDraft(uid) ?? throw new InvalidOperationException("No saved sticker to retry.");
            draft.operationId = Guid.NewGuid().ToString("N");
            WriteJson(PathFor(uid, "draft.json"), draft);
            return draft;
        }
        public void ClearDraft(string uid)
        {
            var draft = ReadDraft(uid);
            TryDelete(PathFor(uid, "draft.json"));
            TryDelete(PathFor(uid, "draft.json.backup"));
            if (draft != null) TryDelete(draft.path);
        }
        public void Remove(string uid)
        {
            string directory = DirectoryFor(uid);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        private static void TryDelete(string path) { try { File.Delete(path); } catch { } }
        private static T ReadJson<T>(string path) where T : class
        {
            foreach (var candidate in new[] { path, path + ".backup" })
                try { if (File.Exists(candidate)) return JsonUtility.FromJson<T>(File.ReadAllText(candidate)); } catch { }
            return null;
        }
        private static void WriteJson(string path, object value)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(value));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".backup");
            else File.Move(path + ".tmp", path);
        }
    }
}
