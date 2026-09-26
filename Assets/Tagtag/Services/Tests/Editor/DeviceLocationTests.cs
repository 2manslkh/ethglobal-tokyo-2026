using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.Services.Tests
{
    public sealed class DeviceLocationTests
    {
        [Test]
        public void ReducedPrecisionStopsBeforeTheTwentySecondWait()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.ReducedAccuracy,
                Status = LocationServiceStatus.Running, LastFix = Fix(1000, 100) };

            ApiFailure error = Assert.ThrowsAsync<ApiFailure>(async () => await new DeviceLocation(runtime).Current());

            StringAssert.Contains("Precise Location", error.Message);
            StringAssert.Contains("Settings", error.Message);
            Assert.That(runtime.DelayCount, Is.Zero);
        }

        [Test]
        public void DeniedAppPermissionDoesNotStartLocationOrWait()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.Denied,
                Status = LocationServiceStatus.Stopped, ServicesEnabled = true };

            ApiFailure error = Assert.ThrowsAsync<ApiFailure>(async () => await new DeviceLocation(runtime).Current());

            StringAssert.Contains("Settings", error.Message);
            Assert.That(runtime.StartCount, Is.Zero);
            Assert.That(runtime.DelayCount, Is.Zero);
        }

        [Test]
        public async Task StationaryStaleFixCanRefreshWithoutMovingThePhone()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                Status = LocationServiceStatus.Stopped, LastFix = Fix(5, 50) };
            runtime.AfterDelay = () =>
            {
                if (runtime.UpdateDistanceMeters == 0)
                    runtime.LastFix = Fix(8, runtime.UtcNow.ToUnixTimeSeconds());
            };

            LocationFix fix = await new DeviceLocation(runtime).Current();

            Assert.That(runtime.UpdateDistanceMeters, Is.Zero);
            Assert.That(fix.accuracyMeters, Is.EqualTo(8));
            Assert.That(fix.measuredUnixSeconds, Is.EqualTo(runtime.UtcNow.ToUnixTimeSeconds()));
        }

        [Test]
        public async Task RestartsRunningLocationSessionWhenItsFixIsStaleAndInaccurate()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                Status = LocationServiceStatus.Stopped, LastFix = Fix(2000, 20) };
            var location = new DeviceLocation(runtime);
            location.Prewarm();
            runtime.AfterDelay = () =>
            {
                if (runtime.StartCount > 1)
                    runtime.LastFix = Fix(8, runtime.UtcNow.ToUnixTimeSeconds());
            };

            LocationFix fix = await location.Current(maxAccuracyMeters: 100);

            Assert.That(runtime.StartCount, Is.EqualTo(2));
            Assert.That(fix.accuracyMeters, Is.EqualTo(8));
            Assert.That(runtime.DelayCount, Is.EqualTo(1));
            location.Stop();
        }

        [Test]
        public async Task ReturnsTheMeasuredTimestampWithoutRefreshingItArtificially()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                Status = LocationServiceStatus.Running, LastFix = Fix(8, 90) };

            LocationFix fix = await new DeviceLocation(runtime).Current();

            Assert.That(fix.measuredUnixSeconds, Is.EqualTo(90));
            Assert.That(runtime.DelayCount, Is.Zero);
        }

        [Test]
        public void FullPrecisionStillRejectsAnInaccurateFix()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                Status = LocationServiceStatus.Running, LastFix = Fix(51, 100) };

            ApiFailure error = Assert.ThrowsAsync<ApiFailure>(async () => await new DeviceLocation(runtime).Current());

            StringAssert.Contains("accuracy is still low", error.Message);
            Assert.That(error.Message, Does.Not.Contain("Settings"));
        }

        [Test]
        public async Task PublishingAcceptsLocationFixesWithinOneHundredMeters()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                Status = LocationServiceStatus.Running, LastFix = Fix(75, 100) };

            LocationFix fix = await new DeviceLocation(runtime).Current(maxAccuracyMeters: 100);

            Assert.That(fix.accuracyMeters, Is.EqualTo(75));
            Assert.That(runtime.DelayCount, Is.Zero);
        }

        [Test]
        public void PrewarmSkipsKnownReducedPrecisionAndDoesNotRequestUpdates()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.ReducedAccuracy };

            new DeviceLocation(runtime).Prewarm();

            Assert.That(runtime.StartCount, Is.Zero);
        }

        [Test]
        public void PrewarmDoesNotRestartAnInitializingPermissionRequest()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.Unknown };
            var location = new DeviceLocation(runtime);

            location.Prewarm();
            runtime.Status = LocationServiceStatus.Initializing;
            location.Prewarm();

            Assert.That(runtime.StartCount, Is.EqualTo(1));
            Assert.That(runtime.StopCount, Is.Zero);
        }

        [Test]
        public async Task ReturningWithFullPrecisionCanUseANewMeasuredFix()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.ReducedAccuracy,
                LastFix = Fix(1000, 100) };
            var location = new DeviceLocation(runtime);
            location.Prewarm();
            runtime.Authorization = LocationAuthorization.FullAccuracy;
            runtime.AfterDelay = () => runtime.LastFix = Fix(8, runtime.UtcNow.ToUnixTimeSeconds());

            location.Prewarm();
            LocationFix fix = await location.Current();

            Assert.That(runtime.StartCount, Is.EqualTo(1));
            Assert.That(fix.accuracyMeters, Is.EqualTo(8));
            Assert.That(fix.measuredUnixSeconds, Is.EqualTo(runtime.UtcNow.ToUnixTimeSeconds()));
        }

        [Test]
        public void SuspendingAnActiveWaitStopsUpdatesAndEndsThatRequest()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                Status = LocationServiceStatus.Stopped, LastFix = Fix(100, 100) };
            var location = new DeviceLocation(runtime);
            location.Prewarm();
            runtime.AfterDelay = location.Suspend;

            ApiFailure error = Assert.ThrowsAsync<ApiFailure>(async () => await location.Current());

            StringAssert.Contains("paused", error.Message);
            Assert.That(runtime.StopCount, Is.EqualTo(1));
            Assert.That(runtime.DelayCount, Is.EqualTo(1));
        }

        [Test]
        public async Task OneShotSuccessfulFixStopsContinuousUpdates()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                LastFix = Fix(8, 100) };

            LocationFix fix = await new DeviceLocation(runtime).Current();

            Assert.That(fix.accuracyMeters, Is.EqualTo(8));
            Assert.That(runtime.StartCount, Is.EqualTo(1));
            Assert.That(runtime.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void OneShotTimeoutStopsContinuousUpdates()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                LastFix = Fix(100, 100) };

            Assert.ThrowsAsync<ApiFailure>(async () => await new DeviceLocation(runtime).Current());

            Assert.That(runtime.StartCount, Is.EqualTo(1));
            Assert.That(runtime.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void CanceledOneShotStopsContinuousUpdates()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                LastFix = Fix(100, 100) };
            using (var cancellation = new CancellationTokenSource())
            {
                runtime.AfterDelay = cancellation.Cancel;

                Assert.CatchAsync<OperationCanceledException>(async () =>
                    await new DeviceLocation(runtime).Current(cancellation.Token));
            }

            Assert.That(runtime.StopCount, Is.EqualTo(1));
        }

        [Test]
        public async Task PlacementPrewarmRetainsUpdatesAcrossIndependentFixesUntilReleased()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                LastFix = Fix(8, 100) };
            var location = new DeviceLocation(runtime);
            location.Prewarm();

            await location.Current();
            await location.Current();

            Assert.That(runtime.StartCount, Is.EqualTo(1));
            Assert.That(runtime.StopCount, Is.Zero);
            location.Stop();
            Assert.That(runtime.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void DisposedLocationCannotRestartFromALateRequest()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy };
            var location = new DeviceLocation(runtime);
            location.Dispose();

            location.Prewarm();
            Assert.CatchAsync<OperationCanceledException>(async () => await location.Current());

            Assert.That(runtime.StartCount, Is.Zero);
        }

        [Test]
        public async Task CancelingOneOverlappingRequestKeepsTheOtherLocationLease()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                LastFix = Fix(100, 100) };
            var waits = new System.Collections.Generic.List<TaskCompletionSource<bool>>();
            runtime.DelayOverride = _ =>
            {
                var wait = new TaskCompletionSource<bool>();
                waits.Add(wait);
                return wait.Task;
            };
            var location = new DeviceLocation(runtime);
            using (var canceled = new CancellationTokenSource())
            {
                Task<LocationFix> first = location.Current(canceled.Token);
                Task<LocationFix> second = location.Current();
                Assert.That(waits.Count, Is.EqualTo(2));

                canceled.Cancel();
                waits[0].SetResult(true);
                Assert.CatchAsync<OperationCanceledException>(async () => await first);
                Assert.That(runtime.StopCount, Is.Zero);

                runtime.LastFix = Fix(8, 100);
                waits[1].SetResult(true);
                await second;
            }

            Assert.That(runtime.StartCount, Is.EqualTo(1));
            Assert.That(runtime.StopCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ReleasingPlacementPrewarmKeepsAnActiveOneShotLease()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                LastFix = Fix(100, 100) };
            var wait = new TaskCompletionSource<bool>();
            runtime.DelayOverride = _ => wait.Task;
            var location = new DeviceLocation(runtime);
            location.Prewarm();
            Task<LocationFix> current = location.Current();

            location.Stop();
            Assert.That(runtime.StopCount, Is.Zero);

            runtime.LastFix = Fix(8, 100);
            wait.SetResult(true);
            await current;
            Assert.That(runtime.StopCount, Is.EqualTo(1));
        }

        [Test]
        public async Task PublicationFallsBackToMeasuredApproximateFixAfterThreeSeconds()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                LastFix = Fix(2000.149f, 100) };
            LocationFix fix = await new DeviceLocation(runtime).Current(maxAccuracyMeters: 5000, preferredAccuracyMeters: 100);
            Assert.That(runtime.DelayCount, Is.EqualTo(3));
            Assert.That(fix.accuracyMeters, Is.EqualTo(2000.149f));
            Assert.That(fix.measuredUnixSeconds, Is.EqualTo(100));
        }

        [Test]
        public async Task CollectionUsesFreshApproximateFixWithoutWaitingForPrecision()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                LastFix = Fix(2000.149f, 100) };

            LocationFix fix = await new DeviceLocation(runtime).Current(maxAccuracyMeters: 5000);

            Assert.That(fix.accuracyMeters, Is.EqualTo(2000.149f));
            Assert.That(runtime.DelayCount, Is.Zero);
        }

        [Test]
        public async Task PublicationUsesPreciseFixWhenItArrivesDuringShortWait()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.FullAccuracy,
                LastFix = Fix(2000, 100) };
            runtime.AfterDelay = () => runtime.LastFix = Fix(20, 101);
            LocationFix fix = await new DeviceLocation(runtime).Current(maxAccuracyMeters: 5000, preferredAccuracyMeters: 100);
            Assert.That(runtime.DelayCount, Is.EqualTo(1));
            Assert.That(fix.accuracyMeters, Is.EqualTo(20));
        }

        [Test]
        public async Task ApproximatePublicationCanUseReducedAccuracyPermission()
        {
            var runtime = new LocationRuntime { Authorization = LocationAuthorization.ReducedAccuracy,
                LastFix = Fix(2000, 100) };
            LocationFix fix = await new DeviceLocation(runtime).Current(maxAccuracyMeters: 5000, preferredAccuracyMeters: 100);
            Assert.That(fix.accuracyMeters, Is.EqualTo(2000));
            Assert.That(runtime.DelayCount, Is.EqualTo(3));
        }

        private static LocationFix Fix(float accuracy, long measuredAt) => new LocationFix
        { latitude = 35.68, longitude = 139.76, accuracyMeters = accuracy, measuredUnixSeconds = measuredAt };

        private sealed class LocationRuntime : ILocationRuntime
        {
            public LocationAuthorization Authorization { get; set; } = LocationAuthorization.Unknown;
            public bool ServicesEnabled { get; set; } = true;
            public LocationServiceStatus Status { get; set; } = LocationServiceStatus.Stopped;
            public LocationFix LastFix { get; set; }
            public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.FromUnixTimeSeconds(100);
            public int StartCount { get; private set; }
            public int DelayCount { get; private set; }
            public int StopCount { get; private set; }
            public float UpdateDistanceMeters { get; private set; } = -1;
            public Action AfterDelay { get; set; }
            public Func<CancellationToken, Task> DelayOverride { get; set; }

            public void Start(float desiredAccuracyMeters, float updateDistanceMeters)
            {
                StartCount++;
                UpdateDistanceMeters = updateDistanceMeters;
                Status = LocationServiceStatus.Running;
            }

            public void Stop()
            {
                StopCount++;
                Status = LocationServiceStatus.Stopped;
            }

            public Task Delay(CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                DelayCount++;
                if (DelayOverride != null) return DelayOverride(cancellation);
                UtcNow = UtcNow.AddSeconds(1);
                AfterDelay?.Invoke();
                return Task.CompletedTask;
            }
        }
    }
}
