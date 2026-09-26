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

    // Private upload octets: exact ARKit anchor ID and serialized ARWorldMap.
    // V2 also carries a bounded JPEG of the original spot before the map bytes.
    public static class WorldMapEnvelope
    {
        private const int V1HeaderBytes = 24;
        private const int V2HeaderBytes = 32;
        private const int MaxBytes = 16 * 1024 * 1024;

        // Kept for callers that still need to construct legacy map-only fixtures.
        public static byte[] Encode(ulong first, ulong second, byte[] worldMap)
        {
            if (worldMap == null || worldMap.Length == 0 || worldMap.Length > MaxBytes - V1HeaderBytes)
                throw new ArgumentOutOfRangeException(nameof(worldMap));
            var bytes = new byte[V1HeaderBytes + worldMap.Length];
            WriteHeader(bytes, 1, first, second);
            Buffer.BlockCopy(worldMap, 0, bytes, V1HeaderBytes, worldMap.Length);
            return bytes;
        }

        public static byte[] Encode(ulong first, ulong second, byte[] worldMap, byte[] photo)
        {
            if (!ReferencePhotoCapture.TryDimensions(photo, out _, out _))
                throw new ArgumentException("A valid bounded JPEG reference photo is required.", nameof(photo));
            if (worldMap == null || worldMap.Length == 0 ||
                (long)V2HeaderBytes + photo.Length + worldMap.Length > MaxBytes)
                throw new ArgumentOutOfRangeException(nameof(worldMap));
            var bytes = new byte[V2HeaderBytes + photo.Length + worldMap.Length];
            WriteHeader(bytes, 2, first, second);
            WriteInt32(bytes, 24, photo.Length);
            WriteInt32(bytes, 28, worldMap.Length);
            Buffer.BlockCopy(photo, 0, bytes, V2HeaderBytes, photo.Length);
            Buffer.BlockCopy(worldMap, 0, bytes, V2HeaderBytes + photo.Length, worldMap.Length);
            return bytes;
        }

        private static void WriteHeader(byte[] bytes, byte version, ulong first, ulong second)
        {
            bytes[0] = (byte)'T'; bytes[1] = (byte)'G'; bytes[2] = (byte)'W'; bytes[3] = (byte)'M';
            bytes[4] = version;
            WriteUInt64(bytes, 8, first);
            WriteUInt64(bytes, 16, second);
        }

        public static bool TryDecode(byte[] bytes, out TrackableId anchorId, out byte[] worldMap)
            => TryDecode(bytes, out anchorId, out worldMap, out _);

        public static bool TryDecode(byte[] bytes, out TrackableId anchorId, out byte[] worldMap, out byte[] photo)
        {
            anchorId = default;
            worldMap = null;
            photo = null;
            if (bytes == null || bytes.Length <= V1HeaderBytes || bytes.Length > MaxBytes ||
                bytes[0] != 'T' || bytes[1] != 'G' || bytes[2] != 'W' || bytes[3] != 'M' ||
                bytes[5] != 0 || bytes[6] != 0 || bytes[7] != 0) return false;
            if (bytes[4] == 1)
            {
                anchorId = new TrackableId(ReadUInt64(bytes, 8), ReadUInt64(bytes, 16));
                worldMap = new byte[bytes.Length - V1HeaderBytes];
                Buffer.BlockCopy(bytes, V1HeaderBytes, worldMap, 0, worldMap.Length);
                return true;
            }
            if (bytes[4] != 2 || bytes.Length <= V2HeaderBytes) return false;
            int photoLength = ReadInt32(bytes, 24);
            int mapLength = ReadInt32(bytes, 28);
            if (photoLength < 1 || photoLength > ReferencePhotoCapture.MaxBytes || mapLength < 1 ||
                (long)V2HeaderBytes + photoLength + mapLength != bytes.Length) return false;
            var candidatePhoto = new byte[photoLength];
            Buffer.BlockCopy(bytes, V2HeaderBytes, candidatePhoto, 0, photoLength);
            if (!ReferencePhotoCapture.TryDimensions(candidatePhoto, out _, out _)) return false;
            var candidateMap = new byte[mapLength];
            Buffer.BlockCopy(bytes, V2HeaderBytes + photoLength, candidateMap, 0, mapLength);
            anchorId = new TrackableId(ReadUInt64(bytes, 8), ReadUInt64(bytes, 16));
            photo = candidatePhoto;
            worldMap = candidateMap;
            return true;
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (i * 8));
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            int value = 0;
            for (int i = 0; i < 4; i++) value |= bytes[offset + i] << (i * 8);
            return value;
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
