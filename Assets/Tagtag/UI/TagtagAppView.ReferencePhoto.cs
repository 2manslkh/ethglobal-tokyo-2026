using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private Button recoveryPhotoButton;
        private Image recoveryPhotoImage;
        private Label recoveryPhotoPlaceholder;
        private Image referencePhotoFull;

        private void BuildRecoveryPreview(VisualElement page, VisualElement dock)
        {
            recoveryPhotoButton = Action(page, "", () =>
            {
                if (!(controller.Ar is IReferencePhotoAr photos) || photos.ReferencePhoto == null) return;
                sheetStickerId = controller.State.selected?.id;
                sheet = Sheet.ReferencePhoto;
                QueueRender();
            }, false);
            recoveryPhotoButton.name = "Original spot preview";
            recoveryPhotoButton.tooltip = "Enlarge photo of the original spot";
            recoveryPhotoButton.style.position = Position.Absolute;
            recoveryPhotoButton.style.right = 12f;
            recoveryPhotoButton.style.width = 112f;
            recoveryPhotoButton.style.minWidth = 0f;
            recoveryPhotoButton.style.paddingLeft = 6f;
            recoveryPhotoButton.style.paddingRight = 6f;
            recoveryPhotoButton.style.paddingTop = 6f;
            recoveryPhotoButton.style.paddingBottom = 6f;
            recoveryPhotoButton.style.backgroundColor = Paper;
            recoveryPhotoButton.style.flexDirection = FlexDirection.Column;
            recoveryPhotoImage = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            recoveryPhotoImage.style.height = 92f;
            recoveryPhotoImage.style.width = Length.Percent(100);
            recoveryPhotoButton.Add(recoveryPhotoImage);
            recoveryPhotoPlaceholder = Text(recoveryPhotoButton, "Loading photo…", 12, false, Muted);
            recoveryPhotoPlaceholder.style.whiteSpace = WhiteSpace.Normal;
            recoveryPhotoPlaceholder.pickingMode = PickingMode.Ignore;
            var caption = Text(recoveryPhotoButton, "Original spot", 12, true);
            caption.pickingMode = PickingMode.Ignore;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            var current = recoveryPhotoButton;
            void PositionPreview()
            {
                if (current.panel == null) return;
                current.style.bottom = Mathf.Max(12f, page.layout.height - dock.layout.yMin + 10f);
            }
            page.RegisterCallback<GeometryChangedEvent>(_ => PositionPreview());
            dock.RegisterCallback<GeometryChangedEvent>(_ => PositionPreview());
            current.schedule.Execute(PositionPreview);
        }

        private void RefreshRecoveryPreview(AppState state)
        {
            if (recoveryPhotoButton == null) return;
            bool discovering = state.page == AppPage.Stick && state.selected != null &&
                !PaperFlow.HasPlacementSelection(state) && !state.accountOpen && sheet == Sheet.None &&
                controller.Ar.CameraPresentation == CameraPresentationState.Live;
            recoveryPhotoButton.style.display = discovering ? DisplayStyle.Flex : DisplayStyle.None;
            var photos = controller.Ar as IReferencePhotoAr;
            var photo = photos?.ReferencePhoto;
            bool ready = photos?.PhotoState == ReferencePhotoState.Ready && photo != null;
            recoveryPhotoImage.image = ready ? photo : null;
            recoveryPhotoImage.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            recoveryPhotoPlaceholder.text = photos?.PhotoState == ReferencePhotoState.Loading ? "Loading photo…" : "No reference photo";
            recoveryPhotoPlaceholder.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            SetDisabled(recoveryPhotoButton, !ready);
            if (referencePhotoFull != null && referencePhotoFull.panel != null)
                referencePhotoFull.image = ready ? photo : null;
        }

        private void BuildReferencePhotoSheet(VisualElement content)
        {
            referencePhotoFull = new Image
            {
                image = (controller.Ar as IReferencePhotoAr)?.ReferencePhoto,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
                name = "Original spot full photo"
            };
            referencePhotoFull.style.height = Mathf.Clamp(root.layout.height * .5f, 180f, 440f);
            referencePhotoFull.style.flexShrink = 1f;
            content.Add(referencePhotoFull);
            Text(content, "Saved photo · match these surroundings to find the sticker.", 14, false, Muted);
        }
    }
}
