using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tagtag.AR.PlayMode.Tests
{
    public sealed class StickerRenderTests
    {
        [UnityTest]
        public IEnumerator NamedCameraSurfaceAcceptsTouchesButControlsAndSheetsBlockThem()
        {
            var host = new GameObject("Camera UI hit test");
            try
            {
                var document = host.AddComponent<UIDocument>();
                document.panelSettings = Resources.Load<PanelSettings>("Tagtag/UI/TagtagPanel");
                Assert.That(document.panelSettings, Is.Not.Null);
                var experience = host.AddComponent<ArExperience>();
                var root = document.rootVisualElement;
                root.style.width = Length.Percent(100f);
                root.style.height = Length.Percent(100f);
                var surface = new VisualElement { name = "STICK Camera Surface" };
                surface.style.position = Position.Absolute;
                surface.style.left = surface.style.right = surface.style.top = surface.style.bottom = 0f;
                surface.style.backgroundColor = Color.white;
                root.Add(surface);
                yield return null;
                var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var panelPoint = RuntimePanelUtils.ScreenToPanel(root.panel,
                    new Vector2(center.x, Screen.height - center.y));
                Assert.That(root.panel.Pick(panelPoint), Is.SameAs(surface));
                var hitTest = typeof(ArExperience).GetMethod("TouchOnUi", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(hitTest, Is.Not.Null);
                Assert.That(hitTest.Invoke(experience, new object[] { center }), Is.EqualTo(false));

                var control = new Button { name = "Camera control" };
                control.style.position = Position.Absolute;
                control.style.left = control.style.right = control.style.top = control.style.bottom = 0f;
                root.Add(control);
                yield return null;
                Assert.That(hitTest.Invoke(experience, new object[] { center }), Is.EqualTo(true));

                root.Remove(control);
                var sheet = new VisualElement { name = "Camera sheet" };
                sheet.style.position = Position.Absolute;
                sheet.style.left = sheet.style.right = sheet.style.top = sheet.style.bottom = 0f;
                sheet.style.backgroundColor = Color.white;
                root.Add(sheet);
                yield return null;
                Assert.That(hitTest.Invoke(experience, new object[] { center }), Is.EqualTo(true));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [UnityTest]
        public IEnumerator AllDefaultStickersAreAcceptedAndUseTheirOwnArtwork()
        {
            var host = new GameObject("Default sticker catalog render");
            try
            {
                var experience = host.AddComponent<ArExperience>();
                var selected = typeof(ArExperience).GetField("presetId", BindingFlags.Instance | BindingFlags.NonPublic);
                var create = typeof(ArExperience).GetMethod("CreateVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                var visualField = typeof(ArExperience).GetField("visual", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (string id in StickerPresets.Ids)
                {
                    experience.SelectPreset(id);
                    Assert.That(selected.GetValue(experience), Is.EqualTo(id));
                    Assert.That(create.Invoke(experience, new object[] { id }), Is.EqualTo(true));
                    var visual = (GameObject)visualField.GetValue(experience);
                    Assert.That(visual.GetComponent<Renderer>().sharedMaterial.mainTexture,
                        Is.SameAs(Resources.Load<Texture2D>("Tagtag/Presets/" + id)));
                    yield return null;
                }
                experience.SelectPreset("taggi-13");
                Assert.That(selected.GetValue(experience), Is.Null);
                yield return null;
            }
            finally
            {
                var visual = GameObject.Find("Tracked tagtag sticker");
                if (visual != null) Object.DestroyImmediate(visual);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void StickerCanBeTappedFromEitherVisibleSide()
        {
            var host = new GameObject("Sticker tap collider test");
            GameObject visual = null;
            bool previousBackfaceQueries = Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces = false;
                var experience = host.AddComponent<ArExperience>();
                var create = typeof(ArExperience).GetMethod("CreateVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(create.Invoke(experience, new object[] { "taggi-1" }), Is.True);
                visual = GameObject.Find("Tracked tagtag sticker");
                visual.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
                visual.transform.localScale = Vector3.one * 0.2f;
                Physics.SyncTransforms();

                foreach (float side in new[] { -1f, 1f })
                {
                    var ray = new Ray(Vector3.up * side, Vector3.down * side);
                    Assert.That(Physics.Raycast(ray, out var hit, 3f), Is.True,
                        "The visible sticker must be tappable from either side.");
                    Assert.That(hit.collider.gameObject, Is.SameAs(visual));
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = previousBackfaceQueries;
                if (visual != null) Object.DestroyImmediate(visual);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CustomArtworkUsesItsPixelsAndPortraitScale()
        {
            var host = new GameObject("Custom artwork render");
            var texture = new Texture2D(4, 5, TextureFormat.RGBA32, false);
            try
            {
                var experience = host.AddComponent<ArExperience>();
                experience.SelectArtwork(new StickerDesign { id = "portrait", width = 4, height = 5 }, texture);
                var create = typeof(ArExperience).GetMethod("CreateVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(create.Invoke(experience, new object[] { "design:portrait" }), Is.True);
                var visual = GameObject.Find("Tracked tagtag sticker");
                Assert.That(visual.GetComponent<Renderer>().sharedMaterial.mainTexture, Is.SameAs(texture));
                var scale = typeof(ArExperience).GetMethod("ArtworkScale", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(scale.Invoke(experience, new object[] { 0.2f }), Is.EqualTo(new Vector3(0.2f, 0.25f, 0.2f)));
            }
            finally
            {
                var visual = GameObject.Find("Tracked tagtag sticker");
                if (visual != null) Object.DestroyImmediate(visual);
                Object.DestroyImmediate(host); Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MissingArtworkReportsFailureWithoutLeavingAMagentaQuad()
        {
            var host = new GameObject("Missing sticker artwork test");
            try
            {
                var experience = host.AddComponent<ArExperience>();
                var create = typeof(ArExperience).GetMethod("CreateVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(create, Is.Not.Null);
                LogAssert.Expect(LogType.Error, new Regex("\\[TagtagSticker\\] Sticker material or artwork is unavailable"));
                Assert.That(create.Invoke(experience, new object[] { "missing-preset" }), Is.EqualTo(false));
                Assert.That(GameObject.Find("Tracked tagtag sticker"), Is.Null);
                Assert.That(experience.Status, Does.Contain("artwork could not load"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void RuntimeStickerMaterialIsIncludedAndRendersArtworkInsteadOfErrorMagenta()
        {
            var host = new GameObject("Sticker material render test");
            var cameraObject = new GameObject("Sticker material test camera");
            var target = new RenderTexture(32, 32, 16);
            var sample = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var knownArtwork = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var capture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            GameObject visual = null;
            try
            {
                var experience = host.AddComponent<ArExperience>();
                var create = typeof(ArExperience).GetMethod("CreateVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(create, Is.Not.Null);
                create.Invoke(experience, new object[] { "taggi-1" });
                visual = GameObject.Find("Tracked tagtag sticker");
                Assert.That(visual, Is.Not.Null);
                var material = visual.GetComponent<Renderer>().sharedMaterial;
                Assert.That(material, Is.Not.Null);
                var presetArtwork = Resources.Load<Texture2D>("Tagtag/Presets/taggi-1");
                Assert.That(material.mainTexture, Is.SameAs(presetArtwork),
                    "The runtime visual must use the shipped preset artwork.");
                knownArtwork.SetPixels(new[] { Color.green, Color.green, Color.green, Color.green });
                knownArtwork.Apply();
                material.mainTexture = knownArtwork;
                visual.transform.position = Vector3.zero;
                visual.transform.localScale = Vector3.one;
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 0.6f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                target.Create();
                var oldTarget = RenderTexture.active;
                try
                {
                    // Probe both sides so quad winding cannot masquerade as a shader failure.
                    var front = RenderCenter(camera, sample, new Vector3(0f, 0f, -2f), Quaternion.identity);
                    var back = RenderCenter(camera, sample, new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));
                    Assert.That(IsGreen(front) || IsGreen(back), Is.True,
                        $"Expected green artwork, got front {front} and back {back}; magenta means an error shader.");

                    camera.transform.SetPositionAndRotation(IsGreen(front) ? new Vector3(0f, 0f, -2f) :
                        new Vector3(0f, 0f, 2f), IsGreen(front) ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
                    material.mainTexture = presetArtwork;
                    camera.Render();
                    var ink = ReadPixel(camera.targetTexture, sample, 16, 16);
                    var dieCutCorner = ReadPixel(camera.targetTexture, sample, 5, 5);
                    RenderTexture.active = target;
                    capture.ReadPixels(new Rect(0f, 0f, 32f, 32f), 0, 0);
                    capture.Apply();
                    var capturePath = Path.Combine(Application.temporaryCachePath, "tagtag-sticker-render.png");
                    File.WriteAllBytes(capturePath, capture.EncodeToPNG());
                    Debug.Log("[TagtagStickerRender] capture=" + capturePath);
                    Assert.That(ink.r, Is.GreaterThan(0.7f), "The artwork body should retain its pale ink.");
                    Assert.That(ink.g, Is.GreaterThan(0.7f));
                    Assert.That(ink.b, Is.GreaterThan(0.7f));
                    Assert.That(dieCutCorner.maxColorComponent, Is.LessThan(0.12f),
                        "A transparent die-cut corner must reveal the black camera background.");

                    var includedMaterial = Resources.Load<Material>("Tagtag/AR/DeviceSticker");
                    Assert.That(includedMaterial, Is.Not.Null, "The player must include the sticker shader through a material asset.");
                    Assert.That(material.shader, Is.SameAs(includedMaterial.shader));
                    Assert.That(material.shader.isSupported, Is.True);
                }
                finally { RenderTexture.active = oldTarget; }
            }
            finally
            {
                Object.DestroyImmediate(visual);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sample);
                Object.DestroyImmediate(knownArtwork);
                Object.DestroyImmediate(capture);
            }
        }

        private static Color RenderCenter(Camera camera, Texture2D sample, Vector3 position, Quaternion rotation)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.Render();
            return ReadPixel(camera.targetTexture, sample, 16, 16);
        }

        private static Color ReadPixel(RenderTexture target, Texture2D sample, int x, int y)
        {
            RenderTexture.active = target;
            sample.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            sample.Apply();
            return sample.GetPixel(0, 0);
        }

        private static bool IsGreen(Color color)
        {
            return color.g > 0.6f && color.r < 0.25f && color.b < 0.25f;
        }
    }
}
