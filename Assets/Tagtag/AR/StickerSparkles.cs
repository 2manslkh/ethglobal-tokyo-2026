using System;
using UnityEngine;

namespace Tagtag.AR
{
    internal sealed class StickerSparkles : MonoBehaviour
    {
        private const float EmissionInterval = 0.12f;
        private ParticleSystem particles;
        private Material material;
        private Texture2D texture;
        private float nextEmissionAt;
        private int emittedCount;
        private bool visible;

        public static StickerSparkles Attach(GameObject stickerVisual)
        {
            if (stickerVisual == null) throw new ArgumentNullException(nameof(stickerVisual));
            var existing = stickerVisual.GetComponent<StickerSparkles>();
            if (existing != null) return existing;
            var particleShader = Resources.Load<Shader>("Tagtag/AR/StickerParticle");
            if (particleShader == null || !particleShader.isSupported)
                throw new InvalidOperationException("The bundled particle shader is unavailable for sparkles.");
            var sparkles = stickerVisual.AddComponent<StickerSparkles>();
            sparkles.CreateEmitter(particleShader);
            sparkles.SetVisible(true);
            return sparkles;
        }

        public void SetVisible(bool isVisible)
        {
            if (visible == isVisible) return;
            visible = isVisible;
            if (particles == null) return;
            particles.gameObject.SetActive(isVisible);
            if (isVisible)
            {
                nextEmissionAt = Time.unscaledTime;
                emittedCount = 0;
                particles.Play();
            }
            else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void CreateEmitter(Shader particleShader)
        {
            texture = CreateStarTexture();
            material = new Material(particleShader) { name = "Sticker sparkle particles", mainTexture = texture };
            var emitter = new GameObject("Sticker sparkle particles");
            emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = new Vector3(0f, 0f, -0.025f);
            particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = 0.12f;
            main.startColor = new Color(1f, 0.86f, 0.2f, 1f);
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = particles.emission;
            emission.enabled = false;
            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(0.9f, 0.65f), new GradientAlphaKey(0f, 1f) });
            colors.color = gradient;
            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            emitter.SetActive(false);
        }

        private static Texture2D CreateStarTexture()
        {
            const int size = 32;
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Sticker sparkle particle artwork",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.Lerp(0.28f, 0.94f,
                    Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * Mathf.Atan2(dy, dx))), 8f));
                result.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((edge - radius) * 12f)));
            }
            result.Apply();
            return result;
        }

        private void Update()
        {
            if (!visible || particles == null) return;
            while (Time.unscaledTime >= nextEmissionAt)
            {
                int side = emittedCount++ % 4;
                float along = Mathf.Sin(emittedCount * 2.4f) * 0.42f;
                Vector3 position = side == 0 ? new Vector3(-0.55f, along, 0f) :
                    side == 1 ? new Vector3(0.55f, along, 0f) :
                    side == 2 ? new Vector3(along, 0.55f, 0f) : new Vector3(along, -0.55f, 0f);
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(position.x * 0.22f, 0.13f + position.y * 0.08f, 0f),
                    rotation = emittedCount * 37f
                }, 1);
                nextEmissionAt += EmissionInterval;
            }
        }

        private void OnDestroy()
        {
            DestroyOwned(material);
            DestroyOwned(texture);
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
