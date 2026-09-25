using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Tagtag.Services
{
    public sealed class LocalCollection
    {
        private readonly string directory;
        public LocalCollection(string directory) { this.directory = directory; }
        public List<CollectedSticker> Read(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return new List<CollectedSticker>();
            string path = PathFor(uid);
            foreach (string candidate in new[] { path, path + ".backup", path + ".tmp" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    CollectionList saved = JsonUtility.FromJson<CollectionList>(File.ReadAllText(candidate));
                    if (saved?.items != null) return CollectionBook.Normalize(saved.items);
                }
                catch { /* Try the last complete copy. */ }
            }
            return new List<CollectedSticker>();
        }
        public void Save(string uid, IEnumerable<CollectedSticker> items)
        {
            if (string.IsNullOrEmpty(uid)) return;
            Directory.CreateDirectory(directory);
            string path = PathFor(uid);
            string temporary = path + ".tmp";
            string backup = path + ".backup";
            File.WriteAllText(temporary, JsonUtility.ToJson(new CollectionList { items = CollectionBook.Normalize(items).ToArray() }));
            if (!File.Exists(path))
            {
                File.Move(temporary, path);
                return;
            }
            try { File.Replace(temporary, path, backup); }
            catch (PlatformNotSupportedException) { ReplaceWithBackup(temporary, path, backup); }
        }
        public void Remove(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            string path = PathFor(uid);
            foreach (string candidate in new[] { path, path + ".backup", path + ".tmp" })
                if (File.Exists(candidate)) File.Delete(candidate);
        }

        private static void ReplaceWithBackup(string temporary, string path, string backup)
        {
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
            try { File.Move(temporary, path); }
            catch
            {
                if (!File.Exists(path)) File.Move(backup, path);
                throw;
            }
        }
        private string PathFor(string uid)
        {
            using (var hash = SHA256.Create())
            {
                string name = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(uid))).Replace("-", "");
                return Path.Combine(directory, name + ".json");
            }
        }
    }

    public sealed class DeviceLocation
    {
        public async System.Threading.Tasks.Task<LocationFix> Current(System.Threading.CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Input.location.status == LocationServiceStatus.Stopped) Input.location.Start(10, 2);
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                cancellation.ThrowIfCancellationRequested();
                if (Input.location.status == LocationServiceStatus.Failed)
                    throw new ApiFailure(Input.location.isEnabledByUser ? "Location is unavailable. Try again outdoors." : "Allow Location in Settings to find and place stickers.");
                if (Input.location.status == LocationServiceStatus.Running)
                {
                    var value = Input.location.lastData;
                    var fix = new LocationFix { latitude = value.latitude, longitude = value.longitude,
                        accuracyMeters = value.horizontalAccuracy, measuredUnixSeconds = (long)value.timestamp };
                    if (CollectionBook.FreshLocation(fix, DateTimeOffset.UtcNow.ToUnixTimeSeconds())) return fix;
                }
                await System.Threading.Tasks.Task.Delay(250, cancellation);
            }
            throw new ApiFailure(Input.location.isEnabledByUser ? "We need a more accurate location. Move outdoors and try again." : "Allow Location in Settings to find and place stickers.");
        }
    }
}
