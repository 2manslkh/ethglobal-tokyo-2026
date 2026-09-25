using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private void CloseCamera()
        {
            if (controller == null || controller.State.busy) return;
            CancelCameraPointers();
            controller.Navigate(cameraReturnPage == AppPage.Explore ? AppPage.Explore : AppPage.Home);
        }

        private void AddAdjustment(VisualElement row, string label, float widthDeltaCm, float rotationDelta)
        {
            Button button = Action(row, label, () =>
            {
                IArExperience ar = controller?.Ar;
                if (!CanAdjustCamera(ar)) return;
                float width = Mathf.Clamp(ar.PlacementWidthMeters * 100f + widthDeltaCm, 10f, 50f) / 100f;
                float rotation = PaperSurfaceGesture.Wrap(ar.PlacementRotationDegrees + rotationDelta);
                ar.AdjustPlacement(width, rotation);
            }, false);
            button.name = "STICK " + label;
            button.style.flexGrow = 1f;
            button.style.flexBasis = 0f;
            button.style.minWidth = 0f;
        }

        private bool CameraInputReady(IArExperience ar)
        {
            if (ar == null || controller == null) return false;
            return !PaperFlow.BlockCameraInteraction(controller.State, sheet != Sheet.None,
                       ar.CameraPresentation, ar.IsTracking) && !ar.PlacementBusy && cameraSurface != null &&
                   cameraSurface.worldBound.width > 0f && cameraSurface.worldBound.height > 0f;
        }

        private bool CanAdjustCamera(IArExperience ar)
        {
            return CameraInputReady(ar) && ar.IsTracking && ar.HasPlacementPreview &&
                !string.IsNullOrEmpty(controller.State.selectedPreset);
        }

        private void UpdateCameraInteraction()
        {
            IArExperience ar = controller?.Ar;
            if (ar == null) return;
            Rect cameraRect = cameraSurface != null && root != null ?
                PaperCameraBounds.ToScreenRect(cameraSurface.worldBound, root.worldBound,
                    Screen.width, Screen.height) : default;
            bool blocked = !CameraInputReady(ar) || cameraRect.width <= 0f || cameraRect.height <= 0f;
            if (blocked) CancelCameraPointers();
            ar.SetCameraInteraction(cameraRect, blocked);
        }

        private void OnCameraPointerDown(PointerDownEvent evt)
        {
            if (evt.target != cameraSurface || evt.button != 0) return;
            IArExperience ar = controller?.Ar;
            if (!CameraInputReady(ar) || !ar.IsTracking) return;
            if (ar.HasPlacementPreview)
            {
                if (!CanAdjustCamera(ar) || !placementGesture.Begin(evt.pointerId, evt.position)) return;
            }
            else
            {
                if (string.IsNullOrEmpty(controller.State.selectedPreset)) return;
                placementTap.Begin(evt.pointerId, evt.position, Time.unscaledTime);
                if (!ar.HasPlacementSurface) placementTap.Cancel();
            }
            cameraSurface.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnCameraPointerMove(PointerMoveEvent evt)
        {
            placementTap.Move(evt.pointerId, evt.position);
            IArExperience ar = controller?.Ar;
            if (!CanAdjustCamera(ar))
            {
                placementGesture.Cancel();
                return;
            }
            float widthCm = ar.PlacementWidthMeters * 100f;
            float rotation = ar.PlacementRotationDegrees;
            if (!placementGesture.Move(evt.pointerId, evt.position, ref widthCm, ref rotation,
                    out Vector2 centroid)) return;
            Vector2? movePoint = placementGesture.MovesPlacement ?
                PaperCameraBounds.ToScreenPoint(centroid, root.worldBound, Screen.width, Screen.height) :
                (Vector2?)null;
            ar.AdjustPlacement(widthCm / 100f, rotation, movePoint);
            evt.StopPropagation();
        }

        private void OnCameraPointerUp(PointerUpEvent evt)
        {
            bool tapped = placementTap.End(evt.pointerId, evt.position, Time.unscaledTime);
            bool adjusted = placementGesture.End(evt.pointerId);
            if (cameraSurface.HasPointerCapture(evt.pointerId))
            {
                cameraSurface.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
            else if (adjusted) evt.StopPropagation();
            IArExperience ar = controller?.Ar;
            if (!tapped || !CameraInputReady(ar) || !ar.IsTracking || !ar.HasPlacementSurface ||
                ar.HasPlacementPreview || string.IsNullOrEmpty(controller.State.selectedPreset) ||
                !cameraSurface.worldBound.Contains(evt.position)) return;
            ar.Place(PaperCameraBounds.ToScreenPoint(evt.position, root.worldBound,
                Screen.width, Screen.height));
        }

        private void OnCameraPointerCancel(PointerCancelEvent evt)
        {
            placementTap.CancelContact(evt.pointerId);
            placementGesture.Cancel();
            if (cameraSurface.HasPointerCapture(evt.pointerId)) cameraSurface.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void CancelCameraPointers()
        {
            placementTap.Cancel();
            placementGesture.Cancel();
        }
    }
}
