using System;
using UnityEngine;

namespace Tagtag.AR
{
    // Renders the AR camera only. UI Toolkit is outside this camera render target.
    public static class ReferencePhotoCapture
    {
        public const int MaxBytes = 512 * 1024;
        public const int MaxEdge = 1024;

        public static Vector2Int Size(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            float scale = Mathf.Min(1f, (float)MaxEdge / Mathf.Max(width, height));
            return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(height * scale)));
        }

        public static byte[] Capture(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            Vector2Int size = Size(camera.pixelWidth, camera.pixelHeight);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                foreach (int quality in new[] { 82, 70, 58 })
                {
                    byte[] jpeg = image.EncodeToJPG(quality);
                    if (TryDimensions(jpeg, out int width, out int height) &&
                        width == size.x && height == size.y) return jpeg;
                }
                throw new InvalidOperationException("The original spot photo could not fit the private map upload.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                DestroyTexture(image);
            }
        }

        // Check the JPEG header before LoadImage can allocate decoded pixels.
        public static bool TryDimensions(byte[] jpeg, out int width, out int height)
        {
            width = height = 0;
            if (jpeg == null || jpeg.Length < 32 || jpeg.Length > MaxBytes ||
                jpeg[0] != 0xff || jpeg[1] != 0xd8 ||
                jpeg[jpeg.Length - 2] != 0xff || jpeg[jpeg.Length - 1] != 0xd9) return false;
            int offset = 2;
            bool foundFrame = false;
            while (offset + 4 <= jpeg.Length)
            {
                if (jpeg[offset++] != 0xff) return false;
                while (offset < jpeg.Length && jpeg[offset] == 0xff) offset++;
                if (offset >= jpeg.Length) return false;
                int marker = jpeg[offset++];
                if (marker == 0xda) return foundFrame;
                if (marker == 0xd8 || marker == 0xd9 || marker == 0x00 ||
                    marker == 0x01 || marker >= 0xd0 && marker <= 0xd7) return false;
                if (offset + 2 > jpeg.Length) return false;
                int length = jpeg[offset] * 256 + jpeg[offset + 1];
                offset += 2;
                if (length < 2 || offset + length - 2 > jpeg.Length - 2) return false;
                bool frame = marker >= 0xc0 && marker <= 0xcf &&
                    marker != 0xc4 && marker != 0xc8 && marker != 0xcc;
                if (frame)
                {
                    if (foundFrame || length < 17 || jpeg[offset] != 8 || jpeg[offset + 5] != 3)
                        return false;
                    height = jpeg[offset + 1] * 256 + jpeg[offset + 2];
                    width = jpeg[offset + 3] * 256 + jpeg[offset + 4];
                    if (width < 1 || height < 1 || width > MaxEdge || height > MaxEdge)
                        return false;
                    foundFrame = true;
                }
                offset += length - 2;
            }
            return false;
        }

        public static bool TryLoad(byte[] jpeg, out Texture2D texture)
        {
            texture = null;
            if (!TryDimensions(jpeg, out int width, out int height)) return false;
            var loaded = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                if (!loaded.LoadImage(jpeg, true) || loaded.width != width || loaded.height != height)
                    return false;
                texture = loaded;
                loaded = null;
                return true;
            }
            catch { return false; }
            finally { DestroyTexture(loaded); }
        }

        public static void DestroyTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
