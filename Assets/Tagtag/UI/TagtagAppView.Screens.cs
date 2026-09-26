using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private VisualElement homeBook;
        private VisualElement homeFooter;
        private VisualElement homeInvitation;
        private PaperCollectionCount homeCount;
        private Button homeCreateButton;
        private readonly PresenterCache homeContents = new PresenterCache();
        private List<CollectedSticker> homeItems = new List<CollectedSticker>();

        private void BuildHome(AppState state)
        {
            homeContents.Reset();
            ScrollView scroll = PaperScroll(screenHost);
            scroll.name = "Home scroll";
            VisualElement page = Column(scroll.contentContainer);
            page.style.paddingLeft = 20f;
            page.style.paddingRight = 20f;
            page.style.paddingBottom = 22f;
            VisualElement heading = Row(page);
            heading.style.alignItems = Align.Center;
            heading.style.marginTop = 16f;
            Label title = Text(heading, "Your sticker book", 30, true);
            title.style.flexGrow = 1f;
            title.style.minWidth = 0f;
            homeProfileButton = new PaperIconButton(SignedIn(state) ? "Account settings" : "Sign in", "profile", () =>
            {
                accountScreen = SignedIn(controller.State) ? AccountScreen.Overview : AccountScreen.SignIn;
                controller.SetAccountOpen(true);
            });
            homeProfileButton.name = "Home Profile";
            homeProfileButton.style.marginLeft = 8f;
            heading.Add(homeProfileButton);
            title.style.marginBottom = 3f;
            homeCreateButton = Action(page, "Make a sticker", controller.OpenCreation, false);
            homeCreateButton.name = "Home Make sticker";
            homeCreateButton.style.alignSelf = Align.FlexStart;
            homeCreateButton.style.marginBottom = 8f;
            homeCount = new PaperCollectionCount();
            homeCount.style.flexDirection = FlexDirection.Row;
            homeCount.style.alignItems = Align.Center;
            page.Add(homeCount);
            homeCount.Number.userData = 24;
            homeCount.Number.style.unityFont = SemiboldFont;
            homeCount.Number.style.unityFontStyleAndWeight = FontStyle.Normal;
            homeCount.Number.style.fontSize = Mathf.RoundToInt(24f * textScale);
            homeCount.Number.style.color = Ink;
            homeCount.Number.style.flexShrink = 0f;
            homeCount.Caption.userData = 14;
            homeCount.Caption.style.unityFont = BodyFont;
            homeCount.Caption.style.unityFontStyleAndWeight = FontStyle.Normal;
            homeCount.Caption.style.fontSize = Mathf.RoundToInt(14f * textScale);
            homeCount.Caption.style.color = Muted;
            homeCount.Caption.style.marginLeft = 8f;
            homeCount.style.marginBottom = 14f;
            homeBook = Column(page);
            homeBook.name = "Sticker book page";
            homeBook.style.minHeight = textScale > 1.2f ? 490f : 390f;
            homeBook.style.backgroundColor = Soft;
            homeBook.style.borderTopLeftRadius = 18f;
            homeBook.style.borderTopRightRadius = 18f;
            homeBook.style.borderBottomLeftRadius = 18f;
            homeBook.style.borderBottomRightRadius = 18f;
            homeBook.style.paddingTop = 8f;
            homeBook.style.paddingBottom = 8f;
            homeBook.style.paddingLeft = 8f;
            homeBook.style.paddingRight = 8f;
            homeBook.RegisterCallback<PointerDownEvent>(evt => OnBookPointerDown(homeBook, evt));
            homeBook.RegisterCallback<PointerUpEvent>(evt => OnBookPointerUp(homeBook, evt, homeItems.Count));
            homeBook.RegisterCallback<PointerCancelEvent>(_ => { bookPressed = false; pressedStickerId = null; });
            homeFooter = Row(page);
            homeFooter.style.minHeight = 62f;
            homeFooter.style.alignItems = Align.Center;
            homeFooter.style.justifyContent = Justify.SpaceBetween;
            RefreshHome(state);
            AddStatus(page, state);
        }

        private void RefreshHome(AppState state)
        {
            if (homeBook == null) return;
            if (homeCreateButton != null) SetDisabled(homeCreateButton, state.busy);
            List<CollectedSticker> items = CollectionPresentation.OrderedDistinct(state.collection);
            int page = BookPaging.ClampPage(bookPage, items.Count);
            string key = page + ":" + items.Count + ":" + state.user?.uid;
            foreach (var item in items) key += ":" + item.id + ":" + item.revision + ":" +
                item.unavailable + ":" + item.designId + ":" + item.thumbnailUrl;
            if (!homeContents.NeedsRefresh(key)) return;
            bookPage = page;
            homeItems = items;
            homeCount.SetCount(items.Count);
            homeBook.Clear();
            for (int rowIndex = 0; rowIndex < BookPaging.Rows; rowIndex++)
            {
                VisualElement row = Row(homeBook);
                row.style.flexGrow = 1f;
                row.style.minHeight = textScale > 1.2f ? 86f : 68f;
                for (int columnIndex = 0; columnIndex < BookPaging.Columns; columnIndex++)
                {
                    int slot = rowIndex * BookPaging.Columns + columnIndex;
                    int index = BookPaging.IndexAt(bookPage, slot, items.Count);
                    VisualElement cell = Column(row);
                    cell.style.flexGrow = 1f;
                    cell.style.width = Length.Percent(25);
                    cell.style.minWidth = 0f;
                    cell.style.marginLeft = 3f;
                    cell.style.marginRight = 3f;
                    cell.style.marginTop = 3f;
                    cell.style.marginBottom = 3f;
                    cell.style.alignItems = Align.Center;
                    cell.style.justifyContent = Justify.Center;
                    if (index < 0) continue;
                    CollectedSticker item = items[index];
                    cell.userData = item.id;
                    cell.tooltip = "Open " + Safe(item.place, "collected sticker") + " details";
                    cell.focusable = true;
                    cell.tabIndex = 0;
                    string id = item.id;
                    cell.RegisterCallback<KeyDownEvent>(evt =>
                    {
                        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
                        OpenCollected(id); evt.StopPropagation();
                    });
                    Image image = Art(cell, item, textScale > 1.2f ? 78f : 67f);
                    image.style.maxWidth = Length.Percent(95);
                    image.style.maxHeight = Length.Percent(92);
                    image.style.opacity = item.unavailable ? .4f : 1f;
                }
            }
            if (items.Count == 0)
            {
                homeInvitation = Column(homeBook);
                PaperDottedOutline.Decorate(homeInvitation, container: true);
                homeInvitation.style.position = Position.Absolute;
                homeInvitation.style.left = 12f;
                homeInvitation.style.right = 12f;
                homeInvitation.style.top = 86f;
                homeInvitation.style.bottom = 86f;
                homeInvitation.style.alignItems = Align.Center;
                homeInvitation.style.justifyContent = Justify.Center;
                Art(homeInvitation, "taggi-1", 72f);
                Label invitation = Text(homeInvitation, PaperFlow.EmptyBookInvitation, 18, true);
                invitation.style.unityTextAlign = TextAnchor.MiddleCenter;
                invitation.style.marginTop = 6f;
                Action(homeInvitation, "Explore nearby", () => controller.Navigate(AppPage.Explore)).style.marginTop = 8f;
            }
            else homeInvitation = null;
            homeFooter.Clear();
            Button previous = Action(homeFooter, "Previous", () => ChangeBookPage(bookPage - 1, homeItems.Count), false);
            SetDisabled(previous, bookPage == 0);
            Text(homeFooter, "Page " + (bookPage + 1) + " of " + BookPaging.PageCount(items.Count), 13, false, Muted);
            Button next = Action(homeFooter, "Next", () => ChangeBookPage(bookPage + 1, homeItems.Count), false);
            SetDisabled(next, bookPage + 1 >= BookPaging.PageCount(items.Count));
        }

        private void OpenCollected(string id)
        {
            sheet = Sheet.Collected;
            sheetStickerId = id;
            controller.OpenCollected(id);
            QueueRender();
        }

        private void FocusCollectedCell(string id)
        {
            if (homeBook == null || string.IsNullOrEmpty(id)) return;
            int index = homeItems.FindIndex(item => item.id == id);
            if (index < 0) return;
            int page = index / BookPaging.PageSize;
            if (page != bookPage)
            {
                bookPage = page;
                RefreshHome(controller.State);
            }
            VisualElement currentCell = null;
            homeBook.Query<VisualElement>().ForEach(element =>
            {
                if (currentCell == null && element.userData is string stickerId && stickerId == id)
                    currentCell = element;
            });
            if (currentCell != null && currentCell.panel != null) currentCell.Focus();
        }

        private void OnBookPointerDown(VisualElement book, PointerDownEvent evt)
        {
            bookPressed = true;
            bookStart = evt.position;
            pressedStickerId = null;
            VisualElement target = evt.target as VisualElement;
            while (target != null && target != book)
            {
                if (target.userData is string stickerId)
                {
                    pressedStickerId = stickerId;
                    break;
                }
                target = target.parent;
            }
            book.CapturePointer(evt.pointerId);
        }

        private void OnBookPointerUp(VisualElement book, PointerUpEvent evt, int count)
        {
            if (!bookPressed) return;
            bookPressed = false;
            if (book.HasPointerCapture(evt.pointerId)) book.ReleasePointer(evt.pointerId);
            float deltaX = evt.position.x - bookStart.x;
            float deltaY = evt.position.y - bookStart.y;
            int nextPage = BookPaging.PageAfterSwipe(bookPage, count, deltaX, deltaY);
            if (nextPage != bookPage)
            {
                ChangeBookPage(nextPage, count);
            }
            else if (BookPaging.IsTap(deltaX, deltaY) && !string.IsNullOrEmpty(pressedStickerId))
            {
                OpenCollected(pressedStickerId);
            }
            pressedStickerId = null;
        }

        private void ChangeBookPage(int page, int itemCount)
        {
            int clamped = BookPaging.ClampPage(page, itemCount);
            if (clamped == bookPage) return;
            bookPage = clamped;
            RefreshHome(controller.State);
        }

        private VisualElement explorePreview;
        private Label exploreLocationNotice;
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
            heading.style.paddingLeft = 24f;
            heading.style.paddingRight = 20f;
            heading.style.marginTop = 12f;
            heading.style.marginBottom = 8f;
            heading.style.alignItems = Align.Center;
            heading.style.justifyContent = Justify.SpaceBetween;
            Text(heading, "Explore", 28, true);
            Button recenter = Action(heading, "Recenter", () =>
            {
                if (controller.State.location != null) controller.Map?.Recenter(controller.State.location);
                else controller.RefreshNearby();
            }, false);
            SetDisabled(recenter, controller.Map == null);
            exploreLocationNotice = Text(page, "", 14, false, Muted);
            exploreLocationNotice.style.marginLeft = 24f;
            exploreLocationNotice.style.marginRight = 24f;
            exploreLocationNotice.style.marginBottom = 8f;
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
                    Action(secondary, "Report", () => OpenReport(selected.id), false);
                    if (!string.IsNullOrEmpty(selected.authorId)) Action(secondary, "Block author", () => OpenBlock(selected.authorId), false);
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
                    sheet != Sheet.None || controller.State.creationOpen) ||
                controller.State.location == null || mapRegion == null || root == null || root.layout.width <= 0f || root.layout.height <= 0f) return;
            Rect bounds = mapRegion.worldBound;
            float x = bounds.xMin / root.layout.width * Screen.width;
            float width = bounds.width / root.layout.width * Screen.width;
            float height = bounds.height / root.layout.height * Screen.height;
            float y = Screen.height - bounds.yMax / root.layout.height * Screen.height;
            Rect screenRect = new Rect(x, y, width, height);
            if (screenRect.width < 80f || screenRect.height < 80f) return;
            controller.Map.Show(screenRect, controller.State.location, controller.State.nearby);
        }

        private VisualElement stickGuidance;
        private VisualElement stickActions;
        private VisualElement cameraSurface;
        private Label stickPlacementGuidanceLabel;
        private Label stickModeTitle;
        private Button stickWriteButton;
        private Button stickInventoryButton;
        private Button stickCloseButton;
        private Button stickCancelButton;
        private Button stickRetryButton;
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
            top.style.top = 10f;
            top.style.alignItems = Align.FlexStart;
            stickCloseButton = new PaperIconButton("Close camera", "close", CloseCamera);
            top.Add(stickCloseButton);
            stickCloseButton.name = "STICK Close";
            stickCloseButton.AddToClassList("camera-close");
            stickCloseButton.Insert(0, new PaperDottedOutline(true));
            VisualElement topContent = Column(top);
            topContent.style.flexGrow = 1f;
            topContent.style.minWidth = 0f;
            VisualElement topCard = Column(topContent);
            topCard.name = "STICK title sticker";
            topCard.AddToClassList("camera-title-sticker");
            topCard.Add(new PaperDottedOutline(false));
            stickModeTitle = Text(topCard, "STICK", 23, true);
            stickModeTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            ScrollView guidanceScroll = PaperScroll(topContent);
            guidanceScroll.name = "STICK guidance scroll";
            guidanceScroll.AddToClassList("camera-guidance-notice");
            guidanceScroll.style.flexGrow = 0f;
            guidanceScroll.style.maxHeight = 116f;
            stickGuidance = guidanceScroll.contentContainer;
            stickPlacementGuidanceLabel = Text(stickGuidance, "", 14, false, Ink);
            stickPlacementGuidanceLabel.name = "STICK Placement Guidance";
            cameraTrackingLabel = Text(stickGuidance, "", 12, false, Muted);
            cameraTrackingLabel.style.marginTop = 2f;
            AddStatus(stickGuidance, state);
            VisualElement dock = Column(page);
            dock.name = "STICK camera dock";
            dock.style.position = Position.Absolute;
            dock.style.left = 12f;
            dock.style.right = 12f;
            dock.style.bottom = 12f;
            dock.style.paddingLeft = 12f;
            dock.style.paddingRight = 12f;
            dock.style.paddingTop = 8f;
            dock.style.paddingBottom = 8f;
            dock.style.backgroundColor = Paper;
            dock.style.borderTopLeftRadius = 20f;
            dock.style.borderTopRightRadius = 20f;
            dock.style.borderBottomLeftRadius = 20f;
            dock.style.borderBottomRightRadius = 20f;
            // Keep recovery content scrollable between the independently sized overlays.
            void LayoutRecovery()
            {
                recoveryScroll.style.top = top.layout.yMax + 8f;
                recoveryScroll.style.bottom = Mathf.Max(0f, page.layout.height - dock.layout.yMin) + 8f;
            }
            top.RegisterCallback<GeometryChangedEvent>(_ => LayoutRecovery());
            dock.RegisterCallback<GeometryChangedEvent>(_ => LayoutRecovery());
            page.RegisterCallback<GeometryChangedEvent>(_ => LayoutRecovery());
            stickActions = Row(dock);
            stickActions.style.alignItems = Align.Center;
            stickActions.style.justifyContent = Justify.SpaceBetween;
            stickInventoryButton = Action(stickActions, "STICK", controller.OpenCreation);
            stickInventoryButton.name = "STICK Inventory";
            stickInventoryButton.text = "";
            stickInventoryButton.AddToClassList("camera-book-button");
            stickInventoryButton.Insert(0, new PaperDottedOutline(true));
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
            Label bookLabel = Text(stickInventoryButton, "STICK", 12, false);
            bookLabel.pickingMode = PickingMode.Ignore;
            bookLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
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
            VisualElement actions = Column(stickActions);
            actions.style.flexGrow = 1f;
            actions.style.marginLeft = 10f;
            stickWriteButton = Action(actions, "Write note", () => { sheet = Sheet.Note; QueueRender(); });
            stickWriteButton.name = "STICK Write note";
            PaperDottedOutline.Decorate(stickWriteButton, capsule: true);
            stickRetryButton = Action(actions, "Retry AR search", controller.StartDiscovery, false);
            stickRetryButton.name = "STICK Retry AR search";
            stickCancelButton = Action(actions, "Cancel placement", controller.CancelPlacement, false);
            RefreshStick(state);
            RefreshCamera(state);
        }

        private void RefreshStick(AppState state)
        {
            if (stickActions == null) return;
            IArExperience ar = controller?.Ar;
            bool selected = PaperFlow.HasPlacementSelection(state);
            PaperStickState placement = PaperFlow.StickPlacement(state, ar?.IsTracking ?? false,
                ar?.HasPlacementSurface ?? false, ar?.HasPlacementPreview ?? false,
                ar?.PlacementBusy ?? false);
            stickModeTitle.text = selected ? placement.Title : state.selected != null ? "Find sticker" : "STICK";
            stickPlacementGuidanceLabel.text = selected ? placement.Guidance : state.selected != null ?
                PaperFlow.DiscoveryGuidance(state.selected) :
                "Open your stickers, choose one, then place it on a surface.";
            stickWriteButton.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
            SetDisabled(stickWriteButton, !placement.CanWriteNote);
            stickRetryButton.style.display = PaperFlow.ShowDiscoveryRetry(state) ? DisplayStyle.Flex : DisplayStyle.None;
            SetDisabled(stickRetryButton, state.busy);
            stickCancelButton.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
            SetDisabled(stickCancelButton, state.busy || (ar?.PlacementBusy ?? false));
            SetDisabled(stickInventoryButton, state.busy || (ar?.PlacementBusy ?? false));
            SetDisabled(stickCloseButton, state.busy);
            if (cameraTrackingLabel != null)
                cameraTrackingLabel.text = ar == null ? "AR is unavailable on this device." :
                    Safe(ar.Status, ar.IsTracking ? "Look around for Taggi." : "Move slowly to scan your surroundings.");
            UpdateCameraInteraction();
        }

        private void RefreshCamera(AppState state)
        {
            if (root == null) return;
            CameraPresentationState presentation = controller?.Ar?.CameraPresentation ?? CameraPresentationState.Unavailable;
            CameraMessage message = PaperFlow.Camera(presentation);
            bool stickVisible = state.page == AppPage.Stick && !state.accountOpen;
            root.style.backgroundColor = stickVisible && !message.Cover ? Color.clear : Paper;
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
                    controller?.Ar?.CanPublish ?? false, state.busy, state.hasPendingPublication));
            }
            if (publishReadinessLabel != null)
            {
                var camera = controller?.Ar;
                publishReadinessLabel.text = PaperFlow.PublishNotice(draftPlace, draftTeaser, draftNote,
                    camera?.IsTracking ?? false, camera?.HasPlacementPreview ?? false,
                    camera?.HasTrackedPlacement ?? false, camera?.CanPublish ?? false,
                    state.busy, state.hasPendingPublication, !string.IsNullOrEmpty(state.selectedDesign));
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
