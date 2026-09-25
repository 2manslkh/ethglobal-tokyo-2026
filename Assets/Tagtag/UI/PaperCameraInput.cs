using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tagtag.UI
{
    // Adapted from source SurfaceTap: a drag, long press, or second contact cannot place art.
    public sealed class PaperSurfaceTap
    {
        private readonly HashSet<int> contacts = new HashSet<int>();
        private int primary = -1;
        private Vector2 start;
        private float began;
        private bool suppressed;
        public void Begin(int pointerId, Vector2 point, float time)
        {
            if (!contacts.Add(pointerId)) return;
            if (contacts.Count > 1) { suppressed = true; return; }
            primary = pointerId;
            start = point;
            began = time;
            suppressed = false;
        }
        public void Move(int pointerId, Vector2 point)
        {
            if (pointerId == primary && Vector2.Distance(start, point) > 12f) suppressed = true;
        }
        public bool End(int pointerId, Vector2 point, float time)
        {
            if (!contacts.Contains(pointerId)) return false;
            Move(pointerId, point);
            bool tapped = pointerId == primary && !suppressed && contacts.Count == 1 &&
                time >= began && time - began <= .5f;
            contacts.Remove(pointerId);
            if (pointerId == primary) primary = -1;
            if (contacts.Count == 0) suppressed = false;
            return tapped;
        }
        public void Cancel() { suppressed = true; }
        public void CancelContact(int pointerId)
        {
            suppressed = true;
            contacts.Remove(pointerId);
            if (pointerId == primary) primary = -1;
            if (contacts.Count == 0) suppressed = false;
        }
    }

    // Adapted from source SurfaceGesture and PlacementGestures. Contact ownership stays on the surface.
    public sealed class PaperSurfaceGesture
    {
        private readonly SortedDictionary<int, Vector2> contacts = new SortedDictionary<int, Vector2>();
        public int Count => contacts.Count;
        public bool MovesPlacement => contacts.Count == 1;
        public bool Begin(int pointerId, Vector2 point)
        {
            if (!Finite(point) || contacts.ContainsKey(pointerId) || contacts.Count == 2) return false;
            contacts.Add(pointerId, point);
            return true;
        }
        public bool Move(int pointerId, Vector2 point, ref float widthCm, ref float rotation,
            out Vector2 centroid)
        {
            centroid = default;
            if (!contacts.ContainsKey(pointerId) || !Finite(point)) return false;
            Vector2[] before = contacts.Values.ToArray();
            contacts[pointerId] = point;
            Vector2[] after = contacts.Values.ToArray();
            centroid = after.Aggregate(Vector2.zero, (sum, item) => sum + item) / after.Length;
            if (after.Length == 2)
            {
                Vector2 from = before[1] - before[0];
                Vector2 to = after[1] - after[0];
                if (from.magnitude >= 2f && to.magnitude >= 2f)
                {
                    widthCm = Mathf.Clamp(widthCm * to.magnitude / from.magnitude, 10f, 50f);
                    rotation = Wrap(rotation + Vector2.SignedAngle(from, to));
                }
            }
            return true;
        }
        public bool End(int pointerId) => contacts.Remove(pointerId);
        // Capture-out after PointerUp finds no contact. An unexpected loss drops
        // the complete gesture so a remaining finger cannot turn into a drag.
        public void CaptureOut(int pointerId)
        {
            if (contacts.ContainsKey(pointerId)) Cancel();
        }
        public void Cancel() => contacts.Clear();
        public static float Wrap(float degrees) => Mathf.Repeat(degrees + 180f, 360f) - 180f;
        private static bool Finite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }

    public static class PaperCameraBounds
    {
        public static Vector2 ToScreenPoint(Vector2 point, Rect panelBounds, int screenWidth,
            int screenHeight)
        {
            if (panelBounds.width <= 0f || panelBounds.height <= 0f) return default;
            return new Vector2((point.x - panelBounds.xMin) * screenWidth / panelBounds.width,
                screenHeight - (point.y - panelBounds.yMin) * screenHeight / panelBounds.height);
        }
        public static Rect ToScreenRect(Rect cameraPanel, Rect panelBounds, int screenWidth,
            int screenHeight)
        {
            if (panelBounds.width <= 0f || panelBounds.height <= 0f) return default;
            return new Rect((cameraPanel.xMin - panelBounds.xMin) * screenWidth / panelBounds.width,
                screenHeight - (cameraPanel.yMax - panelBounds.yMin) * screenHeight / panelBounds.height,
                cameraPanel.width * screenWidth / panelBounds.width,
                cameraPanel.height * screenHeight / panelBounds.height);
        }
    }
}
