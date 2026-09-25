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
            try
            {
                string path = PathFor(uid);
                return File.Exists(path) ? CollectionBook.Normalize(JsonUtility.FromJson<CollectionList>(File.ReadAllText(path)).items) : new List<CollectedSticker>();
            }
            catch { return new List<CollectedSticker>(); }
        }
        public void Save(string uid, IEnumerable<CollectedSticker> items)
        {
            if (string.IsNullOrEmpty(uid)) return;
            Directory.CreateDirectory(directory);
            string path = PathFor(uid);
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(new CollectionList { items = CollectionBook.Normalize(items).ToArray() }));
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);
        }
        public void Remove(string uid)
        {
            if (!string.IsNullOrEmpty(uid) && File.Exists(PathFor(uid))) File.Delete(PathFor(uid));
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
        public async System.Threading.Tasks.Task<LocationFix> Current()
        {
            if (Input.location.status == LocationServiceStatus.Stopped) Input.location.Start(10, 2);
            if (!Input.location.isEnabledByUser) throw new ApiFailure("Allow Location in Settings to find and place stickers.");
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                if (Input.location.status == LocationServiceStatus.Failed) throw new ApiFailure("Location is unavailable. Try again outdoors.");
                if (Input.location.status == LocationServiceStatus.Running)
                {
                    var value = Input.location.lastData;
                    var fix = new LocationFix { latitude = value.latitude, longitude = value.longitude,
                        accuracyMeters = value.horizontalAccuracy, measuredUnixSeconds = (long)value.timestamp };
                    if (CollectionBook.FreshLocation(fix, DateTimeOffset.UtcNow.ToUnixTimeSeconds())) return fix;
                }
                await System.Threading.Tasks.Task.Delay(250);
            }
            throw new ApiFailure("We need a more accurate location. Move outdoors and try again.");
        }
    }
}
