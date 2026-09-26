using System;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;

namespace Tagtag.AR
{
    public static class ArGates
    {
        public static bool CanSerializeWorldMap(ARWorldMappingStatus mappingStatus) =>
            mappingStatus == ARWorldMappingStatus.Extending || mappingStatus == ARWorldMappingStatus.Mapped;

        public static bool CanPublish(bool sessionTracking, bool anchorTracking, bool mapped, bool hasPreview, bool busy)
        {
            return sessionTracking && anchorTracking && mapped && hasPreview && !busy;
        }

        public static bool CanCollect(bool sessionTracking, bool recovered, bool anchorTracking, float distanceMeters, bool directHit)
        {
            return sessionTracking && recovered && anchorTracking && directHit &&
                distanceMeters >= 0f && distanceMeters <= 3f;
        }
    }

    public sealed class RecoveryGate
    {
        private float stableSeconds;
        private int stableFrames;

        public bool Observe(bool matchingAnchor, bool sessionTracking, bool freshCameraFrame, float deltaSeconds)
        {
            if (!matchingAnchor || !sessionTracking || !freshCameraFrame)
            {
                stableSeconds = 0f;
                stableFrames = 0;
                return false;
            }
            stableSeconds += Math.Max(0f, deltaSeconds);
            stableFrames++;
            return stableSeconds >= 1.5f && stableFrames >= 15;
        }
    }

    // Private upload octets: versioned header, exact ARKit anchor ID, then serialized ARWorldMap.
    public static class WorldMapEnvelope
    {
        private const int HeaderBytes = 24;
        private const int MaxBytes = 16 * 1024 * 1024;

        public static byte[] Encode(ulong first, ulong second, byte[] worldMap)
        {
            if (worldMap == null || worldMap.Length == 0 || worldMap.Length > MaxBytes - HeaderBytes)
                throw new ArgumentOutOfRangeException(nameof(worldMap));
            var bytes = new byte[HeaderBytes + worldMap.Length];
            bytes[0] = (byte)'T'; bytes[1] = (byte)'G'; bytes[2] = (byte)'W'; bytes[3] = (byte)'M';
            bytes[4] = 1;
            WriteUInt64(bytes, 8, first);
            WriteUInt64(bytes, 16, second);
            Buffer.BlockCopy(worldMap, 0, bytes, HeaderBytes, worldMap.Length);
            return bytes;
        }

        public static bool TryDecode(byte[] bytes, out TrackableId anchorId, out byte[] worldMap)
        {
            anchorId = default;
            worldMap = null;
            if (bytes == null || bytes.Length <= HeaderBytes || bytes.Length > MaxBytes ||
                bytes[0] != 'T' || bytes[1] != 'G' || bytes[2] != 'W' || bytes[3] != 'M' ||
                bytes[4] != 1 || bytes[5] != 0 || bytes[6] != 0 || bytes[7] != 0) return false;
            anchorId = new TrackableId(ReadUInt64(bytes, 8), ReadUInt64(bytes, 16));
            worldMap = new byte[bytes.Length - HeaderBytes];
            Buffer.BlockCopy(bytes, HeaderBytes, worldMap, 0, worldMap.Length);
            return true;
        }

        private static void WriteUInt64(byte[] bytes, int offset, ulong value)
        {
            for (var i = 0; i < 8; i++) bytes[offset + i] = (byte)(value >> (i * 8));
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (var i = 0; i < 8; i++) value |= (ulong)bytes[offset + i] << (i * 8);
            return value;
        }
    }
}
