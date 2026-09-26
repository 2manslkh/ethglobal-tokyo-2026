using System;
using System.Collections.Generic;
using System.Linq;

namespace Tagtag
{
    public static class CollectionBook
    {
        public const int PageSize = 20;

        public static List<CollectedSticker> Normalize(IEnumerable<CollectedSticker> stickers)
        {
            return (stickers ?? Array.Empty<CollectedSticker>())
                .Where(item => item != null && !string.IsNullOrEmpty(item.id))
                .GroupBy(item => item.id)
                .Select(group => group.OrderByDescending(item => item.revision).First())
                .OrderBy(item => item.collectedAt).ThenBy(item => item.id, StringComparer.Ordinal).ToList();
        }

        public static int PageCount(int count) => Math.Max(1, (Math.Max(0, count) + PageSize - 1) / PageSize);

        public static bool FreshLocationTimestamp(LocationFix location, long now) =>
            location != null && now - location.measuredUnixSeconds >= -5 && now - location.measuredUnixSeconds <= 30;

        public static bool FreshLocation(LocationFix location, long now, float maxAccuracyMeters = 50)
        {
            return location != null && !double.IsNaN(location.latitude) && !double.IsInfinity(location.latitude)
                && !double.IsNaN(location.longitude) && !double.IsInfinity(location.longitude)
                && Math.Abs(location.latitude) <= 90 && Math.Abs(location.longitude) <= 180
                && !float.IsNaN(location.accuracyMeters) && location.accuracyMeters >= 0 && location.accuracyMeters <= maxAccuracyMeters
                && FreshLocationTimestamp(location, now);
        }

        public static bool CanUnlock(RecoveryData recovery, string tappedId, bool arCanCollect, long now)
        {
            return arCanCollect && recovery != null && recovery.sticker != null
                && recovery.sticker.id == tappedId && !string.IsNullOrEmpty(recovery.discoveryId)
                && recovery.expiresAt > now;
        }
    }
}
