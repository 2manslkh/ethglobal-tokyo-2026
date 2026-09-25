using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private void BuildHome(AppState state)
        {
            List<CollectedSticker> collection = CollectionPresentation.OrderedDistinct(state.collection);
            bookPage = BookPaging.ClampPage(bookPage, collection.Count);
            VisualElement page = Column(safeRoot);
            page.style.flexGrow = 1f;
            page.style.minHeight = 0f;
            page.style.paddingLeft = 20f;
            page.style.paddingRight = 20f;

            Label title = Text(page, "Your sticker book", 30, true);
            title.style.marginTop = 16f;
            title.style.marginBottom = 8f;
            Label count = Text(page, collection.Count == 1 ? "1 sticker collected" : collection.Count + " stickers collected", 16, false, Muted);
            count.style.marginBottom = 18f;

            VisualElement book = Column(page);
            book.name = "Sticker book page";
            book.style.flexGrow = 1f;
            book.style.minHeight = 0f;
            book.style.backgroundColor = Soft;
            book.style.borderTopLeftRadius = 18f;
            book.style.borderTopRightRadius = 18f;
            book.style.borderBottomLeftRadius = 18f;
            book.style.borderBottomRightRadius = 18f;
            book.style.paddingTop = 8f;
            book.style.paddingBottom = 8f;
            book.style.paddingLeft = 8f;
            book.style.paddingRight = 8f;
            book.RegisterCallback<PointerDownEvent>(evt => OnBookPointerDown(book, evt));
            book.RegisterCallback<PointerUpEvent>(evt => OnBookPointerUp(book, evt, collection.Count));
            book.RegisterCallback<PointerCancelEvent>(_ =>
            {
                bookPressed = false;
                pressedStickerId = null;
            });

            for (int rowIndex = 0; rowIndex < BookPaging.Rows; rowIndex++)
            {
                VisualElement row = Row(book);
                row.style.flexGrow = 1f;
                row.style.minHeight = 0f;
                for (int columnIndex = 0; columnIndex < BookPaging.Columns; columnIndex++)
                {
                    int slot = rowIndex * BookPaging.Columns + columnIndex;
                    int itemIndex = BookPaging.IndexAt(bookPage, slot, collection.Count);
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
                    cell.style.borderBottomColor = Line;
                    cell.style.borderBottomWidth = 1f;

                    if (itemIndex >= 0)
                    {
                        CollectedSticker item = collection[itemIndex];
                        cell.userData = item.id;
                        cell.tooltip = "Open " + Safe(item.place, "collected sticker") + " details";
                        cell.focusable = true;
                        cell.tabIndex = 0;
                        string stickerId = item.id;
                        cell.RegisterCallback<KeyDownEvent>(evt =>
                        {
                            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
                            sheet = Sheet.Collected;
                            sheetStickerId = stickerId;
                            controller.OpenCollected(stickerId);
                            QueueRender();
                            evt.StopPropagation();
                        });
                        Image image = Art(cell, item.presetId, 72f);
                        image.style.maxWidth = Length.Percent(95);
                        image.style.maxHeight = Length.Percent(92);
                        image.style.opacity = item.unavailable ? 0.35f : 1f;
                    }
                    else
                    {
                        VisualElement restingMark = new VisualElement();
                        restingMark.style.width = 5f;
                        restingMark.style.height = 5f;
                        restingMark.style.borderTopLeftRadius = 3f;
                        restingMark.style.borderTopRightRadius = 3f;
                        restingMark.style.borderBottomLeftRadius = 3f;
                        restingMark.style.borderBottomRightRadius = 3f;
                        restingMark.style.backgroundColor = Line;
                        cell.Add(restingMark);
                    }
                }
            }

            VisualElement footer = Row(page);
            footer.style.height = 58f;
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = Justify.SpaceBetween;
            Button previous = Action(footer, "Previous", () => ChangeBookPage(bookPage - 1, collection.Count), false);
            SetDisabled(previous, bookPage == 0);
            Text(footer, "Page " + (bookPage + 1) + " of " + BookPaging.PageCount(collection.Count), 14, false, Muted);
            Button next = Action(footer, "Next", () => ChangeBookPage(bookPage + 1, collection.Count), false);
            SetDisabled(next, bookPage + 1 >= BookPaging.PageCount(collection.Count));

            if (collection.Count == 0)
            {
                Label empty = Text(page, SignedIn(state) ? "Your first find is waiting nearby." : "Explore now. Sign in when you find a sticker to collect it.", 14, false, Muted);
                empty.style.marginBottom = 8f;
                Text(page, "Explore a clue · Find Taggi in AR · Tap to collect", 13, false, Muted).style.marginBottom = 8f;
                Action(page, "Explore nearby", () => controller.Navigate(AppPage.Explore)).style.marginBottom = 10f;
            }
            AddStatus(page, state);
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
                sheet = Sheet.Collected;
                sheetStickerId = pressedStickerId;
                controller.OpenCollected(pressedStickerId);
                QueueRender();
            }
            pressedStickerId = null;
        }

        private void ChangeBookPage(int page, int itemCount)
        {
            int clamped = BookPaging.ClampPage(page, itemCount);
            if (clamped == bookPage) return;
            bookPage = clamped;
            QueueRender();
        }

        private void BuildExplore(AppState state)
        {
            VisualElement page = Column(safeRoot);
            page.style.flexGrow = 1f;
            page.style.minHeight = 0f;
            page.style.backgroundColor = Paper;
            VisualElement heading = Row(page);
            heading.style.paddingLeft = 24f;
            heading.style.paddingRight = 20f;
            heading.style.marginTop = 14f;
            heading.style.marginBottom = 10f;
            heading.style.alignItems = Align.Center;
            heading.style.justifyContent = Justify.SpaceBetween;
            Text(heading, "Explore", 30, true);
            Button recenter = Action(heading, "Recenter", () =>
            {
                if (controller.State.location != null) controller.Map?.Recenter(controller.State.location);
                else controller.RefreshNearby();
            }, false);
            SetDisabled(recenter, controller.Map == null);

            if (!state.servicesConfigured)
            {
                Label configuration = Text(page, "Nearby stickers need a configured service. The map can still show your area if location is available.", 14, false, Muted);
                configuration.style.marginLeft = 24f;
                configuration.style.marginRight = 24f;
                configuration.style.marginBottom = 8f;
            }
            if (!HasLocation(state))
            {
                Label location = Text(page, "Location is unavailable. Allow location access to see stickers near you.", 14, false, Muted);
                location.style.marginLeft = 24f;
                location.style.marginRight = 24f;
                location.style.marginBottom = 8f;
            }

            mapRegion = new VisualElement();
            mapRegion.name = "Native MapKit region";
            mapRegion.style.flexGrow = 1f;
            mapRegion.style.minHeight = 120f;
            mapRegion.style.backgroundColor = (Color)new Color32(238, 237, 230, 255);
            mapRegion.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                mapDirty = true;
                UpdateMapLayout();
            });
            page.Add(mapRegion);
            if (controller.Map == null)
            {
                Label unavailable = Text(mapRegion, "Map is unavailable on this device.", 16, false, Muted);
                unavailable.style.marginTop = 24f;
                unavailable.style.marginLeft = 24f;
            }
            else if (state.location == null)
            {
                Label awaitingLocation = Text(mapRegion, "Waiting for your location…", 16, false, Muted);
                awaitingLocation.style.marginTop = 24f;
                awaitingLocation.style.marginLeft = 24f;
            }

            VisualElement teaser = Column(page);
            teaser.style.backgroundColor = Paper;
            teaser.style.paddingLeft = 24f;
            teaser.style.paddingRight = 24f;
            teaser.style.paddingTop = 16f;
            teaser.style.paddingBottom = 16f;
            StickerSummary selected = state.selected;
            if (selected == null)
            {
                Text(teaser, state.nearby.Count == 0 ? "No stickers in view yet" : "Tap a sticker on the map", 20, true);
                Label hint = Text(teaser, state.nearby.Count == 0 ? "Try another area or refresh nearby stickers." : "See its clue before you set out to find it.", 14, false, Muted);
                hint.style.marginTop = 5f;
                Action(teaser, "Refresh nearby", controller.RefreshNearby, false).style.alignSelf = Align.FlexStart;
            }
            else
            {
                VisualElement titleRow = Row(teaser);
                titleRow.style.alignItems = Align.Center;
                Art(titleRow, selected.presetId, 62f);
                VisualElement words = Column(titleRow);
                words.style.flexGrow = 1f;
                words.style.marginLeft = 10f;
                Text(words, Safe(selected.place, "A place nearby"), 20, true);
                Text(words, "Left by " + Safe(selected.authorName, "someone nearby"), 13, false, Muted);
                Label clue = Text(teaser, Safe(selected.teaser, "A sticker is waiting here."), 15);
                clue.style.marginTop = 9f;
                clue.style.marginBottom = 9f;
                Action(teaser, "Find in AR", () =>
                {
                    controller.StartDiscovery();
                });
                VisualElement secondary = Row(teaser);
                secondary.style.justifyContent = Justify.SpaceBetween;
                Action(secondary, "Report", () => OpenReport(selected.id), false);
                if (!string.IsNullOrEmpty(selected.authorId)) Action(secondary, "Block author", () => OpenBlock(selected.authorId), false);
            }
            AddStatus(page, state);
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

        private void BuildStick(AppState state)
        {
            VisualElement page = Column(safeRoot);
            page.style.flexGrow = 1f;
            page.style.minHeight = 0f;
            page.style.justifyContent = Justify.SpaceBetween;
            page.style.backgroundColor = Color.clear;
            VisualElement top = Column(page);
            top.style.marginLeft = 20f;
            top.style.marginRight = 20f;
            top.style.marginTop = 12f;
            top.style.paddingLeft = 16f;
            top.style.paddingRight = 16f;
            top.style.paddingTop = 12f;
            top.style.paddingBottom = 12f;
            top.style.backgroundColor = Paper;
            top.style.borderTopLeftRadius = 16f;
            top.style.borderTopRightRadius = 16f;
            top.style.borderBottomLeftRadius = 16f;
            top.style.borderBottomRightRadius = 16f;
            Text(top, "Find it. Tap it. Keep the story.", 18, true);
            string tracking = controller.Ar == null ? "AR is unavailable on this device." : Safe(controller.Ar.Status, controller.Ar.IsTracking ? "Move your phone slowly to scan." : "Looking for a surface…");
            Label trackingLabel = Text(top, tracking, 14, false, Muted);
            trackingLabel.style.marginTop = 4f;
            if (!HasLocation(state)) Text(top, "Location is needed to place or collect a sticker.", 13, false, Muted);
            if (state.selected != null)
            {
                Label clue = Text(top, "Find " + Safe(state.selected.place, "the place") + ": " + Safe(state.selected.teaser, "Look for the sticker."), 14);
                clue.style.marginTop = 8f;
                Text(top, "Tap the sticker in the camera to unlock its note. Being nearby is not enough.", 13, false, Muted);
            }
            AddStatus(top, state);

            VisualElement composer = Column(page);
            composer.style.backgroundColor = Paper;
            composer.style.borderTopLeftRadius = 22f;
            composer.style.borderTopRightRadius = 22f;
            composer.style.paddingTop = 13f;
            composer.style.paddingBottom = 14f;
            composer.style.paddingLeft = 20f;
            composer.style.paddingRight = 20f;
            composer.style.height = Length.Percent(string.IsNullOrEmpty(state.selectedPreset) ? 33f : 52f);
            composer.style.maxHeight = Length.Percent(65);
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            activeDraftScroll = scroll;
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;
            composer.Add(scroll);
            VisualElement content = scroll.contentContainer;
            if (state.selected != null && string.IsNullOrEmpty(state.selectedPreset))
            {
                Text(content, "Find Taggi", 21, true);
                Text(content, "Move slowly around the place. Once the sticker is tracked, tap it in the camera to unlock the note.", 14, false, Muted).style.marginTop = 8f;
                Button retry = Action(content, state.busy ? "Finding…" : "Retry AR search", controller.StartDiscovery);
                retry.style.marginTop = 12f;
                SetDisabled(retry, state.busy || controller.Ar == null);
                Action(content, "Back to Explore", () => controller.Navigate(AppPage.Explore), false).style.marginTop = 7f;
                Action(content, "Leave your own sticker", () => controller.SelectPreset(Presets[0]), false).style.marginTop = 7f;
                return;
            }
            Text(content, "Leave a sticker", 21, true);
            Label intro = Text(content, "Choose Taggi, place it on a surface, then leave a clue and a note.", 14, false, Muted);
            intro.style.marginTop = 5f;
            intro.style.marginBottom = 9f;
            VisualElement presets = Row(content);
            presets.style.justifyContent = Justify.SpaceBetween;
            foreach (string presetId in Presets)
            {
                string selectedId = presetId;
                Button choice = new Button(() => controller.SelectPreset(selectedId));
                choice.name = "Select " + presetId;
                choice.tooltip = "Select Taggi pose " + presetId.Substring(presetId.Length - 1);
                choice.style.flexGrow = 1f;
                choice.style.minWidth = 60f;
                choice.style.minHeight = 68f;
                choice.style.marginRight = 4f;
                choice.style.marginLeft = 4f;
                choice.style.borderTopLeftRadius = 12f;
                choice.style.borderTopRightRadius = 12f;
                choice.style.borderBottomLeftRadius = 12f;
                choice.style.borderBottomRightRadius = 12f;
                choice.style.borderTopWidth = 2f;
                choice.style.borderRightWidth = 2f;
                choice.style.borderBottomWidth = 2f;
                choice.style.borderLeftWidth = 2f;
                choice.style.borderTopColor = state.selectedPreset == presetId ? Ink : Line;
                choice.style.borderRightColor = state.selectedPreset == presetId ? Ink : Line;
                choice.style.borderBottomColor = state.selectedPreset == presetId ? Ink : Line;
                choice.style.borderLeftColor = state.selectedPreset == presetId ? Ink : Line;
                choice.style.backgroundColor = Paper;
                Art(choice, presetId, 58f);
                presets.Add(choice);
            }

            if (!string.IsNullOrEmpty(state.selectedPreset))
            {
                Label instruction = Text(content, "Move your phone to place Taggi. Pinch to resize and twist to rotate.", 13, false, Muted);
                instruction.style.marginTop = 9f;
                DraftField(content, "Place", draftPlace, 80, false, value => draftPlace = value);
                DraftField(content, "A short clue people can see before finding it", draftTeaser, 180, false, value => draftTeaser = value);
                DraftField(content, "Your note, unlocked when they find Taggi", draftNote, 2000, true, value => draftNote = value);
                if (!SignedIn(state))
                {
                    Text(content, "Sign in to publish. Your draft will stay here.", 14, false, Muted);
                    Action(content, "Sign in to publish", OpenSignIn).style.marginTop = 8f;
                }
                else
                {
                    Button publish = Action(content, state.busy ? "Publishing…" : "Publish sticker", () =>
                    {
                        controller.SetDraft(draftPlace, draftTeaser, draftNote);
                        controller.Publish();
                    });
                    publish.style.marginTop = 12f;
                    SetDisabled(publish, state.busy || controller.Ar == null || !controller.Ar.CanPublish ||
                        string.IsNullOrWhiteSpace(draftPlace) || string.IsNullOrWhiteSpace(draftTeaser) || string.IsNullOrWhiteSpace(draftNote));
                    if (controller.Ar != null && !controller.Ar.CanPublish)
                    {
                        Text(content, "Publishing becomes available after tracking and location are ready.", 13, false, Muted);
                    }
                }
                Action(content, "Cancel placement", controller.CancelPlacement, false).style.marginTop = 6f;
            }
            else if (state.selected == null)
            {
                Text(content, "Or walk toward a sticker on Explore and find it through the camera.", 13, false, Muted).style.marginTop = 10f;
            }
        }

        private void DraftField(VisualElement parent, string label, string value, int maxLength, bool multiline, Action<string> save)
        {
            TextField field = new TextField(label);
            field.name = label;
            field.tooltip = label;
            field.value = value;
            field.maxLength = maxLength;
            field.multiline = multiline;
            field.style.fontSize = Mathf.RoundToInt(16f * textScale);
            field.style.color = Ink;
            field.style.minHeight = multiline ? 100f : 64f;
            field.style.marginTop = 10f;
            parent.Add(field);
            field.RegisterCallback<FocusInEvent>(_ => focusedField = field);
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (focusedField == field) focusedField = null;
                QueueRender();
            });
            field.RegisterValueChangedCallback(evt =>
            {
                save(evt.newValue);
                controller.SetDraft(draftPlace, draftTeaser, draftNote);
            });
        }
    }
}
