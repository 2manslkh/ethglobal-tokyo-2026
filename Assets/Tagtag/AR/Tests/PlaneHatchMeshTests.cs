using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Tagtag.AR.Tests
{
    public sealed class PlaneHatchMeshTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ConcaveBoundaryFillsOnlyDetectedPolygon(bool reverse)
        {
            var points = new[]
            {
                new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 2f), new Vector2(0f, 2f)
            };
            if (reverse) System.Array.Reverse(points);
            using (var boundary = new NativeArray<Vector2>(points, Allocator.Temp))
            {
                var mesh = new Mesh();
                try
                {
                    Assert.That(new PlaneHatchMesh().Rebuild(boundary, mesh), Is.True);
                    var vertices = mesh.vertices;
                    var triangles = mesh.triangles;
                    var area = 0f;
                    for (var i = 0; i < triangles.Length; i += 3)
                    {
                        var a = vertices[triangles[i]];
                        var b = vertices[triangles[i + 1]];
                        var c = vertices[triangles[i + 2]];
                        area += Mathf.Abs((b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x)) * .5f;
                        var center = (a + b + c) / 3f;
                        Assert.That(center.x > 1f && center.z > 1f, Is.False, "The concave notch must stay clear.");
                    }
                    Assert.That(area, Is.EqualTo(3f).Within(.0001f));
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test]
        public void RebuiltBoundaryRemovesOldTriangles()
        {
            var builder = new PlaneHatchMesh();
            var mesh = new Mesh();
            try
            {
                using (var square = new NativeArray<Vector2>(new[]
                {
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up
                }, Allocator.Temp))
                    Assert.That(builder.Rebuild(square, mesh), Is.True);
                using (var invalid = new NativeArray<Vector2>(new[] { Vector2.zero, Vector2.right }, Allocator.Temp))
                    Assert.That(builder.Rebuild(invalid, mesh), Is.False);
                Assert.That(mesh.triangles, Is.Empty);
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
