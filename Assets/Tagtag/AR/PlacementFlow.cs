using UnityEngine;

namespace Tagtag.AR
{
    public sealed class PlacementFlow
    {
        private Rect cameraScreenRect;
        private bool blocked = true;

        public bool IsBlocked => blocked || cameraScreenRect.width <= 0f || cameraScreenRect.height <= 0f;

        public bool SetInteraction(Rect cameraScreenRect, bool blocked)
        {
            if (this.cameraScreenRect == cameraScreenRect && this.blocked == blocked) return false;
            this.cameraScreenRect = cameraScreenRect;
            this.blocked = blocked;
            return true;
        }

        public bool Allows(Vector2 point)
        {
            return !IsBlocked && cameraScreenRect.Contains(point);
        }

        public static bool ShouldOutline(bool selected, bool tracking, bool busy, bool hasPreview, bool blocked)
        {
            return selected && tracking && !busy && !hasPreview && !blocked;
        }

        public static PlacementScanState ScanState(bool tracking, bool surface, bool hasPlacement,
            bool anchorTracking, bool mapReady)
        {
            if (!tracking || (hasPlacement && !anchorTracking)) return PlacementScanState.FindingSurface;
            if (hasPlacement) return mapReady ? PlacementScanState.Ready : PlacementScanState.Placed;
            return surface ? PlacementScanState.SurfaceReady : PlacementScanState.FindingSurface;
        }

        public static bool TryMoveOnOriginalPlane(Pose original, Pose hit, out Vector3 position)
        {
            position = default;
            var normal = original.rotation * Vector3.up;
            if (Vector3.Angle(normal, hit.rotation * Vector3.up) >= 10f ||
                Mathf.Abs(Vector3.Dot(hit.position - original.position, normal)) >= 0.03f) return false;
            position = hit.position + normal * 0.002f;
            return true;
        }
    }
}
