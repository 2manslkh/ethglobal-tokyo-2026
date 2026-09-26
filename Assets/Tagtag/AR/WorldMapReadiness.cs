using UnityEngine.XR.ARKit;

namespace Tagtag.AR
{
    // A live mapping status is only permission to try serialization. Readiness
    // requires bytes that Capture can reuse for this exact placement.
    public sealed class WorldMapReadiness
    {
        public const int MaxMapBytes = 16 * 1024 * 1024 - 512 * 1024 - 32;
        private const double FreshSeconds = 10;
        private bool eligible;
        private int revision;
        private int generation;
        private byte[] map;
        private double validUntil;
        private double nextValidation;
        public bool IsValidating { get; private set; }

        public void Observe(bool canValidate, int placementRevision)
        {
            if (!canValidate || revision != placementRevision)
                Invalidate();
            eligible = canValidate;
            revision = placementRevision;
        }

        public void Invalidate()
        {
            generation++;
            eligible = false;
            IsValidating = false;
            map = null;
            validUntil = 0;
            nextValidation = 0;
        }

        public bool CanValidate(double now) => eligible && !IsValidating && now >= nextValidation;

        public int BeginValidation(double now)
        {
            if (!CanValidate(now)) return -1;
            IsValidating = true;
            return ++generation;
        }

        public void CompleteValidation(int request, ARWorldMapRequestStatus status, byte[] bytes, double now)
        {
            if (!eligible || !IsValidating || request != generation) return;
            IsValidating = false;
            map = status == ARWorldMapRequestStatus.Success && bytes != null &&
                bytes.Length > 0 && bytes.Length <= MaxMapBytes ? bytes : null;
            validUntil = map == null ? 0 : now + FreshSeconds;
            nextValidation = now + (map == null ? 2 : 5);
        }

        public bool TryGetMap(int placementRevision, double now, out byte[] bytes)
        {
            bytes = eligible && placementRevision == revision && now < validUntil ? map : null;
            return bytes != null;
        }
    }
}
