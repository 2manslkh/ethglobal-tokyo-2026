using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private enum HomeSection { Collected, Designs, Placed }

        private VisualElement homeBook;
        private VisualElement homeFooter;
        private VisualElement homeInvitation;
        private VisualElement homeCollectedSection;
        private VisualElement homeDesignSection;
        private VisualElement homeDesignGrid;
        private VisualElement homePlacedSection;
        private VisualElement homePlacedList;
        private ScrollView homeScroll;
        private Label homeDesignStatus;
        private PaperSelection homeCollectedTab;
        private PaperSelection homeDesignTab;
        private PaperSelection homePlacedTab;
        private Label homePlacedStatus;
        private Button homeRetryPlacements;
        private Button homePlacedSignIn;
        private Button homeRefreshDesigns;
        private Button homeSignInButton;
        private Button homePreviewPlaceButton;
        private Button homePreviewRemoveButton;
        private Label homeDesignActionError;
        private PaperCollectionCount homeCount;
        private Button homeCreateButton;
        private readonly PresenterCache homeContents = new PresenterCache();
        private readonly PresenterCache homeDesignContents = new PresenterCache();
        private readonly PresenterCache homePlacedContents = new PresenterCache();
        private readonly List<Button> homeDesignCards = new List<Button>();
        private List<CollectedSticker> homeItems = new List<CollectedSticker>();
        private HomeSection homeSection;
        private Vector2 homeCollectedOffset;
        private Vector2 homeDesignOffset;
        private Vector2 homePlacedOffset;
        private string homeRequestedPlacementCursor;
        private List<StickerSummary> homeObservedPlacements;
        private bool homeScrollRestorePending;
        private int homeScrollRestoreGeneration;
        private string homeUserId;
        private bool homeUserKnown;
        private bool deleteDesignFromHome;
        private bool homePlacementPending;

        private void SyncHomeIdentity(AppState state)
        {
            string userId = state.user?.uid;
            if (!homeUserKnown)
            {
                homeUserKnown = true;
                homeUserId = userId;
                return;
            }
            if (homeUserId == userId) return;
            homeUserId = userId;
            homeSection = HomeSection.Collected;
            homeCollectedOffset = homeDesignOffset = homePlacedOffset = Vector2.zero;
            bookPage = 0;
            homeDesignContents.Reset();
            homePlacedContents.Reset();
            homeRequestedPlacementCursor = null;
            homeObservedPlacements = null;
            if (sheet == Sheet.HomeDesignPreview || (sheet == Sheet.DeleteDesign && deleteDesignFromHome))
            {
                sheet = Sheet.None;
                sheetDesignId = null;
                deleteDesignFromHome = false;
            }
        }

        private void SyncHomePlacement(AppState state)
        {
            if (homePlacementPending &&
                (sheet != Sheet.HomeDesignPreview || state.page != AppPage.Home || !state.busy))
                homePlacementPending = false;
        }

        private void CaptureHomeScroll()
        {
            if (homeScroll == null) return;
            if (homeSection == HomeSection.Collected) homeCollectedOffset = homeScroll.scrollOffset;
            else if (homeSection == HomeSection.Designs) homeDesignOffset = homeScroll.scrollOffset;
            else homePlacedOffset = homeScroll.scrollOffset;
        }

        private void RestoreHomeScrollAfterLayout(HomeSection section)
        {
            homeScrollRestorePending = true;
            homeScrollRestoreGeneration++;
        }

        private void OnHomeSectionGeometry(HomeSection section, GeometryChangedEvent evt)
        {
            VisualElement sectionElement = section == HomeSection.Collected ? homeCollectedSection :
                section == HomeSection.Designs ? homeDesignSection : homePlacedSection;
            if (evt.target != sectionElement || !homeScrollRestorePending || homeSection != section) return;
            int generation = homeScrollRestoreGeneration;
            ScrollView scroll = homeScroll;
            scroll.schedule.Execute(() =>
            {
                if (scroll.panel == null || homeSection != section ||
                    generation != homeScrollRestoreGeneration) return;
                scroll.scrollOffset = section == HomeSection.Collected ? homeCollectedOffset :
                    section == HomeSection.Designs ? homeDesignOffset : homePlacedOffset;
                homeScrollRestorePending = false;
                MaybeLoadMorePlacements();
            });
        }

        private void BuildHome(AppState state)
        {
            homeContents.Reset();
            homeDesignContents.Reset();
            homePlacedContents.Reset();
            homeScroll = PaperScroll(screenHost);
            homeScroll.name = "Home scroll";
            homeScroll.verticalScroller.valueChanged += _ => MaybeLoadMorePlacements();
            VisualElement page = Column(homeScroll.contentContainer);
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
            VisualElement summary = Row(page);
            summary.style.alignItems = Align.Center;
            summary.style.justifyContent = Justify.SpaceBetween;
            summary.style.marginTop = 7f;
            summary.style.marginBottom = 13f;
            homeCount = new PaperCollectionCount();
            homeCount.name = "Home collected count";
            homeCount.style.flexDirection = FlexDirection.Row;
            homeCount.style.alignItems = Align.Center;
            homeCount.style.flexGrow = 1f;
            homeCount.style.minWidth = 0f;
            summary.Add(homeCount);
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
            homeCount.Caption.style.marginLeft = 6f;
            homeCount.Caption.style.whiteSpace = WhiteSpace.Normal;
            homeCount.Caption.style.minWidth = 0f;
            VisualElement sectionSwitch = Row(page);
            sectionSwitch.style.marginBottom = 14f;
            homeCollectedTab = new PaperSelection("Collected", homeSection == HomeSection.Collected,
                () => SetHomeSection(HomeSection.Collected));
            homeCollectedTab.name = "Home Collected";
            homeDesignTab = new PaperSelection("Your Designs", homeSection == HomeSection.Designs,
                () => SetHomeSection(HomeSection.Designs));
            homeDesignTab.name = "Home My designs";
            homePlacedTab = new PaperSelection("Placed", homeSection == HomeSection.Placed,
                () => SetHomeSection(HomeSection.Placed));
            homePlacedTab.name = "Home Placed";
            foreach (PaperSelection tab in new[] { homeCollectedTab, homeDesignTab, homePlacedTab })
            {
                tab.style.width = Length.Percent(tab == homeCollectedTab ? 38f : tab == homeDesignTab ? 34f : 28f);
                tab.style.flexGrow = 1f;
                tab.style.minWidth = 0f;
                tab.style.minHeight = 48f;
                tab.style.whiteSpace = WhiteSpace.Normal;
                tab.style.paddingLeft = 9f;
                tab.style.paddingRight = 9f;
                tab.style.fontSize = Mathf.RoundToInt(12f * textScale);
                tab.Q<PaperIcon>(className: "selection-check").style.display = DisplayStyle.None;
                sectionSwitch.Add(tab);
            }
            homeCollectedTab.style.whiteSpace = WhiteSpace.NoWrap;
            homeCollectedTab.style.marginRight = 3f;
            homeDesignTab.style.marginLeft = 3f;
            homeDesignTab.style.marginRight = 3f;
            homePlacedTab.style.marginLeft = 3f;
            homeCollectedSection = Column(page);
            homeCollectedSection.RegisterCallback<GeometryChangedEvent>(evt => OnHomeSectionGeometry(HomeSection.Collected, evt));
            homeBook = Column(homeCollectedSection);
            homeBook.name = "Sticker book page";
            PaperDottedOutline.Decorate(homeBook, container: true);
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
            homeFooter = Row(homeCollectedSection);
            homeFooter.style.minHeight = 62f;
            homeFooter.style.alignItems = Align.Center;
            homeFooter.style.justifyContent = Justify.SpaceBetween;
            homeDesignSection = Column(page);
            homeDesignSection.name = "Home design section";
            homeDesignSection.style.backgroundColor = Soft;
            homeDesignSection.style.paddingLeft = 12f;
            homeDesignSection.style.paddingRight = 12f;
            homeDesignSection.style.paddingTop = 12f;
            homeDesignSection.style.paddingBottom = 12f;
            homeDesignSection.RegisterCallback<GeometryChangedEvent>(evt => OnHomeSectionGeometry(HomeSection.Designs, evt));
            homeCreateButton = Action(homeDesignSection, "Make a sticker", OpenHomeCreator, false);
            homeCreateButton.name = "Home Make sticker";
            homeCreateButton.text = "";
            homeCreateButton.tooltip = "Make a sticker";
            homeCreateButton.style.flexDirection = FlexDirection.Column;
            homeCreateButton.style.flexShrink = 0f;
            homeCreateButton.style.width = 136f;
            homeCreateButton.style.minHeight = 138f;
            homeCreateButton.style.paddingLeft = 6f;
            homeCreateButton.style.paddingRight = 6f;
            homeCreateButton.style.paddingTop = 4f;
            homeCreateButton.style.paddingBottom = 6f;
            homeCreateButton.style.alignItems = Align.Center;
            homeCreateButton.style.justifyContent = Justify.Center;
            Image makeArt = new Image { name = "Home Make sticker art", scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore, image = Resources.Load<Texture2D>("Tagtag/Home/make-sticker") };
            makeArt.style.width = 96f;
            makeArt.style.height = 96f;
            makeArt.style.flexShrink = 0f;
            homeCreateButton.Add(makeArt);
            Label makeCaption = Text(homeCreateButton, "Make a\nsticker", 14, true);
            makeCaption.style.unityTextAlign = TextAnchor.MiddleCenter;
            makeCaption.style.whiteSpace = WhiteSpace.Normal;
            makeCaption.style.marginTop = 2f;
            makeCaption.style.flexShrink = 0f;
            makeCaption.pickingMode = PickingMode.Ignore;
            VisualElement designsHeading = Row(homeDesignSection);
            designsHeading.style.alignItems = Align.Center;
            if (textScale > 1.2f) designsHeading.style.flexDirection = FlexDirection.Column;
            Label designsTitle = Text(designsHeading, "Your designs", 21, true);
            designsTitle.style.flexGrow = 1f;
            designsTitle.style.minWidth = 0f;
            homeRefreshDesigns = Action(designsHeading, "Refresh", controller.RefreshDesigns, false);
            homeRefreshDesigns.name = "Home Refresh designs";
            homeRefreshDesigns.style.flexShrink = 0f;
            homeRefreshDesigns.style.minWidth = 80f;
            if (textScale > 1.2f) homeRefreshDesigns.style.alignSelf = Align.FlexStart;
            homeDesignStatus = Text(homeDesignSection, "", 14, false, Muted);
            homeDesignStatus.name = "Home design status";
            homeDesignStatus.style.marginTop = 10f;
            homeDesignStatus.style.marginBottom = 8f;
            homeSignInButton = Action(homeDesignSection, "Sign in", () =>
            {
                accountScreen = AccountScreen.SignIn;
                controller.SetAccountOpen(true);
            }, false);
            homeSignInButton.name = "Home Sign in";
            homeSignInButton.style.alignSelf = Align.FlexStart;
            homeSignInButton.style.marginBottom = 12f;
            homeDesignGrid = Column(homeDesignSection);
            homeDesignGrid.name = "Home Designs";
            homePlacedSection = Column(page);
            homePlacedSection.name = "Home placed section";
            homePlacedSection.style.backgroundColor = Soft;
            homePlacedSection.style.paddingLeft = 12f;
            homePlacedSection.style.paddingRight = 12f;
            homePlacedSection.style.paddingTop = 12f;
            homePlacedSection.style.paddingBottom = 12f;
            homePlacedSection.RegisterCallback<GeometryChangedEvent>(evt => OnHomeSectionGeometry(HomeSection.Placed, evt));
            Text(homePlacedSection, "Placed", 21, true);
            homePlacedStatus = Text(homePlacedSection, "", 14, false, Muted);
            homePlacedStatus.name = "Home placed status";
            homePlacedStatus.style.marginTop = 8f;
            homeRetryPlacements = Action(homePlacedSection, "Retry", () =>
                RefreshPlacementsFromStart(), false);
            homeRetryPlacements.name = "Home Retry placements";
            homeRetryPlacements.style.alignSelf = Align.FlexStart;
            homePlacedSignIn = Action(homePlacedSection, "Sign in", () =>
            {
                accountScreen = AccountScreen.SignIn;
                controller.SetAccountOpen(true);
            }, false);
            homePlacedSignIn.name = "Home Placed sign in";
            homePlacedSignIn.style.alignSelf = Align.FlexStart;
            homePlacedList = Column(homePlacedSection);
            homePlacedList.name = "Home Placed list";
            homePlacedList.RegisterCallback<GeometryChangedEvent>(_ => MaybeLoadMorePlacements());
            RefreshHome(state);
            AddStatus(page, state);
            RestoreHomeScrollAfterLayout(homeSection);
            if (homeSection == HomeSection.Designs && SignedIn(state)) controller.RefreshDesigns();
            if (homeSection == HomeSection.Placed && SignedIn(state))
                RefreshPlacementsFromStart();
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
            homeCount.SetCount(items.Count);
            homeCollectedTab.SetSelected(homeSection == HomeSection.Collected);
            homeDesignTab.SetSelected(homeSection == HomeSection.Designs);
            homePlacedTab.SetSelected(homeSection == HomeSection.Placed);
            homeCollectedSection.style.display = homeSection == HomeSection.Collected ? DisplayStyle.Flex : DisplayStyle.None;
            homeDesignSection.style.display = homeSection == HomeSection.Designs ? DisplayStyle.Flex : DisplayStyle.None;
            homePlacedSection.style.display = homeSection == HomeSection.Placed ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshHomeDesigns(state);
            RefreshHomePlacements(state);
            if (!homeContents.NeedsRefresh(key)) return;
            bookPage = page;
            homeItems = items;
            homeBook.Clear();
            PaperDottedOutline.Decorate(homeBook, container: true);
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
                homeInvitation.style.position = Position.Absolute;
                homeInvitation.style.left = 12f;
                homeInvitation.style.right = 12f;
                homeInvitation.style.top = 48f;
                homeInvitation.style.bottom = 48f;
                homeInvitation.style.alignItems = Align.Center;
                homeInvitation.style.justifyContent = Justify.Center;
                Image emptyArt = new Image { name = "Home Empty Taggi", scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore, image = Resources.Load<Texture2D>("Tagtag/Home/empty-book") };
                emptyArt.style.width = 128f;
                emptyArt.style.height = 128f;
                homeInvitation.Add(emptyArt);
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

        private void SetHomeSection(HomeSection section, bool refreshDesigns = true)
        {
            if (section == homeSection || homeScroll == null) return;
            CaptureHomeScroll();
            homeSection = section;
            RestoreHomeScrollAfterLayout(section);
            RefreshHome(controller.State);
            if (refreshDesigns && section == HomeSection.Designs && SignedIn(controller.State)) controller.RefreshDesigns();
            if (section == HomeSection.Placed && SignedIn(controller.State))
                RefreshPlacementsFromStart();
        }

        private void RefreshPlacementsFromStart()
        {
            homeRequestedPlacementCursor = null;
            (controller as IPlacedLocationsController)?.RefreshPlacements();
        }

        private void OpenHomeCreator()
        {
            controller.OpenCreation();
            if (!controller.State.creationOpen) return;
            sheet = Sheet.Creator;
            QueueRender();
        }

        private void OpenHomeDesignsFromCreator()
        {
            if (controller.State.busy) return;
            bool fromHome = controller.State.page == AppPage.Home && homeScroll != null;
            if (fromHome)
                SetHomeSection(HomeSection.Designs, false);
            else
                homeSection = HomeSection.Designs;
            controller.CloseCreation();
            controller.Navigate(AppPage.Home);
            if (fromHome && SignedIn(controller.State)) controller.RefreshDesigns();
            QueueRender();
        }

        private List<StickerDesign> HomeOwnedDesigns(AppState state)
        {
            if (!SignedIn(state)) return new List<StickerDesign>();
            return state.designs.Where(design => design != null && !string.IsNullOrEmpty(design.id) &&
                    design.ownerId == state.user.uid && design.kind != "preset" &&
                    Array.IndexOf(Presets, design.id) < 0)
                .OrderByDescending(design => design.createdAt).ThenBy(design => design.id, StringComparer.Ordinal)
                .ToList();
        }

        private void RefreshHomeDesigns(AppState state)
        {
            if (homeDesignGrid == null) return;
            SetDisabled(homeRefreshDesigns, state.busy || state.designsLoading || !SignedIn(state));
            List<StickerDesign> designs = HomeOwnedDesigns(state);
            homeDesignStatus.text = !SignedIn(state) ? "Sign in to see your saved designs." :
                state.designsLoading ? designs.Count > 0 ? "Refreshing your designs…" : "Loading your designs…" :
                !string.IsNullOrEmpty(state.designError) ? state.designError :
                designs.Count == 0 ? "No designs yet. Make a sticker to start your collection." : "";
            homeDesignStatus.style.color = SignedIn(state) && !state.designsLoading &&
                !string.IsNullOrEmpty(state.designError) ? (Color)new Color32(125, 39, 31, 255) : Muted;
            homeDesignStatus.style.display = string.IsNullOrEmpty(homeDesignStatus.text) ? DisplayStyle.None : DisplayStyle.Flex;
            homeSignInButton.style.display = SignedIn(state) ? DisplayStyle.None : DisplayStyle.Flex;
            string key = state.user?.uid + ":" + designs.Count;
            foreach (StickerDesign design in designs)
                key += ":" + design.id + ":" + design.revision + ":" + design.name + ":" +
                    design.thumbnailUrl + ":" + design.createdAt;
            if (!homeDesignContents.NeedsRefresh(key))
            {
                foreach (Button card in homeDesignCards) SetDisabled(card, state.busy);
                return;
            }
            homeDesignGrid.Clear();
            artworkNotices.RemoveAll(notice => notice.status.panel == null);
            homeDesignCards.Clear();
            for (int index = 0; index < designs.Count; index += 2)
            {
                VisualElement row = Row(homeDesignGrid);
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginBottom = 12f;
                for (int column = 0; column < 2 && index + column < designs.Count; column++)
                {
                    StickerDesign design = designs[index + column];
                    string designId = design.id;
                    VisualElement cell = Column(row);
                    cell.style.width = Length.Percent(48f);
                    cell.style.minWidth = 0f;
                    Button card = Action(cell, "", () => OpenHomeDesign(designId), false);
                    card.name = "Home Design " + designId;
                    card.tooltip = "Preview " + Safe(design.name, "sticker design");
                    card.style.width = Length.Percent(100f);
                    card.style.minWidth = 0f;
                    card.style.minHeight = textScale > 1.2f ? 156f : 144f;
                    card.style.flexDirection = FlexDirection.Column;
                    card.style.alignItems = Align.Center;
                    card.style.justifyContent = Justify.Center;
                    card.style.paddingLeft = 6f;
                    card.style.paddingRight = 6f;
                    Image art = Art(card, design, textScale > 1.2f ? 88f : 96f);
                    art.pickingMode = PickingMode.Ignore;
                    Label name = Text(card, Safe(design.name, "Untitled sticker"), 14, true);
                    name.style.unityTextAlign = TextAnchor.MiddleCenter;
                    name.style.marginTop = 5f;
                    name.pickingMode = PickingMode.Ignore;
                    SetDisabled(card, state.busy);
                    homeDesignCards.Add(card);
                    AddArtworkNotice(cell, art, designId, true);
                }
            }
        }

        private void RefreshHomePlacements(AppState state)
        {
            if (homePlacedList == null) return;
            if (!ReferenceEquals(homeObservedPlacements, state.placements))
            {
                homeObservedPlacements = state.placements;
                homeRequestedPlacementCursor = null;
            }
            bool signedIn = SignedIn(state);
            List<StickerSummary> placements = signedIn && state.placements != null ? state.placements
                .Where(item => item != null && !string.IsNullOrEmpty(item.id) && item.status == "published")
                .OrderByDescending(item => item.createdAt).ThenByDescending(item => item.id, StringComparer.Ordinal)
                .GroupBy(item => item.id).Select(group => group.First()).ToList() : new List<StickerSummary>();
            homePlacedStatus.text = !signedIn ? "Sign in to see your placed stickers." :
                state.placementsLoading ? placements.Count == 0 ? "Loading your placed stickers…" : "Loading more placed stickers…" :
                !string.IsNullOrEmpty(state.placementsError) ? state.placementsError :
                !state.placementsLoaded ? "Loading your placed stickers…" :
                placements.Count == 0 ? "No placed stickers yet. Place a design to see it here." : "";
            homePlacedStatus.style.color = signedIn && !state.placementsLoading &&
                !string.IsNullOrEmpty(state.placementsError) ? (Color)new Color32(125, 39, 31, 255) : Muted;
            homePlacedStatus.style.display = string.IsNullOrEmpty(homePlacedStatus.text) ? DisplayStyle.None : DisplayStyle.Flex;
            homeRetryPlacements.style.display = signedIn && !state.placementsLoading &&
                !string.IsNullOrEmpty(state.placementsError) ? DisplayStyle.Flex : DisplayStyle.None;
            homePlacedSignIn.style.display = signedIn ? DisplayStyle.None : DisplayStyle.Flex;
            string key = state.user?.uid + ":" + placements.Count;
            foreach (StickerSummary item in placements)
                key += ":" + item.id + ":" + item.revision + ":" + item.place + ":" + item.createdAt + ":" + item.thumbnailUrl;
            if (homeSection == HomeSection.Placed) homeScroll?.schedule.Execute(MaybeLoadMorePlacements);
            if (!homePlacedContents.NeedsRefresh(key)) return;
            homePlacedList.Clear();
            foreach (StickerSummary item in placements)
            {
                StickerSummary placement = item;
                Button card = Action(homePlacedList, "", () =>
                    (controller as IPlacedLocationsController)?.OpenPlacedLocation(placement), false);
                card.name = "Home Placed " + item.id;
                card.tooltip = "Explore " + Safe(item.place, "placed sticker");
                card.style.flexDirection = FlexDirection.Row;
                card.style.alignItems = Align.Center;
                card.style.justifyContent = Justify.FlexStart;
                card.style.marginBottom = 10f;
                card.style.minHeight = 96f;
                Image art = Art(card, item, 72f);
                art.pickingMode = PickingMode.Ignore;
                VisualElement words = Column(card);
                words.pickingMode = PickingMode.Ignore;
                words.style.flexGrow = 1f;
                words.style.minWidth = 0f;
                words.style.marginLeft = 12f;
                Text(words, Safe(item.place, "Placed sticker"), 16, true).pickingMode = PickingMode.Ignore;
                Text(words, "Placed " + Date(item.createdAt), 13, false, Muted).pickingMode = PickingMode.Ignore;
            }
        }

        private void MaybeLoadMorePlacements()
        {
            AppState state = controller?.State;
            if (homeSection != HomeSection.Placed || homeScroll == null || state == null || !SignedIn(state) ||
                state.placementsLoading || !string.IsNullOrEmpty(state.placementsError) ||
                string.IsNullOrEmpty(state.placementsNextCursor) ||
                state.placementsNextCursor == homeRequestedPlacementCursor) return;
            float viewport = homeScroll.contentViewport.layout.height;
            float content = homeScroll.contentContainer.layout.height;
            if (viewport <= 0f || content <= 0f || homeScroll.scrollOffset.y + viewport < content - 160f) return;
            homeRequestedPlacementCursor = state.placementsNextCursor;
            (controller as IPlacedLocationsController)?.LoadMorePlacements();
        }

        private void OpenHomeDesign(string id)
        {
            if (HomeOwnedDesigns(controller.State).All(design => design.id != id)) return;
            sheetDesignId = id;
            sheet = Sheet.HomeDesignPreview;
            QueueRender();
        }

        private void BuildHomeDesignPreview(VisualElement content, AppState state)
        {
            StickerDesign design = HomeOwnedDesigns(state).Find(item => item.id == sheetDesignId);
            if (design == null)
            {
                Text(content, "This design is no longer available.", 16, false, Muted);
                return;
            }
            Image art = Art(content, design, 240f, false);
            art.name = "Home Design preview art";
            art.style.maxWidth = Length.Percent(100f);
            art.style.marginTop = 10f;
            AddArtworkNotice(content, art, design.id, false);
            Label name = Text(content, Safe(design.name, "Untitled sticker"), 23, true);
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.marginTop = 12f;
            homePreviewPlaceButton = Action(content, "Place sticker", () =>
            {
                if (homePlacementPending) return;
                homePlacementPending = true;
                controller.SelectDesign(design.id);
                QueueRender();
            });
            homePreviewPlaceButton.name = "Home Place design";
            homePreviewPlaceButton.style.marginTop = 18f;
            homePreviewRemoveButton = Action(content, "Remove design", () =>
            {
                deleteDesignFromHome = true;
                sheet = Sheet.DeleteDesign;
                QueueRender();
            }, false);
            homePreviewRemoveButton.name = "Home Remove design";
            homePreviewRemoveButton.style.marginTop = 7f;
            BuildDesignActionError(content);
            RefreshHomeDesignPreview(state);
        }

        private void BuildDesignActionError(VisualElement content)
        {
            homeDesignActionError = Text(content, "", 14, false, new Color32(125, 39, 31, 255));
            homeDesignActionError.name = "Design action error";
            homeDesignActionError.style.marginTop = 9f;
        }

        private void RefreshDesignActionError(AppState state)
        {
            if (homeDesignActionError == null) return;
            homeDesignActionError.text = state.designError ?? "";
            homeDesignActionError.style.display = string.IsNullOrEmpty(homeDesignActionError.text) ?
                DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshHomeDesignPreview(AppState state)
        {
            if (homePreviewPlaceButton != null) SetDisabled(homePreviewPlaceButton, state.busy || homePlacementPending);
            if (homePreviewRemoveButton != null) SetDisabled(homePreviewRemoveButton, state.busy || homePlacementPending);
            RefreshDesignActionError(state);
        }

        private void FocusHomeDesign(string id)
        {
            screenHost?.schedule.Execute(() => screenHost.Q<Button>("Home Design " + id)?.Focus());
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

    }
}
