using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tagtag.Tests
{
    public sealed class AppStoreCaptureTests
    {
        [UnityTest]
        public IEnumerator CaptureCurrentCampaignScreens()
        {
            var fixture = new HomeLibraryVisualTests();
            var type = fixture.GetType();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            fixture.SetUp();
            try
            {
                var controller = (ITagtagController)type.GetField("controller", flags).GetValue(fixture);
                controller.State.creationCapabilities = 15;
                for (int i = 0; i < 20; i++)
                    controller.State.collection.Add(new CollectedSticker {
                        id = "campaign-" + i,
                        presetId = "taggi-" + (i % 12 + 1)
                    });
                yield return (IEnumerator)type.GetMethod("Mount", flags).Invoke(fixture, new object[] { 1f, 1170, 2532 });
                yield return new WaitForSecondsRealtime(1);
                var target = (RenderTexture)type.GetField("target", flags).GetValue(fixture);
                Capture(target, "collected-current.png");
                type.GetMethod("Submit", flags).Invoke(fixture, new object[] { "Home My designs" });
                yield return new WaitForSecondsRealtime(.5f);
                type.GetMethod("Submit", flags).Invoke(fixture, new object[] { "Home Make sticker" });
                yield return new WaitForSecondsRealtime(.7f);
                Capture(target, "add-sticker-current.png");
            }
            finally { fixture.Cleanup(); }
        }

        private static void Capture(RenderTexture target, string filename)
        {
            var prior = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = prior;
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/app-store/captures"));
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, filename), pixels.EncodeToPNG());
            Object.Destroy(pixels);
        }
    }
}
