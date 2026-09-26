using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Tagtag.AR
{
    // ARPlane boundaries can be concave and arrive in either winding. Scratch lists survive each update.
    public sealed class PlaneHatchMesh
    {
        private const float Epsilon = 0.000001f;
        private readonly List<Vector2> points = new List<Vector2>(32);
        private readonly List<Vector3> vertices = new List<Vector3>(32);
        private readonly List<int> remaining = new List<int>(32);
        private readonly List<int> triangles = new List<int>(90);

        public bool Rebuild(NativeArray<Vector2> boundary, Mesh mesh)
        {
            points.Clear();
            vertices.Clear();
            remaining.Clear();
            triangles.Clear();
            for (var i = 0; i < boundary.Length; i++)
            {
                var point = boundary[i];
                if (points.Count == 0 || (point - points[points.Count - 1]).sqrMagnitude > Epsilon * Epsilon)
                    points.Add(point);
            }
            if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude <= Epsilon * Epsilon)
                points.RemoveAt(points.Count - 1);
            for (var i = 0; points.Count >= 3 && i < points.Count;)
            {
                var previous = points[(i + points.Count - 1) % points.Count];
                var current = points[i];
                var next = points[(i + 1) % points.Count];
                if (Mathf.Abs(Cross(previous, current, next)) <= Epsilon)
                {
                    points.RemoveAt(i);
                    if (i > 0) i--;
                }
                else i++;
            }
            mesh.Clear();
            if (points.Count < 3) return false;

            var signedArea = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var next = points[(i + 1) % points.Count];
                signedArea += point.x * next.y - next.x * point.y;
                vertices.Add(new Vector3(point.x, 0.003f, point.y));
                remaining.Add(i);
            }
            if (Mathf.Abs(signedArea) <= Epsilon) return false;
            var winding = signedArea > 0f ? 1f : -1f;
            while (remaining.Count > 3)
            {
                var foundEar = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var before = remaining[(i + remaining.Count - 1) % remaining.Count];
                    var at = remaining[i];
                    var after = remaining[(i + 1) % remaining.Count];
                    if (Cross(points[before], points[at], points[after]) * winding <= Epsilon) continue;
                    var containsPoint = false;
                    for (var j = 0; j < remaining.Count; j++)
                    {
                        var other = remaining[j];
                        if (other == before || other == at || other == after) continue;
                        if (!InsideTriangle(points[other], points[before], points[at], points[after], winding)) continue;
                        containsPoint = true;
                        break;
                    }
                    if (containsPoint) continue;
                    triangles.Add(before);
                    triangles.Add(at);
                    triangles.Add(after);
                    remaining.RemoveAt(i);
                    foundEar = true;
                    break;
                }
                if (!foundEar) return false;
            }
            triangles.Add(remaining[0]);
            triangles.Add(remaining[1]);
            triangles.Add(remaining[2]);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static bool InsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c, float winding) =>
            Cross(a, b, point) * winding >= -Epsilon &&
            Cross(b, c, point) * winding >= -Epsilon &&
            Cross(c, a, point) * winding >= -Epsilon;
    }
}
