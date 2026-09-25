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
        private Label homeCount;
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
            Label title = Text(page, "Your sticker book", 30, true);
            title.style.marginTop = 16f;
            title.style.marginBottom = 3f;
            homeCount = Text(page, "", 15, false, Muted);
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
            List<CollectedSticker> items = CollectionPresentation.OrderedDistinct(state.collection);
            int page = BookPaging.ClampPage(bookPage, items.Count);
            string key = page + ":" + items.Count + ":" + SignedIn(state);
            foreach (var item in items) key += ":" + item.id + ":" + item.revision + ":" + item.unavailable;
            if (!homeContents.NeedsRefresh(key)) return;
            bookPage = page;
            homeItems = items;
            homeCount.text = items.Count == 1 ? "1 sticker collected" : items.Count + " stickers collected";
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
                    Image image = Art(cell, item.presetId, textScale > 1.2f ? 78f : 67f);
                    image.style.maxWidth = Length.Percent(95);
                    image.style.maxHeight = Length.Percent(92);
                    image.style.opacity = item.unavailable ? .4f : 1f;
                }
            }
            if (items.Count == 0)
            {
                homeInvitation = Column(homeBook);
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
            exploreLocationNotice.text = !state.servicesConfigured ?
                "Nearby stickers need a configured service." : !HasLocation(state) ?
                "Allow location access to see stickers near you." : "";
            exploreLocationNotice.style.display = string.IsNullOrEmpty(exploreLocationNotice.text) ? DisplayStyle.None : DisplayStyle.Flex;
            exploreMapMessage.text = controller.Map == null ? "Map is unavailable on this device." :
                state.location == null ? (state.busy ? "Finding your location…" : "Location is unavailable.") : "";
            exploreMapMessage.style.display = string.IsNullOrEmpty(exploreMapMessage.text) ? DisplayStyle.None : DisplayStyle.Flex;
            StickerSummary selected = state.selected;
            string key = selected == null ? "none:" + (state.nearby.Count == 0) : selected.id + ":" + selected.revision;
            if (explorePreviewContents.NeedsRefresh(key))
            {
                explorePreview.Clear();
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
                    Art(titleRow, selected.presetId, 68f);
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
            if (exploreRefreshButton != null) SetDisabled(exploreRefreshButton, state.busy);
            if (exploreEmptyTitle != null) exploreEmptyTitle.text = state.busy ? "Looking for nearby stickers" :
                state.nearby.Count == 0 ? "No stickers in view yet" : "Tap a sticker on the map";
        }

        private void UpdateMapLayout()
        {
            mapDirty = false;
            if (controller?.Map == null || !MapPresentation.ShouldShow(controller.State, sheet != Sheet.None) ||
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
        private Button stickPrimaryButton;
        private readonly PresenterCache stickModeContents = new PresenterCache();
        private Label cameraTitleLabel;
        private Label cameraDetailLabel;
        private Button cameraRecoveryButton;

        private void BuildStick(AppState state)
        {
            stickModeContents.Reset();
            VisualElement page = Column(screenHost);
            page.name = "STICK camera";
            page.style.flexGrow = 1f;
            page.style.minHeight = 0f;
            page.style.justifyContent = Justify.SpaceBetween;
            page.style.backgroundColor = Color.clear;
            cameraCover = Column(page);
            cameraCover.name = "Opaque camera cover";
            cameraCover.style.position = Position.Absolute;
            cameraCover.style.left = 0f;
            cameraCover.style.right = 0f;
            cameraCover.style.top = 0f;
            cameraCover.style.bottom = 0f;
            cameraCover.style.backgroundColor = Paper;
            cameraCover.style.alignItems = Align.Center;
            cameraCover.style.justifyContent = Justify.Center;
            cameraCover.style.paddingLeft = 30f;
            cameraCover.style.paddingRight = 30f;
            Art(cameraCover, "taggi-1", 100f).style.marginBottom = 18f;
            cameraTitleLabel = Text(cameraCover, "Getting the camera ready", 23, true);
            cameraTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            cameraDetailLabel = Text(cameraCover, "Hold your phone up while the camera starts.", 15, false, Muted);
            cameraDetailLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            cameraDetailLabel.style.marginTop = 7f;
            cameraRecoveryButton = Action(cameraCover, "Try camera again", () =>
            {
                if (controller?.Ar?.CameraPresentation == CameraPresentationState.PermissionDenied)
                    Application.OpenURL("app-settings:");
                else controller?.Ar?.Enter();
            });
            cameraRecoveryButton.style.marginTop = 16f;
            VisualElement top = Column(page);
            top.style.marginLeft = 18f;
            top.style.marginRight = 18f;
            top.style.marginTop = 12f;
            top.style.paddingLeft = 14f;
            top.style.paddingRight = 14f;
            top.style.paddingTop = 10f;
            top.style.paddingBottom = 10f;
            top.style.backgroundColor = Paper;
            top.style.borderTopLeftRadius = 14f;
            top.style.borderTopRightRadius = 14f;
            top.style.borderBottomLeftRadius = 14f;
            top.style.borderBottomRightRadius = 14f;
            Text(top, "Find it. Tap it. Keep the story.", 18, true);
            cameraTrackingLabel = Text(top, "", 14, false, Muted);
            cameraTrackingLabel.style.marginTop = 3f;
            stickGuidance = Column(top);
            AddStatus(top, state);
            VisualElement dock = Column(page);
            dock.style.paddingLeft = 18f;
            dock.style.paddingRight = 18f;
            dock.style.paddingTop = 10f;
            dock.style.paddingBottom = 12f;
            dock.style.backgroundColor = Paper;
            dock.style.borderTopLeftRadius = 22f;
            dock.style.borderTopRightRadius = 22f;
            stickActions = Column(dock);
            RefreshStick(state);
            RefreshCamera(state);
        }

        private void RefreshStick(AppState state)
        {
            if (stickActions == null) return;
            string mode = !string.IsNullOrEmpty(state.selectedPreset) ? "placing:" + state.selectedPreset :
                state.selected != null ? "discovering:" + state.selected.id : "idle";
            mode += ":" + HasLocation(state);
            if (stickModeContents.NeedsRefresh(mode))
            {
                stickGuidance.Clear();
                stickActions.Clear();
                stickPrimaryButton = null;
                if (!HasLocation(state)) Text(stickGuidance, "Location is needed to place or collect a sticker.", 13, false, Muted);
                if (state.selected != null)
                {
                    Text(stickGuidance, "Find " + Safe(state.selected.place, "the place") + ": " +
                        Safe(state.selected.teaser, "Look for the sticker."), 14).style.marginTop = 6f;
                    Text(stickGuidance, "Tap its image in AR to unlock the full note.", 13, false, Muted);
                }
                if (!string.IsNullOrEmpty(state.selectedPreset))
                {
                    Text(stickActions, "Position Taggi · Pinch to resize · Twist to rotate", 13, false, Muted);
                    stickPrimaryButton = Action(stickActions, "Write note", () => { sheet = Sheet.Note; QueueRender(); });
                    stickPrimaryButton.style.marginTop = 7f;
                    VisualElement secondary = Row(stickActions);
                    secondary.style.justifyContent = Justify.SpaceBetween;
                    Action(secondary, "Change pose", () => { sheet = Sheet.Picker; QueueRender(); }, false);
                    Action(secondary, "Cancel placement", controller.CancelPlacement, false);
                }
                else if (state.selected != null)
                {
                    Text(stickActions, "Look around slowly for Taggi.", 15, true);
                    stickPrimaryButton = Action(stickActions, "Retry AR search", controller.StartDiscovery);
                    Action(stickActions, "Back to Explore", () => controller.Navigate(AppPage.Explore), false);
                }
                else
                {
                    Text(stickActions, "Leave something worth finding.", 16, true);
                    stickPrimaryButton = Action(stickActions, "Leave a sticker", () => { sheet = Sheet.Picker; QueueRender(); });
                    stickPrimaryButton.style.marginTop = 6f;
                }
            }
            if (stickPrimaryButton != null) SetDisabled(stickPrimaryButton, state.busy ||
                (state.selected != null && controller.Ar == null));
            if (cameraTrackingLabel != null)
                cameraTrackingLabel.text = controller.Ar == null ? "AR is unavailable on this device." :
                    Safe(controller.Ar.Status, controller.Ar.IsTracking ? "Look around for Taggi." : "Move slowly to scan your surroundings.");
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
            if (publishButton == null) return;
            publishButton.text = state.busy ? "Publishing…" : state.hasPendingPublication ? "Retry publish" : "Publish sticker";
            SetDisabled(publishButton, !PaperFlow.CanPresentPublish(draftPlace, draftTeaser, draftNote,
                controller?.Ar?.CanPublish ?? false, state.busy, state.hasPendingPublication));
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
