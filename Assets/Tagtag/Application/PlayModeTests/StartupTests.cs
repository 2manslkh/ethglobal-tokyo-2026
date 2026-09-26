using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tagtag.Tests
{
    public sealed class StartupTests
    {
        [UnityTest]
        public IEnumerator EntrySceneRendersLoginOnFirstLaunch()
        {
            Application.runInBackground = true;
            yield return SceneManager.LoadSceneAsync("Tagtag", LoadSceneMode.Single);
            for (int frame = 0; frame < 10; frame++) yield return null;

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null, "The entry scene must create its UI document.");
            var root = document.rootVisualElement;
            Assert.That(root, Is.Not.Null);
            Assert.That(root.childCount, Is.GreaterThan(0), "Startup must render login, not leave an empty panel.");
            Assert.That(root.Q<Button>("Action Continue with Apple"), Is.Not.Null);
            Assert.That(root.Q<Button>("Action Continue with Google"), Is.Not.Null);
            Assert.That(root.Q<Button>("Tab Explore"), Is.Null);
            Assert.That(root.Q("Login video background"), Is.Not.Null);
            var fallback = GameObject.Find("Paper camera fallback")?.GetComponent<Camera>();
            Assert.That(fallback, Is.Not.Null);
            Assert.That(fallback.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(fallback.backgroundColor.a, Is.EqualTo(1f));
            Assert.That(fallback.cullingMask, Is.Zero);

            var target = new RenderTexture(390, 844, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            for (int frame = 0; frame < 10; frame++) yield return null;
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(390, 844, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            string capture = Path.Combine(Application.temporaryCachePath, "tagtag-startup.png");
            File.WriteAllBytes(capture, pixels.EncodeToPNG());
            Debug.Log("tagtag startup screenshot: " + capture);
            int lightPixels = pixels.GetPixels32().Count(pixel => pixel.r > 200 && pixel.g > 200 && pixel.b > 180 && pixel.a > 200);
            document.panelSettings.targetTexture = null;
            Object.Destroy(pixels);
            Object.Destroy(target);
            Assert.That(lightPixels, Is.GreaterThan(390 * 844 / 5), "Login must render its readable foreground rather than a black screen.");
        }
    }
}
