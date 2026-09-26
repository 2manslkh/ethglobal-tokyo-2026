using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tagtag.AR.PlayMode.Tests
{
    public sealed class StickerSparklesTests
    {
        [UnityTest]
        public IEnumerator RepeatingVisibleDoesNotRestartThePulse()
        {
            var visual = new GameObject("Found sticker visual");
            try
            {
                var type = typeof(ArExperience).Assembly.GetType("Tagtag.AR.StickerSparkles");
                Assert.That(type, Is.Not.Null);
                var attach = type.GetMethod("Attach", BindingFlags.Public | BindingFlags.Static);
                var setVisible = type.GetMethod("SetVisible", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(attach, Is.Not.Null);
                Assert.That(setVisible, Is.Not.Null);

                var sparkles = (MonoBehaviour)attach.Invoke(null, new object[] { visual });
                var star = visual.transform.GetChild(0);
                var initialScale = star.localScale.x;
                yield return new WaitForSecondsRealtime(0.2f);
                var pulsingScale = star.localScale.x;
                Assert.That(Mathf.Abs(pulsingScale - initialScale), Is.GreaterThan(0.001f));

                setVisible.Invoke(sparkles, new object[] { true });
                Assert.That(star.localScale.x, Is.EqualTo(pulsingScale).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(visual); }
        }

        [UnityTest]
        public IEnumerator SparklesPulseOnlyWhileVisibleAndNeverAddTouchColliders()
        {
            var visual = new GameObject("Found sticker visual");
            try
            {
                var type = typeof(ArExperience).Assembly.GetType("Tagtag.AR.StickerSparkles");
                Assert.That(type, Is.Not.Null);
                var attach = type.GetMethod("Attach", BindingFlags.Public | BindingFlags.Static);
                var setVisible = type.GetMethod("SetVisible", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(attach, Is.Not.Null);
                Assert.That(setVisible, Is.Not.Null);

                var sparkles = (MonoBehaviour)attach.Invoke(null, new object[] { visual });
                Assert.That(sparkles.gameObject, Is.SameAs(visual));
                Assert.That(visual.transform.childCount, Is.EqualTo(4));
                Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<Collider2D>(true), Is.Empty);

                setVisible.Invoke(sparkles, new object[] { false });
                var hiddenScales = new Vector3[4];
                for (var i = 0; i < 4; i++)
                {
                    var star = visual.transform.GetChild(i);
                    Assert.That(star.gameObject.activeSelf, Is.False);
                    hiddenScales[i] = star.localScale;
                }
                yield return new WaitForSecondsRealtime(0.15f);
                for (var i = 0; i < 4; i++)
                    Assert.That(visual.transform.GetChild(i).localScale, Is.EqualTo(hiddenScales[i]));

                setVisible.Invoke(sparkles, new object[] { true });
                for (var i = 0; i < 4; i++)
                    Assert.That(visual.transform.GetChild(i).gameObject.activeSelf, Is.True);
                var firstScale = visual.transform.GetChild(0).localScale.x;
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(visual.transform.GetChild(0).localScale.x,
                    Is.Not.EqualTo(firstScale).Within(0.001f));

                setVisible.Invoke(sparkles, new object[] { false });
                var stoppedScale = visual.transform.GetChild(0).localScale;
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.That(visual.transform.GetChild(0).localScale, Is.EqualTo(stoppedScale));
                Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally { Object.DestroyImmediate(visual); }
        }
    }
}
