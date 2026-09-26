using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Tagtag
{
    // Shared by UI and AR; signed URLs never form a persistent cache identity.
    public static class StickerArtwork
    {
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Task<Texture2D>> requests = new Dictionary<string, Task<Texture2D>>();
        private static readonly Dictionary<string, string> failures = new Dictionary<string, string>();
        private static readonly HashSet<string> revoked = new HashSet<string>();
        private static string account = "guest";
        private static int generation;
        public static event Action Changed;
        public static Vector3 Scale(float width, int imageWidth, int imageHeight) =>
            new Vector3(width, width * (imageWidth > 0 && imageHeight > 0 ? (float)imageHeight / imageWidth : 1f), width);
        public static void SetAccount(string uid)
        {
            string next = string.IsNullOrEmpty(uid) ? "guest" : uid;
            if (account == next) return;
            generation++; account = next;
            // AR may still reference a texture until its placement is torn down.
            foreach (var texture in textures.Values) if (texture != null) Release(texture);
            textures.Clear(); requests.Clear(); failures.Clear(); revoked.Clear();
        }
        public static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
        public static bool IsLoading(string id, bool thumbnail = true) => requests.ContainsKey(Key(id, thumbnail));
        public static bool HasFailed(string id, bool thumbnail = true) => failures.ContainsKey(Key(id, thumbnail));
        public static void Retry() { failures.Clear(); Changed?.Invoke(); }
        public static Texture2D Get(string presetId, string designId, string artworkUrl, string thumbnailUrl, bool thumbnail = true)
        {
            if (string.IsNullOrEmpty(designId)) return Resources.Load<Texture2D>("Tagtag/Presets/" + presetId);
            string key = Key(designId, thumbnail);
            if (revoked.Contains(key)) return null;
            if (textures.TryGetValue(key, out var texture)) return texture;
            string url = thumbnail && !string.IsNullOrEmpty(thumbnailUrl) ? thumbnailUrl : artworkUrl;
            if (!requests.ContainsKey(key) && (!failures.TryGetValue(key, out var failed) || failed != url))
                _ = Load(designId, artworkUrl, thumbnailUrl, thumbnail);
            return thumbnail && textures.TryGetValue(Key(designId, false), out var full) ? full : null;
        }
        public static Task<Texture2D> Load(string designId, string artworkUrl, string thumbnailUrl = null, bool thumbnail = false)
        {
            string key = Key(designId, thumbnail);
            if (revoked.Contains(key)) return Task.FromResult<Texture2D>(null);
            if (textures.TryGetValue(key, out var texture)) return Task.FromResult(texture);
            if (requests.TryGetValue(key, out var existing)) return existing;
            var completion = new TaskCompletionSource<Texture2D>();
            requests[key] = completion.Task;
            _ = Fetch(key, thumbnail && !string.IsNullOrEmpty(thumbnailUrl) ? thumbnailUrl : artworkUrl, generation, completion);
            return completion.Task;
        }
        private static async Task Fetch(string key, string url, int version, TaskCompletionSource<Texture2D> completion)
        {
            await Task.Yield();
            Texture2D texture = null;
            try
            {
                string file = CachePath(key);
                string readFile = file;
                if (!File.Exists(file) && key.EndsWith(":thumb"))
                {
                    string fullPath = CachePath(key.Substring(0, key.Length - 6) + ":full");
                    if (File.Exists(fullPath)) readFile = fullPath;
                }
                byte[] bytes = null;
                if (File.Exists(readFile))
                {
                    try { bytes = File.ReadAllBytes(readFile); texture = Decode(bytes); }
                    catch { bytes = null; }
                }
                if (texture == null)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
                        throw new InvalidOperationException("Artwork is not available offline.");
                    using (var request = UnityWebRequest.Get(url))
                    {
                        request.timeout = 30;
                        var operation = request.SendWebRequest();
                        while (!operation.isDone)
                        {
                            if (request.downloadedBytes > 5 * 1024 * 1024) { request.Abort(); throw new InvalidOperationException("Artwork is too large."); }
                            await Task.Yield();
                        }
                        if (request.result != UnityWebRequest.Result.Success) throw new InvalidOperationException("Artwork could not load. Refresh and try again.");
                        bytes = request.downloadHandler.data;
                        texture = Decode(bytes);
                    }
                    if (version != generation || revoked.Contains(key)) throw new OperationCanceledException();
                    try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)); File.WriteAllBytes(file, bytes); }
                    catch { /* A downloaded image remains usable if disk is full. */ }
                }
                if (version != generation || revoked.Contains(key)) throw new OperationCanceledException();
                textures[key] = texture;
                failures.Remove(key);
                completion.TrySetResult(texture);
            }
            catch
            {
                if (texture != null) Release(texture);
                if (version == generation) failures[key] = url;
                // UI Get() intentionally observes errors through an empty image and retry action.
                completion.TrySetResult(null);
            }
            finally
            {
                if (version == generation) { requests.Remove(key); Changed?.Invoke(); }
            }
        }
        public static Texture2D Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 24 || bytes.Length > 5 * 1024 * 1024 ||
                bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                throw new InvalidOperationException("Invalid sticker image.");
            int width = ReadInt(bytes, 16), height = ReadInt(bytes, 20);
            if (width < 1 || height < 1 || width > 1024 || height > 1024) throw new InvalidOperationException("Invalid sticker dimensions.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes)) { Release(texture); throw new InvalidOperationException("Invalid sticker image."); }
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
        private static int ReadInt(byte[] bytes, int index) =>
            (bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3];
        private static string Key(string id, bool thumbnail) => account + ":" + id + (thumbnail ? ":thumb" : ":full");
        private static string CachePath(string key)
        {
            using (var hash = SHA256.Create())
                return System.IO.Path.Combine(Application.persistentDataPath, "sticker-artwork", BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "") + ".png");
        }
        // Call only after a fresh server response permits access to this immutable design.
        public static void Authorize(string id)
        {
            revoked.Remove(Key(id, true)); revoked.Remove(Key(id, false));
        }
        public static void Store(string id, byte[] bytes)
        {
            string key = Key(id, false);
            var texture = Decode(bytes);
            if (textures.TryGetValue(key, out var previous)) Release(previous);
            textures[key] = texture;
            try { string path = CachePath(key); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); File.WriteAllBytes(path, bytes); } catch { }
            Changed?.Invoke();
        }
        public static void Forget(string id)
        {
            foreach (bool thumbnail in new[] { true, false })
            {
                string key = Key(id, thumbnail);
                revoked.Add(key);
                if (textures.TryGetValue(key, out var value)) Release(value);
                textures.Remove(key); failures.Remove(key);
                try { File.Delete(CachePath(key)); } catch { }
            }
        }
    }
}
