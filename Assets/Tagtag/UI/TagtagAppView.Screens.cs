using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private VisualElement explorePreview;
        private Label exploreLocationNotice;
        private Label exploreNativeMapNotice;
        private Button exploreNativeMapRetry;
        private Label exploreMapMessage;
        private Button exploreFindButton;
        private Button exploreRefreshButton;
        private Label exploreEmptyTitle;
        private readonly PresenterCache explorePreviewContents = new PresenterCache();

        private void BuildExplore(AppState state)
        {
            explorePreviewContents.Reset();
            VisualElement page = Column(screenHost);
            page.style.flexGrow = 1f;
            page.style.flexShrink = 1f;
            page.style.minHeight = 0f;
            page.style.backgroundColor = Paper;
            VisualElement heading = Row(page);
            heading.style.paddingLeft = 16f;
            heading.style.paddingRight = 20f;
            heading.style.flexShrink = 0f;
            heading.style.marginBottom = 8f;
            heading.style.alignItems = Align.Center;
            heading.style.justifyContent = Justify.SpaceBetween;
            HangingHeader(heading, "Explore", 28);
            Button recenter = Action(heading, "Recenter", () =>
            {
                if (controller.State.location != null) controller.Map?.Recenter(controller.State.location);
                else controller.RefreshNearby();
            }, false);
            recenter.style.marginLeft = 12f;
            recenter.style.flexShrink = 0f;
            SetDisabled(recenter, controller.Map == null);
            exploreLocationNotice = Text(page, "", 14, false, Muted);
            exploreLocationNotice.style.marginLeft = 24f;
            exploreLocationNotice.style.marginRight = 24f;
            exploreLocationNotice.style.marginBottom = 8f;
            VisualElement nativeMapStatus = Row(page);
            nativeMapStatus.name = "Explore native map status";
            nativeMapStatus.style.alignItems = Align.Center;
            nativeMapStatus.style.marginLeft = 24f;
            nativeMapStatus.style.marginRight = 24f;
            exploreNativeMapNotice = Text(nativeMapStatus, "", 14, false, Muted);
            exploreNativeMapNotice.name = "Explore native map notice";
            exploreNativeMapNotice.style.flexGrow = 1f;
            exploreNativeMapRetry = Action(nativeMapStatus, "Retry map", () =>
                (controller.Map as IMapLoadingExperience)?.Retry(), false);
            exploreNativeMapRetry.name = "Explore retry map";
            mapRegion = new VisualElement();
            mapRegion.name = "Native MapKit region";
            mapRegion.style.flexGrow = 1f;
            mapRegion.style.minHeight = 180f;
            mapRegion.style.backgroundColor = (Color)new Color32(236, 236, 229, 255);
            mapRegion.RegisterCallback<GeometryChangedEvent>(_ => { mapDirty = true; UpdateMapLayout(); });
            page.Add(mapRegion);
            exploreMapMessage = Text(mapRegion, "", 16, false, Muted);
            exploreMapMessage.style.marginTop = 24f;
            exploreMapMessage.style.marginLeft = 24f;
            ScrollView teaserScroll = PaperScroll(page);
            teaserScroll.name = "Explore teaser scroll";
            teaserScroll.style.flexGrow = 0f;
            teaserScroll.style.flexShrink = 1f;
            teaserScroll.style.maxHeight = Length.Percent(42f);
            teaserScroll.style.minHeight = 0f;
            explorePreview = Column(teaserScroll.contentContainer);
            explorePreview.style.backgroundColor = Paper;
            explorePreview.style.paddingLeft = 24f;
            explorePreview.style.paddingRight = 24f;
            explorePreview.style.paddingTop = 14f;
            explorePreview.style.paddingBottom = 14f;
            AddStatus(teaserScroll.contentContainer, state);
            RefreshExplore(state);
        }

        private void RefreshExplore(AppState state)
        {
            if (explorePreview == null) return;
            NearbyStatus nearbyStatus = PaperFlow.Nearby(state, controller.Map != null);
            exploreLocationNotice.text = nearbyStatus.LocationNotice;
            exploreLocationNotice.style.display = string.IsNullOrEmpty(exploreLocationNotice.text) ? DisplayStyle.None : DisplayStyle.Flex;
            IMapLoadingExperience mapLoading = controller.Map as IMapLoadingExperience;
            string mapError = mapLoading?.Error;
            exploreNativeMapNotice.text = !string.IsNullOrEmpty(mapError) ? mapError :
                mapLoading?.IsLoading == true ? "Loading map…" : "";
            exploreNativeMapNotice.style.color = string.IsNullOrEmpty(mapError) ? Muted : (Color)new Color32(125, 39, 31, 255);
            exploreNativeMapNotice.style.display = string.IsNullOrEmpty(exploreNativeMapNotice.text) ? DisplayStyle.None : DisplayStyle.Flex;
            exploreNativeMapRetry.style.display = string.IsNullOrEmpty(mapError) ? DisplayStyle.None : DisplayStyle.Flex;
            exploreMapMessage.text = nearbyStatus.MapMessage;
            exploreMapMessage.style.display = string.IsNullOrEmpty(exploreMapMessage.text) ? DisplayStyle.None : DisplayStyle.Flex;
            StickerSummary selected = state.selected;
            string key = selected == null ? "none:" + (state.nearby.Count == 0) : selected.id + ":" + selected.revision;
            if (explorePreviewContents.NeedsRefresh(key))
            {
                explorePreview.Clear();
                PaperDottedOutline.Decorate(explorePreview, container: true);
                if (selected == null)
                {
                    exploreEmptyTitle = Text(explorePreview, "", 19, true);
                    Text(explorePreview, state.nearby.Count == 0 ?
                        "Try another area or refresh nearby stickers." : "See its clue before you set out to find it.", 14, false, Muted).style.marginTop = 4f;
                    exploreRefreshButton = Action(explorePreview, "Refresh nearby", controller.RefreshNearby, false);
                    exploreRefreshButton.style.alignSelf = Align.FlexStart;
                    exploreFindButton = null;
                }
                else
                {
                    VisualElement titleRow = Row(explorePreview);
                    titleRow.style.alignItems = Align.Center;
                    Art(titleRow, selected, 68f);
                    VisualElement words = Column(titleRow);
                    words.style.flexGrow = 1f;
                    words.style.marginLeft = 12f;
                    Text(words, Safe(selected.place, "A place nearby"), 20, true);
                    Text(words, "Left by " + Safe(selected.authorName, "someone nearby"), 13, false, Muted);
                    Text(explorePreview, Safe(selected.teaser, "A sticker is waiting here."), 16).style.marginTop = 8f;
                    exploreFindButton = Action(explorePreview, "Find in AR", controller.StartDiscovery);
                    exploreRefreshButton = null;
                    exploreEmptyTitle = null;
                    exploreFindButton.style.marginTop = 10f;
                    VisualElement secondary = Row(explorePreview);
                    secondary.style.justifyContent = Justify.SpaceBetween;
                    SetDisabled(Action(secondary, "Report", null, false, true), true);
                    if (!string.IsNullOrEmpty(selected.authorId)) SetDisabled(Action(secondary, "Block author", null, false, true), true);
                }
            }
            if (exploreFindButton != null) SetDisabled(exploreFindButton, state.busy);
            if (exploreRefreshButton != null)
            {
                exploreRefreshButton.text = nearbyStatus.CanRefresh ? "Refresh nearby" : "Refreshing nearby…";
                SetDisabled(exploreRefreshButton, !nearbyStatus.CanRefresh);
            }
            if (exploreEmptyTitle != null) exploreEmptyTitle.text = nearbyStatus.EmptyTitle;
        }

        private void UpdateMapLayout()
        {
            mapDirty = false;
            if (controller?.Map == null || !MapPresentation.ShouldShow(controller.State,
                    sheet != Sheet.None || controller.State.creationOpen || CelebrationActive) ||
                mapRegion == null || root == null || root.layout.width <= 0f || root.layout.height <= 0f) return;
            Rect bounds = mapRegion.worldBound;
            float x = bounds.xMin / root.layout.width * Screen.width;
            float width = bounds.width / root.layout.width * Screen.width;
            float height = bounds.height / root.layout.height * Screen.height;
            float y = Screen.height - bounds.yMax / root.layout.height * Screen.height;
            Rect screenRect = new Rect(x, y, width, height);
            if (screenRect.width < 80f || screenRect.height < 80f) return;
            controller.Map.Show(screenRect, controller.State.location, MapPresentation.Pins(controller.State));
            MapPresentation.SyncTarget(controller.State, controller.Map);
        }

        private VisualElement stickActions;
        private VisualElement cameraSurface;
        private Label stickModeTitle;
        private Button stickInventoryButton;
        private Label stickBookLabel;
        private Button stickCloseButton;
        private Button stickRetryButton;
        private Button stickSelectedArtworkButton;
        private string stickSelectedArtworkKey;
        private bool stickSelectedArtworkPointerHeld;
        private PaperScanProgress stickScanProgress;
        private Label stickScanLabel;
        private Label stickScanRecovery;
        private VisualElement discoveryStatusHost;
        private readonly PaperSurfaceTap placementTap = new PaperSurfaceTap();
        private readonly PaperSurfaceGesture placementGesture = new PaperSurfaceGesture();
        private Label cameraTitleLabel;
        private Label cameraDetailLabel;
        private Button cameraRecoveryButton;

        private void BuildStick(AppState state)
        {
            VisualElement page = Column(screenHost);
            page.name = "STICK camera";
            page.style.flexGrow = 1f;
            page.style.minHeight = 0f;
            page.style.backgroundColor = Color.clear;
            cameraSurface = new VisualElement { name = "STICK Camera Surface" };
            cameraSurface.style.position = Position.Absolute;
            cameraSurface.style.left = 0f;
            cameraSurface.style.right = 0f;
            cameraSurface.style.top = 0f;
            cameraSurface.style.bottom = 0f;
            cameraSurface.RegisterCallback<GeometryChangedEvent>(_ => UpdateCameraInteraction());
            cameraSurface.RegisterCallback<PointerDownEvent>(OnCameraPointerDown);
            cameraSurface.RegisterCallback<PointerMoveEvent>(OnCameraPointerMove);
            cameraSurface.RegisterCallback<PointerUpEvent>(OnCameraPointerUp);
            cameraSurface.RegisterCallback<PointerCancelEvent>(OnCameraPointerCancel);
            cameraSurface.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                placementTap.CancelContact(evt.pointerId);
                placementGesture.CaptureOut(evt.pointerId);
            });
            page.Add(cameraSurface);
            cameraCover = Column(page);
            cameraCover.name = "Opaque camera cover";
            cameraCover.style.position = Position.Absolute;
            cameraCover.style.left = 0f;
            cameraCover.style.right = 0f;
            cameraCover.style.top = 0f;
            cameraCover.style.bottom = 0f;
            cameraCover.style.backgroundColor = Paper;
            ScrollView recoveryScroll = PaperScroll(cameraCover);
            recoveryScroll.name = "Camera recovery scroll";
            recoveryScroll.style.position = Position.Absolute;
            recoveryScroll.style.left = 0f;
            recoveryScroll.style.right = 0f;
            VisualElement recoveryContent = recoveryScroll.contentContainer;
            recoveryContent.style.alignItems = Align.Center;
            recoveryContent.style.paddingLeft = 30f;
            recoveryContent.style.paddingRight = 30f;
            recoveryContent.style.paddingTop = 16f;
            recoveryContent.style.paddingBottom = 16f;
            Image recoveryArt = Art(recoveryContent, "taggi-1", 72f);
            recoveryArt.style.flexShrink = 0f;
            recoveryArt.style.marginBottom = 18f;
            cameraTitleLabel = Text(recoveryContent, "Getting the camera ready", 23, true);
            cameraTitleLabel.style.flexShrink = 0f;
            cameraTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            cameraDetailLabel = Text(recoveryContent, "Hold your phone up while the camera starts.", 15, false, Muted);
            cameraDetailLabel.style.flexShrink = 0f;
            cameraDetailLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            cameraDetailLabel.style.marginTop = 7f;
            cameraRecoveryButton = Action(recoveryContent, "Try camera again", () =>
            {
                if (controller?.Ar?.CameraPresentation == CameraPresentationState.PermissionDenied)
                    Application.OpenURL("app-settings:");
                else controller?.Ar?.Enter();
            });
            cameraRecoveryButton.style.marginTop = 16f;
            VisualElement top = Row(page);
            top.name = "STICK camera header";
            top.style.position = Position.Absolute;
            top.style.left = 12f;
            top.style.right = 12f;
            top.style.top = 0f;
            top.style.alignItems = Align.Center;
            stickCloseButton = new PaperIconButton("Close camera", "close", CloseCamera);
            top.Add(stickCloseButton);
            stickCloseButton.name = "STICK Close";
            stickCloseButton.AddToClassList("camera-close");
            PaperDottedOutline.DecorateCircular(stickCloseButton);
            var titleSign = HangingHeader(top, "Place Sticker", 28);
            titleSign.name = "STICK title sticker";
            stickModeTitle = titleSign.Title;
            VisualElement dock = Column(page);
            dock.name = "STICK camera dock";
            dock.style.position = Position.Absolute;
            dock.style.left = 12f;
            dock.style.right = 12f;
            dock.style.bottom = 12f;
            dock.pickingMode = PickingMode.Ignore;
            // Keep recovery content scrollable between the independently sized overlays.
            void LayoutRecovery()
            {
                recoveryScroll.style.top = top.layout.yMax + 8f;
                recoveryScroll.style.bottom = Mathf.Max(0f, page.layout.height - dock.layout.yMin) + 8f;
            }
            top.RegisterCallback<GeometryChangedEvent>(_ => LayoutRecovery());
            dock.RegisterCallback<GeometryChangedEvent>(_ => LayoutRecovery());
            page.RegisterCallback<GeometryChangedEvent>(_ => LayoutRecovery());
            stickScanRecovery = Text(dock, "", 12, false, Muted);
            stickScanRecovery.name = "STICK scan recovery";
            stickScanRecovery.AddToClassList("camera-notice");
            stickScanRecovery.style.unityTextAlign = TextAnchor.MiddleCenter;
            stickScanRecovery.style.marginBottom = 6f;
            discoveryStatusHost = Column(dock);
            discoveryStatusHost.name = "Discovery status";
            discoveryStatusHost.AddToClassList("camera-notice");
            AddStatus(discoveryStatusHost, state);
            stickActions = Row(dock);
            stickActions.pickingMode = PickingMode.Ignore;
            stickActions.style.alignItems = Align.Center;
            stickActions.style.justifyContent = Justify.Center;
            stickActions.style.flexWrap = Wrap.Wrap;
            stickRetryButton = Action(stickActions, "Retry AR search", controller.StartDiscovery, false);
            stickRetryButton.name = "STICK Retry AR search";
            foreach (Button action in new[] { stickRetryButton })
            {
                action.style.flexGrow = 1f;
                action.style.flexBasis = Length.Percent(40f);
                action.style.minWidth = 0f;
                action.style.marginLeft = 3f;
                action.style.marginRight = 3f;
                action.style.marginBottom = 8f;
            }
            stickScanLabel = Text(dock, "", 12, true);
            stickScanLabel.name = "STICK scan label";
            stickScanLabel.AddToClassList("camera-notice");
            stickScanLabel.style.alignSelf = Align.Center;
            stickScanLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            stickScanLabel.style.marginBottom = 8f;
            VisualElement inventoryTarget = Column(dock);
            inventoryTarget.name = "STICK circular control";
            inventoryTarget.pickingMode = PickingMode.Ignore;
            inventoryTarget.style.alignSelf = Align.Center;
            inventoryTarget.style.alignItems = Align.Center;
            inventoryTarget.style.justifyContent = Justify.Center;
            inventoryTarget.style.width = 108f;
            inventoryTarget.style.height = 108f;
            inventoryTarget.style.flexShrink = 0f;
            stickScanProgress = new PaperScanProgress(Paper, Line, Yellow);
            inventoryTarget.Add(stickScanProgress);
            stickInventoryButton = Action(inventoryTarget, "STICK", () =>
            {
                if (!PaperFlow.HasPlacementSelection(controller.State)) controller.OpenCreation();
                else
                {
                    captureNoteRequested = true;
                    controller.CaptureSpot();
                }
            });
            stickInventoryButton.name = "STICK Inventory";
            stickInventoryButton.text = "";
            stickInventoryButton.AddToClassList("camera-book-button");
            PaperDottedOutline.DecorateCircular(stickInventoryButton);
            var bookArt = new Image
            {
                image = Resources.Load<Texture2D>("Tagtag/Navigation/stick-book"),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
                name = "Taggi holding sticker book"
            };
            bookArt.style.width = 52f;
            bookArt.style.height = 52f;
            bookArt.style.flexShrink = 0f;
            stickInventoryButton.Add(bookArt);
            stickBookLabel = Text(stickInventoryButton, "STICK", 12, false);
            stickBookLabel.pickingMode = PickingMode.Ignore;
            stickBookLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            stickInventoryButton.tooltip = "Open sticker inventory";
            stickInventoryButton.style.width = 88f;
            stickInventoryButton.style.height = 88f;
            stickInventoryButton.style.minWidth = 88f;
            stickInventoryButton.style.minHeight = 88f;
            stickInventoryButton.style.paddingLeft = 0f;
            stickInventoryButton.style.paddingRight = 0f;
            stickInventoryButton.style.borderTopLeftRadius = 44f;
            stickInventoryButton.style.borderTopRightRadius = 44f;
            stickInventoryButton.style.borderBottomLeftRadius = 44f;
            stickInventoryButton.style.borderBottomRightRadius = 44f;
            BuildRecoveryPreview(page, dock);
            stickSelectedArtworkButton = Action(page, "", () =>
            {
                if (controller.State.busy || controller.Ar?.PlacementBusy == true) return;
                controller.OpenCreation();
                if (!controller.State.creationOpen) return;
                sheet = Sheet.Picker;
                QueueRender();
            }, false);
            stickSelectedArtworkButton.name = "STICK selected artwork";
            ((PaperButton)stickSelectedArtworkButton).ShowDottedOutline = false;
            stickSelectedArtworkButton.tooltip = "Choose a different sticker";
            stickSelectedArtworkButton.style.position = Position.Absolute;
            stickSelectedArtworkButton.style.right = 16f;
            stickSelectedArtworkButton.style.width = 84f;
            stickSelectedArtworkButton.style.height = 84f;
            stickSelectedArtworkButton.style.minWidth = 84f;
            stickSelectedArtworkButton.style.minHeight = 84f;
            stickSelectedArtworkButton.style.paddingLeft = 4f;
            stickSelectedArtworkButton.style.paddingRight = 4f;
            stickSelectedArtworkButton.style.paddingTop = 4f;
            stickSelectedArtworkButton.style.paddingBottom = 4f;
            stickSelectedArtworkButton.style.backgroundColor = Paper;
            stickSelectedArtworkButton.RegisterCallback<PointerDownEvent>(_ =>
            {
                stickSelectedArtworkPointerHeld = true;
                CancelCameraPointers();
                UpdateCameraInteraction();
            });
            stickSelectedArtworkButton.RegisterCallback<PointerUpEvent>(_ =>
            {
                stickSelectedArtworkPointerHeld = false;
                UpdateCameraInteraction();
            });
            stickSelectedArtworkButton.RegisterCallback<PointerCancelEvent>(_ =>
            {
                stickSelectedArtworkPointerHeld = false;
                UpdateCameraInteraction();
            });
            var selectedArtworkButton = stickSelectedArtworkButton;
            void PositionSelectedArtwork()
            {
                if (selectedArtworkButton.panel == null) return;
                selectedArtworkButton.style.bottom = Mathf.Max(12f, page.layout.height - dock.layout.yMin + 12f);
            }
            page.RegisterCallback<GeometryChangedEvent>(_ => PositionSelectedArtwork());
            dock.RegisterCallback<GeometryChangedEvent>(_ => PositionSelectedArtwork());
            selectedArtworkButton.schedule.Execute(PositionSelectedArtwork);
            RefreshStick(state);
            RefreshCamera(state);
        }

        private void RefreshStick(AppState state)
        {
            if (stickActions == null) return;
            IArExperience ar = controller?.Ar;
            bool selected = PaperFlow.HasPlacementSelection(state);
            bool showCameraStatus = selected ? state.capturingSpot || !string.IsNullOrEmpty(state.error) :
                state.selected != null && (state.discoveryLoading || !string.IsNullOrEmpty(state.error) || state.locationSettingsRequired);
            discoveryStatusHost.style.display = showCameraStatus ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshRecoveryPreview(state);
            RefreshSelectedArtwork(state);
            PaperStickState placement = PaperFlow.StickPlacement(state, ar?.IsTracking ?? false,
                ar?.HasPlacementSurface ?? false, ar?.HasPlacementPreview ?? false,
                ar?.PlacementBusy ?? false);
            stickModeTitle.text = selected ? placement.Title : state.selected != null ? "Find sticker" : "Place Sticker";
            stickScanProgress.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
            stickScanLabel.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
            bool trackingPaused = selected && ar != null &&
                (ar.CameraPresentation == CameraPresentationState.Interrupted ||
                 ar.CameraPresentation == CameraPresentationState.Live && !ar.IsTracking);
            stickScanRecovery.text = trackingPaused ? "Tracking paused. Move slowly to resume." : "";
            if (!trackingPaused && selected && ar?.ScanState == PlacementScanState.Placed)
                stickScanRecovery.text = "Move slowly around your sticker. Scan the surface from several angles until Scan ready.";
            if (!selected && state.selected != null && ar != null)
                stickScanRecovery.text = ar.Status ?? "";
            stickScanRecovery.style.display = string.IsNullOrEmpty(stickScanRecovery.text) ? DisplayStyle.None : DisplayStyle.Flex;
            if (selected)
            {
                PlacementScanState scanState = ar?.ScanState ?? PlacementScanState.FindingSurface;
                stickScanProgress.SetStage(scanState);
                stickScanLabel.text = PaperScan.Label(scanState);
            }
            stickRetryButton.style.display = PaperFlow.ShowDiscoveryRetry(state) ? DisplayStyle.Flex : DisplayStyle.None;
            SetDisabled(stickRetryButton, state.busy);
            bool canCapture = state.hasPendingPublication || state.hasCapturedSpot ||
                ar?.ScanState == PlacementScanState.Ready && ar.CanPublish;
            SetDisabled(stickInventoryButton, state.busy || state.capturingSpot ||
                (ar?.PlacementBusy ?? false) || selected && !canCapture);
            stickInventoryButton.tooltip = selected ? "Capture this spot" : "Open sticker inventory";
            Image inventoryArt = stickInventoryButton.Q<Image>("Taggi holding sticker book");
            if (inventoryArt != null) inventoryArt.style.display = selected ? DisplayStyle.None : DisplayStyle.Flex;
            if (stickBookLabel != null)
            {
                stickBookLabel.style.fontSize = selected ? 21f : 12f;
                stickBookLabel.style.unityFont = selected ? SemiboldFont : BodyFont;
            }
            SetDisabled(stickCloseButton, state.busy && !state.discoveryLoading);
            UpdateCameraInteraction();
        }

        private void RefreshSelectedArtwork(AppState state)
        {
            if (stickSelectedArtworkButton == null) return;
            bool selected = PaperFlow.HasPlacementSelection(state);
            bool visible = selected && state.page == AppPage.Stick && !state.accountOpen &&
                sheet == Sheet.None && !state.creationOpen &&
                controller.Ar?.CameraPresentation == CameraPresentationState.Live;
            stickSelectedArtworkButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            SetDisabled(stickSelectedArtworkButton, state.busy || controller.Ar?.PlacementBusy == true);
            string key = !string.IsNullOrEmpty(state.selectedDesign) ? "design:" + state.selectedDesign : "preset:" + state.selectedPreset;
            if (key == stickSelectedArtworkKey) return;
            stickSelectedArtworkKey = key;
            stickSelectedArtworkButton.Clear();
            if (!selected) return;
            Image artwork;
            StickerDesign design = state.designs?.Find(item => item != null && item.id == state.selectedDesign);
            if (design != null) artwork = Art(stickSelectedArtworkButton, design, 72f);
            else artwork = Art(stickSelectedArtworkButton, state.selectedPreset, state.selectedDesign,
                null, null, 72f, true, 0, 0);
            artwork.name = "STICK selected artwork image";
            artwork.pickingMode = PickingMode.Ignore;
        }

        private void RefreshCamera(AppState state)
        {
            if (root == null) return;
            CameraPresentationState presentation = controller?.Ar?.CameraPresentation ?? CameraPresentationState.Unavailable;
            CameraMessage message = PaperFlow.Camera(presentation);
            bool stickVisible = state.page == AppPage.Stick && !state.accountOpen;
            root.style.backgroundColor = stickVisible && !message.Cover ? Color.clear : Paper;
            statusBarBacking.style.backgroundColor = stickVisible ? Color.clear : Paper;
            if (cameraCover == null) return;
            cameraCover.style.display = message.Cover ? DisplayStyle.Flex : DisplayStyle.None;
            cameraTitleLabel.text = message.Title;
            cameraDetailLabel.text = message.Detail;
            cameraRecoveryButton.style.display = message.Recovery == CameraRecovery.None ? DisplayStyle.None : DisplayStyle.Flex;
            cameraRecoveryButton.text = message.Recovery == CameraRecovery.Settings ? "Open Settings" : "Try camera again";
        }

        private void RefreshPublish(AppState state)
        {
            if (publishButton != null)
            {
                publishButton.text = state.busy ? (publicationRequested ? "Publishing…" : "Please wait") :
                    (state.hasPendingPublication ? "Retry publish" : "Publish sticker");
                SetDisabled(publishButton, !PaperFlow.CanPresentPublish(draftPlace, draftTeaser, draftNote,
                    state.hasCapturedSpot, state.busy, state.hasPendingPublication));
            }
        }

        private PaperField DraftField(VisualElement parent, string label, string value, int maxLength,
            bool multiline, Action<string> save, string helper)
        {
            PaperField field = new PaperField(label, value, maxLength, multiline, helper, true);
            field.name = label;
            field.tooltip = label;
            field.style.fontSize = Mathf.RoundToInt(16f * textScale);
            field.style.unityFont = BodyFont;
            parent.Add(field);
            field.RegisterCallback<FocusInEvent>(_ =>
            {
                focusedField = field;
                lastDraftFieldName = field.name;
            });
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                lastDraftFieldName = field.name;
                lastDraftCursor = field.cursorIndex;
                lastDraftSelection = field.selectIndex;
                if (focusedField == field) focusedField = null;
                field.PresentError(string.IsNullOrWhiteSpace(field.value) ? "Add " + label.ToLowerInvariant() + " before publishing." : "");
            });
            field.RegisterValueChangedCallback(evt =>
            {
                save(evt.newValue);
                if (!string.IsNullOrWhiteSpace(evt.newValue)) field.PresentError("");
                controller.SetDraft(draftPlace, draftTeaser, draftNote);
                RefreshPublish(controller.State);
            });
            return field;
        }

    }
}
