using System.IO;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Tagtag.AR.PlayMode.Tests
{
    public sealed class PlaneHatchRenderTests
    {
        [Test]
        public void BundledHatchRendersClippedTranslucentPencilStrokes()
        {
            var includedMaterial = Resources.Load<Material>("Tagtag/AR/PlaneHatch");
            Assert.That(includedMaterial, Is.Not.Null, "The material must be included in player Resources.");
            Assert.That(includedMaterial.shader, Is.Not.Null);
            Assert.That(includedMaterial.shader.isSupported, Is.True);

            var surface = new GameObject("Hatch render surface");
            var cameraObject = new GameObject("Hatch render camera");
            var target = new RenderTexture(256, 256, 16);
            var sample = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var capture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var mesh = new Mesh();
            var oldTarget = RenderTexture.active;
            try
            {
                using (var boundary = new NativeArray<Vector2>(new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, .5f),
                    new Vector2(.5f, .5f), new Vector2(.5f, 1f), new Vector2(0f, 1f)
                }, Allocator.Temp))
                    Assert.That(new PlaneHatchMesh().Rebuild(boundary, mesh), Is.True);
                surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                surface.AddComponent<MeshRenderer>().sharedMaterial = includedMaterial;
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = .7f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transform.SetPositionAndRotation(new Vector3(.5f, 2f, .5f), Quaternion.Euler(90f, 0f, 0f));
                camera.targetTexture = target;
                target.Create();
                camera.Render();

                var center = ReadPixel(target, sample, 100, 100);
                var outside = ReadPixel(target, sample, 8, 8);
                var concaveNotch = ReadPixel(target, sample, 190, 190);
                Assert.That(center.r, Is.GreaterThan(.03f).And.LessThan(.75f),
                    "The surface must tint the camera image without becoming opaque.");
                Assert.That(center.r, Is.GreaterThan(center.g));
                Assert.That(center.g, Is.GreaterThan(center.b));
                Assert.That(outside.maxColorComponent, Is.LessThan(.02f),
                    "The hatch must be clipped to the detected boundary.");
                Assert.That(concaveNotch.maxColorComponent, Is.LessThan(.02f),
                    "The concave cutout must reveal the camera image.");

                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                capture.Apply();
                var darkest = 1f;
                var lightest = 0f;
                for (var x = 100; x <= 210; x++)
                {
                    var red = capture.GetPixel(x, 100).r;
                    darkest = Mathf.Min(darkest, red);
                    lightest = Mathf.Max(lightest, red);
                }
                Assert.That(lightest - darkest, Is.GreaterThan(.07f),
                    "Pencil strokes must remain distinguishable from the translucent wash.");
                var capturePath = Path.Combine(Application.temporaryCachePath, "tagtag-plane-hatch-render.png");
                File.WriteAllBytes(capturePath, capture.EncodeToPNG());
                Debug.Log("[TagtagPlaneHatchRender] capture=" + capturePath);
            }
            finally
            {
                RenderTexture.active = oldTarget;
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sample);
                Object.DestroyImmediate(capture);
                Object.DestroyImmediate(mesh);
            }
        }

        private static Color ReadPixel(RenderTexture target, Texture2D sample, int x, int y)
        {
            RenderTexture.active = target;
            sample.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            sample.Apply();
            return sample.GetPixel(0, 0);
        }
    }
}
