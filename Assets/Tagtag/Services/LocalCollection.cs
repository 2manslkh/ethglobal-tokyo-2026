using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

    public enum LocationAuthorization { Unknown, Denied, ReducedAccuracy, FullAccuracy }

    public interface ILocationRuntime
    {
        LocationAuthorization Authorization { get; }
        bool ServicesEnabled { get; }
        LocationServiceStatus Status { get; }
        LocationFix LastFix { get; }
        DateTimeOffset UtcNow { get; }
        void Start(float desiredAccuracyMeters, float updateDistanceMeters);
        void Stop();
        Task Delay(CancellationToken cancellation);
    }

    internal sealed class UnityLocationRuntime : ILocationRuntime
    {
        public LocationAuthorization Authorization
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                try
                {
                    int status = TagtagLocationAuthorizationStatus();
                    return status >= 0 && status <= 3 ? (LocationAuthorization)status : LocationAuthorization.Unknown;
                }
                catch (EntryPointNotFoundException) { return LocationAuthorization.Unknown; }
                catch (DllNotFoundException) { return LocationAuthorization.Unknown; }
#else
                return LocationAuthorization.Unknown;
#endif
            }
        }
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int TagtagLocationAuthorizationStatus();
#endif
        public bool ServicesEnabled => Input.location.isEnabledByUser;
        public LocationServiceStatus Status => Input.location.status;
        public LocationFix LastFix
        {
            get
            {
                var value = Input.location.lastData;
                return new LocationFix { latitude = value.latitude, longitude = value.longitude,
                    accuracyMeters = value.horizontalAccuracy, measuredUnixSeconds = (long)value.timestamp };
            }
        }
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public void Start(float desiredAccuracyMeters, float updateDistanceMeters) => Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);
        public void Stop() => Input.location.Stop();
        public Task Delay(CancellationToken cancellation) => Task.Delay(250, cancellation);
    }

    public sealed class DeviceLocation
    {
        private readonly ILocationRuntime runtime;
        private bool stationaryUpdates;
        private bool prewarmed;
        private bool suspended;
        private bool disposed;
        private int activeRequests;
        private int lifecycleVersion;
        public DeviceLocation(ILocationRuntime runtime = null) { this.runtime = runtime ?? new UnityLocationRuntime(); }

        public void CheckPermission()
        {
            if (disposed) throw new OperationCanceledException("Location request ended.");
            LocationAuthorization authorization = runtime.Authorization;
            if (!runtime.ServicesEnabled || authorization == LocationAuthorization.Denied)
            {
                prewarmed = false;
                StopRuntime();
                throw new ApiFailure("Allow Location in Settings to find and place stickers.", locationSettingsRequired: true);
            }
            if (authorization == LocationAuthorization.ReducedAccuracy)
            {
                prewarmed = false;
                StopRuntime();
                throw new ApiFailure("Turn on Precise Location for tagtag in Settings, then try again.", locationSettingsRequired: true);
            }
        }

        public void Prewarm()
        {
            if (disposed) return;
            LocationAuthorization authorization = runtime.Authorization;
            if (suspended || !runtime.ServicesEnabled || authorization == LocationAuthorization.Denied ||
                authorization == LocationAuthorization.ReducedAccuracy)
            {
                if (!suspended) Stop();
                return;
            }
            EnsureStarted();
            prewarmed = true;
        }

        public void Stop()
        {
            prewarmed = false;
            if (activeRequests == 0) StopRuntime();
        }

        private void StopRuntime()
        {
            if (runtime.Status != LocationServiceStatus.Stopped) runtime.Stop();
            stationaryUpdates = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifecycleVersion++;
            prewarmed = false;
            StopRuntime();
        }

        public void Suspend()
        {
            suspended = true;
            lifecycleVersion++;
            prewarmed = false;
            StopRuntime();
        }

        public void Resume() { if (!disposed) suspended = false; }

        private void EnsureStarted()
        {
            if (stationaryUpdates && (runtime.Status == LocationServiceStatus.Running ||
                runtime.Status == LocationServiceStatus.Initializing)) return;
            if (runtime.Status != LocationServiceStatus.Stopped) runtime.Stop();
            runtime.Start(10, 0);
            stationaryUpdates = true;
        }

        private void CheckLifecycle(int requestVersion)
        {
            if (disposed) throw new OperationCanceledException("Location request ended.");
            if (suspended || requestVersion != lifecycleVersion)
                throw new ApiFailure("Location paused. Return to tagtag and try again.");
        }

        public async Task<LocationFix> Current(CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            int requestVersion = lifecycleVersion;
            CheckLifecycle(requestVersion);
            activeRequests++;
            try
            {
                CheckPermission();
                EnsureStarted();
                var deadline = runtime.UtcNow.AddSeconds(20);
                LocationFix lastFix = null;
                while (runtime.UtcNow < deadline)
                {
                    cancellation.ThrowIfCancellationRequested();
                    CheckLifecycle(requestVersion);
                    CheckPermission();
                    if (runtime.Status == LocationServiceStatus.Failed)
                        throw new ApiFailure("Location is unavailable. Try again outdoors.");
                    if (runtime.Status == LocationServiceStatus.Running)
                    {
                        lastFix = runtime.LastFix;
                        if (CollectionBook.FreshLocation(lastFix, runtime.UtcNow.ToUnixTimeSeconds())) return lastFix;
                    }
                    await runtime.Delay(cancellation);
                }
                CheckLifecycle(requestVersion);
                CheckPermission();
                if (lastFix != null)
                    Debug.LogWarning("[Tagtag location] outcome=timeout authorization=" + runtime.Authorization +
                        " accuracyMeters=" + lastFix.accuracyMeters +
                        " ageSeconds=" + (runtime.UtcNow.ToUnixTimeSeconds() - lastFix.measuredUnixSeconds));
                throw new ApiFailure("We need a more accurate location. Move outdoors and try again.");
            }
            finally
            {
                activeRequests--;
                if (activeRequests == 0 && !prewarmed) StopRuntime();
            }
        }
    }
}
