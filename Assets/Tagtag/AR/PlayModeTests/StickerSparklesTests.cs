using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tagtag.AR.PlayMode.Tests
{
    public sealed class StickerSparklesTests
    {
        [Test]
        public void RepeatingVisibleDoesNotRestartParticleEmission()
        {
            var visual = new GameObject("Found sticker visual");
            try
            {
                var type = typeof(ArExperience).Assembly.GetType("Tagtag.AR.StickerSparkles");
                var sparkles = type.GetMethod("Attach", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { visual });
                type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sparkles, null);
                var count = (int)type.GetField("emittedCount", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(sparkles);
                Assert.That(count, Is.GreaterThan(0));

                type.GetMethod("SetVisible", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(sparkles, new object[] { true });
                Assert.That(type.GetField("emittedCount", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(sparkles), Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(visual); }
        }

        [Test]
        public void ParticlesEmitOnlyWhileVisibleAndNeverAddTouchColliders()
        {
            var visual = new GameObject("Found sticker visual");
            try
            {
                var type = typeof(ArExperience).Assembly.GetType("Tagtag.AR.StickerSparkles");
                var sparkles = type.GetMethod("Attach", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { visual });
                var setVisible = type.GetMethod("SetVisible", BindingFlags.Public | BindingFlags.Instance);
                var update = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                var particles = visual.GetComponentInChildren<ParticleSystem>();
                Assert.That(particles, Is.Not.Null);
                Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<Collider2D>(true), Is.Empty);

                update.Invoke(sparkles, null);
                Assert.That(particles.particleCount, Is.GreaterThan(0));
                setVisible.Invoke(sparkles, new object[] { false });
                Assert.That(particles.gameObject.activeSelf, Is.False);
                Assert.That(particles.particleCount, Is.Zero);
                setVisible.Invoke(sparkles, new object[] { true });
                update.Invoke(sparkles, null);
                Assert.That(particles.gameObject.activeSelf, Is.True);
                Assert.That(particles.particleCount, Is.GreaterThan(0));
                Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally { Object.DestroyImmediate(visual); }
        }
    }
}
