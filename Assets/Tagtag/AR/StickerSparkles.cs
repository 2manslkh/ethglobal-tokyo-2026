using System;
using UnityEngine;

namespace Tagtag.AR
{
    internal sealed class StickerSparkles : MonoBehaviour
    {
        private const int StarCount = 4;
        private const float BaseScale = 0.13f;
        private readonly Transform[] stars = new Transform[StarCount];
        private Mesh starMesh;
        private Material starMaterial;
        private Texture2D starTexture;
        private bool visible;
        private float pulseStartedAt;

        public static StickerSparkles Attach(GameObject stickerVisual)
        {
            if (stickerVisual == null) throw new ArgumentNullException(nameof(stickerVisual));
            var existing = stickerVisual.GetComponent<StickerSparkles>();
            if (existing != null) return existing;

            var template = Resources.Load<Material>("Tagtag/AR/DeviceSticker");
            if (template == null || template.shader == null || !template.shader.isSupported)
                throw new InvalidOperationException("The bundled sticker shader is unavailable for sparkles.");

            var sparkles = stickerVisual.AddComponent<StickerSparkles>();
            sparkles.CreateStars(template);
            sparkles.SetVisible(true);
            return sparkles;
        }

        public void SetVisible(bool isVisible)
        {
            if (visible == isVisible) return;
            visible = isVisible;
            if (isVisible) pulseStartedAt = Time.unscaledTime;
            for (var i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                if (isVisible) stars[i].localScale = Vector3.one * BaseScale;
                stars[i].gameObject.SetActive(isVisible);
            }
        }

        private void CreateStars(Material template)
        {
            starMesh = CreateStarMesh();
            starTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Sticker sparkle yellow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            starTexture.SetPixel(0, 0, new Color(1f, 0.84f, 0.17f, 1f));
            starTexture.Apply();
            starMaterial = new Material(template) { name = "Sticker sparkle material", mainTexture = starTexture };

            var positions = new[]
            {
                new Vector3(-0.57f, 0.34f, -0.01f),
                new Vector3(0.57f, 0.28f, -0.01f),
                new Vector3(-0.45f, -0.53f, -0.01f),
                new Vector3(0.49f, -0.51f, -0.01f)
            };
            for (var i = 0; i < StarCount; i++)
            {
                var star = new GameObject("Sticker sparkle " + (i + 1));
                star.transform.SetParent(transform, false);
                star.transform.localPosition = positions[i];
                star.transform.localScale = Vector3.one * BaseScale;
                star.AddComponent<MeshFilter>().sharedMesh = starMesh;
                star.AddComponent<MeshRenderer>().sharedMaterial = starMaterial;
                stars[i] = star.transform;
            }
        }

        private static Mesh CreateStarMesh()
        {
            var vertices = new Vector3[9];
            var uv = new Vector2[9];
            var triangles = new int[8 * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI / 4f;
                var radius = i % 2 == 0 ? 0.5f : 0.17f;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, 0f);
                uv[i + 1] = new Vector2(vertices[i + 1].x + 0.5f, vertices[i + 1].y + 0.5f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % 8 + 1;
            }
            var mesh = new Mesh { name = "Sticker sparkle star" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Update()
        {
            if (!visible) return;
            var elapsed = Time.unscaledTime - pulseStartedAt;
            for (var i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                var pulse = 0.92f + 0.08f * Mathf.Sin(elapsed * 3.5f - i * 0.9f);
                stars[i].localScale = Vector3.one * (BaseScale * pulse);
            }
        }

        private void OnDestroy()
        {
            foreach (var star in stars)
                if (star != null) DestroyOwned(star.gameObject);
            DestroyOwned(starMesh);
            DestroyOwned(starMaterial);
            DestroyOwned(starTexture);
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
