using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    [DisallowMultipleComponent]
    public sealed partial class TagtagAppView : MonoBehaviour
    {
        private enum AccountScreen { Overview, SignIn, Authored, DeleteConfirmation }
        private enum Sheet { None, Picker, Creator, HomeDesignPreview, DeleteDesign, Note, Collected, Report, Block, Withdraw, ReferencePhoto, Privacy, Terms }

        private static readonly Color Paper = new Color32(255, 254, 250, 255);
        private static readonly Color Ink = new Color32(32, 32, 30, 255);
        private static readonly Color Muted = new Color32(92, 91, 85, 255);
        private static readonly Color Line = new Color32(226, 224, 215, 255);
        private static readonly Color Soft = new Color32(246, 245, 239, 255);
        private static readonly Color Yellow = new Color32(255, 225, 90, 255);
        private Font bodyFont, semiboldFont, headingFont;
        private Font BodyFont => bodyFont ?? (bodyFont = Resources.Load<Font>("Tagtag/Fonts/InstrumentRegular"));
        private Font SemiboldFont => semiboldFont ?? (semiboldFont = Resources.Load<Font>("Tagtag/Fonts/InstrumentSemibold"));
        private Font HeadingFont => headingFont ?? (headingFont = Resources.Load<Font>("Tagtag/Fonts/ShadowsIntoLight"));
        private Font DisplayFont => HeadingFont;
        private static readonly string[] Presets = StickerPresets.Ids.ToArray();
        private static readonly string[] ReportReasons = { "Harassment or hate", "Unsafe place", "Private information", "Spam or misleading", "Something else" };

        private ITagtagController controller;
        private UIDocument document;
        private PanelSettings ownedPanelSettings;
        private VisualElement root;
        private VisualElement safeRoot;
        private VisualElement statusBarBacking;
        private VisualElement screenHost;
        private VisualElement navHost;
        private VisualElement overlayHost;
        private VisualElement cameraCover;
        private Button publishButton;
        private Button homeProfileButton;
        private PaperNavigationMotion navigationMotion = new PaperNavigationMotion();
        private string renderedIdentity;
        private string renderedSheetSignature;
        private VisualElement mapRegion;
        private ScrollView activeDraftScroll;
        private Label statusLabel;
        private VisualElement statusNotice;
        private VisualElement statusSymbol;
        private Button statusSettingsButton;
        private Label screenStatusLabel;
        private VisualElement screenStatusNotice;
        private VisualElement screenStatusSymbol;
        private Button screenStatusSettingsButton;
        private TextField focusedField;
        private Rect lastSafeArea;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private float lastKeyboardHeight;
        private int bookPage;
        private AccountScreen accountScreen;
        private Sheet sheet;
        private string sheetStickerId;
        private string sheetDesignId;
        private string sheetAuthorId;
        private Sheet returnAfterSignIn;
        private string returnStickerId;
        private string returnAuthorId;
        private string returnFieldName;
        private int returnCursor;
        private int returnSelection;
        private string lastDraftFieldName;
        private int lastDraftCursor;
        private int lastDraftSelection;
        private string selectedReportReason = ReportReasons[0];
        private string deleteConfirmation = "";
        private string draftPlace = "";
        private string draftTeaser = "";
        private string draftNote = "";
        private readonly float textScale = 1f;
        private readonly bool reducedMotion = false;
        private bool renderQueued;
        private bool mapDirty;
        private bool bookPressed;
        private Vector2 bookStart;
        private string pressedStickerId;
        private string lastPresentedDiscoveryId;
        private bool publicationRequested;
        private bool captureNoteRequested;
        private AppPage renderedPage;
        private bool renderedAccountOpen;
        private bool artworkSubscribed;
        private readonly List<ArtworkNotice> artworkNotices = new List<ArtworkNotice>();
        private AppPage cameraReturnPage = AppPage.Home;

        public void Initialize(ITagtagController tagtagController)
        {
            if (controller != null)
            {
                controller.Changed -= OnControllerChanged;
                if (controller.Map is IMapLoadingExperience oldMapLoading)
                    oldMapLoading.Changed -= OnMapLoadingChanged;
            }

            controller = tagtagController ?? throw new ArgumentNullException(nameof(tagtagController));
            controller.Changed += OnControllerChanged;
            if (controller.Map is IMapLoadingExperience mapLoading)
                mapLoading.Changed += OnMapLoadingChanged;
            if (!artworkSubscribed)
            {
                StickerArtwork.Changed += OnArtworkChanged;
                artworkSubscribed = true;
            }
            EnsureDocument();
            SyncDraftFromState();
            QueueRender();
        }

        private void Awake()
        {
            EnsureDocument();
        }

        private void OnEnable()
        {
            PaperMotion.SetPaused(false);
            if (loginPlayback != null) loginPlayback.enabled = true;
            if (controller != null)
            {
                QueueRender();
            }
        }

        private void OnDisable()
        {
            PaperMotion.SetPaused(true);
            if (loginPlayback != null) loginPlayback.enabled = false;
            controller?.Ar?.SetCameraInteraction(default, true);
            controller?.Map?.Hide();
        }

        private void OnDestroy()
        {
            DisposeLogin();
            if (appleFont != null) Destroy(appleFont);
            if (artworkSubscribed) StickerArtwork.Changed -= OnArtworkChanged;
            if (controller != null)
            {
                controller.Changed -= OnControllerChanged;
                if (controller.Map is IMapLoadingExperience mapLoading)
                    mapLoading.Changed -= OnMapLoadingChanged;
                controller.Ar?.SetCameraInteraction(default, true);
                controller.Map?.Hide();
            }

            if (ownedPanelSettings != null)
            {
                Destroy(ownedPanelSettings);
            }
        }

        private void Update()
        {
            if (root == null)
            {
                EnsureDocument();
                if (root != null) QueueRender();
            }
            if (root == null || controller == null)
            {
                return;
            }

            float keyboardHeight = TouchScreenKeyboard.visible ? TouchScreenKeyboard.area.height : 0f;
            if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenWidth || Screen.height != lastScreenHeight ||
                Mathf.Abs(keyboardHeight - lastKeyboardHeight) > 1f)
            {
                lastKeyboardHeight = keyboardHeight;
                ApplySafeArea();
                if (sheetView != null)
                {
                    sheetView.style.bottom = SheetBottom();
                    ApplySheetHeight();
                }
                mapDirty = true;
                if (keyboardHeight > 0f && focusedField != null && activeDraftScroll != null)
                {
                    activeDraftScroll.schedule.Execute(() => activeDraftScroll.ScrollTo(focusedField));
                }
            }

            if (mapDirty && MapPresentation.ShouldShow(controller.State,
                    sheet != Sheet.None || controller.State.creationOpen || CelebrationActive))
            {
                UpdateMapLayout();
            }
            if (controller.State.page == AppPage.Stick) UpdateCameraInteraction();
        }

        private void EnsureDocument()
        {
            if (document == null)
            {
                document = GetComponent<UIDocument>();
                if (document == null)
                {
                    document = gameObject.AddComponent<UIDocument>();
                }
            }

            if (document.panelSettings == null)
            {
                // A saved panel keeps Unity's text and rendering resources in player builds.
                var template = Resources.Load<PanelSettings>("Tagtag/UI/TagtagPanel");
                if (template == null)
                    throw new InvalidOperationException("The tagtag UI panel asset is missing.");
                ownedPanelSettings = Instantiate(template);
                ownedPanelSettings.name = "Tagtag runtime panel";
                ownedPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ownedPanelSettings.referenceResolution = new Vector2Int(390, 844);
                ownedPanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                ownedPanelSettings.match = 0f;
                document.panelSettings = ownedPanelSettings;
            }

            root = document.rootVisualElement;
            if (root == null) return;
            root.AddToClassList("paper-app");
            root.EnableInClassList("reduced-motion", reducedMotion);
            root.style.unityFont = BodyFont;
            StyleSheet paperStyles = Resources.Load<StyleSheet>("Tagtag/UI/Paper");
            if (paperStyles != null && !root.styleSheets.Contains(paperStyles)) root.styleSheets.Add(paperStyles);
            root.style.backgroundColor = Paper;
            root.style.flexGrow = 1f;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);
            root.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                ApplySafeArea();
                mapDirty = true;
            });
            if (safeRoot == null)
            {
                safeRoot = Column(root);
                safeRoot.style.flexGrow = 1f;
                safeRoot.style.minHeight = 0f;
                statusBarBacking = new VisualElement { name = "Status bar paper", pickingMode = PickingMode.Ignore };
                statusBarBacking.style.position = Position.Absolute;
                statusBarBacking.style.left = 0f;
                statusBarBacking.style.right = 0f;
                statusBarBacking.style.top = 0f;
                statusBarBacking.style.backgroundColor = Paper;
                root.Add(statusBarBacking);
                screenHost = Column(safeRoot);
                screenHost.style.flexGrow = 1f;
                screenHost.style.flexShrink = 1f;
                screenHost.style.minHeight = 0f;
                navHost = Column(safeRoot);
                navHost.style.flexShrink = 0f;
                headerStringsHost = new VisualElement { name = "Header strings layer", pickingMode = PickingMode.Ignore };
                headerStringsHost.style.position = Position.Absolute;
                headerStringsHost.style.left = headerStringsHost.style.right = 0f;
                headerStringsHost.style.top = headerStringsHost.style.bottom = 0f;
                root.Add(headerStringsHost);
                overlayHost = new VisualElement { pickingMode = PickingMode.Ignore };
                overlayHost.style.position = Position.Absolute;
                overlayHost.style.left = 0f;
                overlayHost.style.right = 0f;
                overlayHost.style.top = 0f;
                overlayHost.style.bottom = 0f;
                root.Add(overlayHost);
                ApplySafeArea();
            }
        }

        private void OnControllerChanged()
        {
            AppState state = controller?.State;
            if (captureNoteRequested && state != null)
            {
                if (state.page != AppPage.Stick || state.accountOpen || !string.IsNullOrEmpty(state.error))
                    captureNoteRequested = false;
                else if (state.hasCapturedSpot && !state.capturingSpot)
                {
                    captureNoteRequested = false;
                    sheet = Sheet.Note;
                }
                else if (!state.capturingSpot) captureNoteRequested = false;
            }
            if (PaperFlow.ShouldClearPublishedDraft(publicationRequested, state))
            {
                publicationRequested = false;
                SyncDraftFromState();
                if (sheet == Sheet.Note) sheet = Sheet.None;
            }
            else if (publicationRequested && state != null && !state.busy && !string.IsNullOrEmpty(state.error))
            {
                publicationRequested = false;
            }
            if (state != null && state.accountOpen && accountScreen == AccountScreen.SignIn &&
                SignedIn(state) && returnAfterSignIn != Sheet.None)
            {
                RestoreSignInSheet();
                controller.SetAccountOpen(false);
                return;
            }
            if (state != null && state.page == AppPage.Stick && state.detail == null)
                lastPresentedDiscoveryId = null;
            if (PaperFlow.ShouldOpenCollectedDetail(renderedPage, state, lastPresentedDiscoveryId) &&
                state.celebrations.Pending == null)
            {
                lastPresentedDiscoveryId = state.detail.id;
                sheet = Sheet.Collected;
                sheetStickerId = state.detail.id;
            }
            QueueRender();
        }

        private void OnMapLoadingChanged() => QueueRender();

        private void QueueRender()
        {
            if (root == null || renderQueued)
            {
                return;
            }

            renderQueued = true;
            root.schedule.Execute(() =>
            {
                renderQueued = false;
                Render();
            });
        }

        private void Render()
        {
            if (controller == null || root == null)
            {
                return;
            }

            AppState state = controller.State;
            state.celebrations.SetAccount(state.user?.uid);
            if (CelebrationActive) sheet = Sheet.None;
            bool loginRequired = !SignedIn(state);
            statusBarBacking.style.display = loginRequired ? DisplayStyle.None : DisplayStyle.Flex;
            if (loginRequired)
            {
                if (!IsLegalSheet) sheet = Sheet.None;
                AbandonSignInReturn();
                controller.Map?.Hide();
            }
            if (!loginRequired && IsLegalSheet) sheet = Sheet.None;
            SyncHomeIdentity(state);
            SyncDeleteDesign(state);
            SyncHomePlacement(state);
            if (sheet == Sheet.ReferencePhoto && (state.page != AppPage.Stick || state.accountOpen ||
                state.selected?.id != sheetStickerId || PaperFlow.HasPlacementSelection(state)))
            {
                sheet = Sheet.None;
                sheetStickerId = null;
            }
            if (sheet == Sheet.HomeDesignPreview &&
                (state.page != AppPage.Home || HomeOwnedDesigns(state).All(design => design.id != sheetDesignId)))
            {
                sheet = Sheet.None;
                sheetDesignId = null;
            }
            if (state.creationOpen && !state.accountOpen && sheet == Sheet.None) sheet = Sheet.Picker;
            if (!state.creationOpen && (sheet == Sheet.Picker || sheet == Sheet.Creator ||
                (sheet == Sheet.DeleteDesign && !deleteDesignFromHome))) sheet = Sheet.None;
            if (state.accountOpen && !SignedIn(state) && accountScreen != AccountScreen.SignIn)
            {
                accountScreen = AccountScreen.SignIn;
            }
            else if (state.accountOpen && SignedIn(state) && accountScreen == AccountScreen.SignIn &&
                returnAfterSignIn == Sheet.None)
            {
                accountScreen = AccountScreen.Overview;
            }
            MapPresentation.SyncVisibility(state, sheet != Sheet.None || state.creationOpen || CelebrationActive, controller.Map);
            string identity = loginRequired ? "Login" : state.accountOpen ? "Account:" + (accountScreen == AccountScreen.SignIn && SignedIn(state) ? AccountScreen.Overview : accountScreen) : state.page.ToString();
            bool destinationChanged = identity != renderedIdentity;
            if (destinationChanged)
            {
                CaptureHomeScroll();
                DisposeLogin();
                if (!state.accountOpen && state.page == AppPage.Stick &&
                    renderedIdentity != null && renderedPage != AppPage.Stick)
                    cameraReturnPage = renderedPage == AppPage.Explore ? AppPage.Explore : AppPage.Home;
                mapRegion = null;
                activeDraftScroll = null;
                cameraCover = null;
                publishButton = null;
                homeProfileButton = null;
                homeCreateButton = null;
                homeBook = null;
                homeFooter = null;
                homeInvitation = null;
                homeScroll = null;
                homeCollectedSection = null;
                homeDesignSection = null;
                homeDesignGrid = null;
                homeDesignStatus = null;
                homePlacedSection = null;
                homePlacedList = null;
                homePlacedStatus = null;
                homeRetryPlacements = null;
                homePlacedSignIn = null;
                homeCollectedTab = null;
                homeDesignTab = null;
                homePlacedTab = null;
                homeRefreshDesigns = null;
                homeSignInButton = null;
                explorePreview = null;
                exploreLocationNotice = null;
                exploreMapMessage = null;
                exploreFindButton = null;
                stickActions = null;
                stickBookLabel = null;
                cameraSurface = null;
                stickSelectedArtworkButton = null;
                stickSelectedArtworkKey = null;
                stickSelectedArtworkPointerHeld = false;
                accountCollectionCount = null;
                accountAuthoredCount = null;
                authoredListHost = null;
                authoredWithdrawButtons.Clear();
                appleSignInButton = null;
                googleSignInButton = null;
                deleteButton = null;
                deleteField = null;
                screenStatusLabel = null;
                screenStatusNotice = null;
                screenStatusSymbol = null;
                screenStatusSettingsButton = null;
                statusSettingsButton = null;
                focusedField = null;
                SyncDraftFromState();
                screenHost.Clear();
                headerStringsHost.Clear();
                pageHeader = null;
                navHost.Clear();
                root.style.backgroundColor = Paper;
                if (loginRequired) BuildLogin(state);
                else if (state.accountOpen) BuildAccount(state);
                else
                {
                    if (state.page == AppPage.Home) BuildHome(state);
                    else if (state.page == AppPage.Stick) BuildStick(state);
                    else BuildExplore(state);
                    if (PaperFlow.ShowBottomNavigation(state)) BuildTabBar(state);
                }
                var reason = navigationMotion.Observe(identity, !state.accountOpen);
                EnterPageHeader(reason);
                renderedIdentity = identity;
            }
            else
            {
                if (loginRequired) RefreshLogin(state);
                else RefreshMounted(state);
            }

            string sheetSignature = sheet + ":" + sheetStickerId + ":" + sheetDesignId + ":" + sheetAuthorId + ":" + state.accountOpen;
            if (sheetSignature != renderedSheetSignature)
            {
                overlayHost.Clear();
                artworkNotices.RemoveAll(notice => notice.status.panel == null);
                bool hideCreationSheet = loginRequired && !IsLegalSheet || state.accountOpen &&
                    (sheet == Sheet.Picker || sheet == Sheet.Creator || sheet == Sheet.DeleteDesign);
                if (sheet != Sheet.None && !hideCreationSheet) BuildSheet(state);
                else
                {
                    sheetView = null;
                    sheetDetailHost = null;
                    sheetSubmitButton = null;
                    publishButton = null;
                    reportChoices.Clear();
                    statusLabel = screenStatusLabel;
                    statusNotice = screenStatusNotice;
                    statusSymbol = screenStatusSymbol;
                    statusSettingsButton = screenStatusSettingsButton;
                }
                renderedSheetSignature = sheetSignature;
            }
            else RefreshSheet(state);

            if (MapPresentation.ShouldShow(state, sheet != Sheet.None || state.creationOpen || CelebrationActive))
            {
                mapDirty = true;
            }
            headerStringsHost.style.display = CelebrationActive ? DisplayStyle.None : DisplayStyle.Flex;
            if (CelebrationActive) pageHeader?.Settle();
            RenderCelebration(state);
            ApplyTextScale();
            UpdateCameraInteraction();
            renderedPage = state.page;
            renderedAccountOpen = state.accountOpen;
        }

        private void RefreshMounted(AppState state)
        {
            UpdateStatus(state);
            if (homeProfileButton != null) homeProfileButton.tooltip = SignedIn(state) ? "Account settings" : "Sign in";
            if (state.accountOpen) RefreshAccount(state);
            else if (state.page == AppPage.Home) RefreshHome(state);
            else if (state.page == AppPage.Stick) RefreshStick(state);
            else RefreshExplore(state);
            RefreshCamera(state);
            RefreshPublish(state);
            mapDirty = state.page == AppPage.Explore;
        }

        private void ApplySafeArea()
        {
            if (safeRoot == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            lastSafeArea = Screen.safeArea;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            float scaleX = root.layout.width > 0f ? root.layout.width / Screen.width : 1f;
            float scaleY = root.layout.height > 0f ? root.layout.height / Screen.height : scaleX;
            float topInset = Mathf.Max(0f, (Screen.height - lastSafeArea.yMax) * scaleY);
            safeRoot.style.paddingTop = topInset;
            statusBarBacking.style.height = topInset;
            safeRoot.style.paddingBottom = Mathf.Max(lastSafeArea.yMin, lastKeyboardHeight) * scaleY;
            safeRoot.style.paddingLeft = Mathf.Max(0f, lastSafeArea.xMin * scaleX);
            safeRoot.style.paddingRight = Mathf.Max(0f, (Screen.width - lastSafeArea.xMax) * scaleX);
        }

        private void SyncDraftFromState()
        {
            if (controller == null) return;
            AppState state = controller.State;
            draftPlace = state.draftPlace ?? "";
            draftTeaser = state.draftTeaser ?? "";
            draftNote = state.draftNote ?? "";
        }

        private void BuildTabBar(AppState state)
        {
            VisualElement bar = Row(navHost);
            bar.AddToClassList("paper-nav");
            bar.style.minHeight = 80f;
            bar.style.flexShrink = 0f;
            bar.style.paddingLeft = 16f;
            bar.style.paddingRight = 16f;
            bar.style.borderTopWidth = 1f;
            bar.style.borderTopColor = Line;
            bar.style.backgroundColor = Paper;
            AddTab(bar, "Home", AppPage.Home, state.page == AppPage.Home);
            AddTab(bar, "STICK", AppPage.Stick, state.page == AppPage.Stick);
            AddTab(bar, "Explore", AppPage.Explore, state.page == AppPage.Explore);
        }

        private void AddTab(VisualElement bar, string title, AppPage page, bool selected)
        {
            PaperButton tab = new PaperButton("", () =>
            {
                if (controller.State.page == page && !controller.State.accountOpen) return;
                sheet = Sheet.None;
                controller.Navigate(page);
            }, PaperButtonKind.Quiet);
            tab.tooltip = "Go to " + title;
            tab.name = "Tab " + title;
            tab.AddToClassList("paper-nav-item");
            tab.AddToClassList(selected ? "nav-selected" : "nav-unselected");
            if (page == AppPage.Stick) tab.AddToClassList("nav-stick");
            Texture2D navigationTexture = Resources.Load<Texture2D>(PaperNavigationArt.ResourcePath(page));
            if (navigationTexture == null)
                throw new InvalidOperationException("Missing Taggi navigation art: " + PaperNavigationArt.ResourcePath(page));
            Image icon = new Image { image = navigationTexture, scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore };
            icon.name = "Taggi " + title + " navigation icon";
            icon.AddToClassList("nav-art");
            tab.Add(icon);
            var label = new Label(title);
            label.AddToClassList("nav-label");
            label.style.unityFont = SemiboldFont;
            tab.Add(label);
            if (selected && page != AppPage.Stick)
            {
                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.AddToClassList("nav-selection-marker");
                tab.Add(marker);
            }
            bar.Add(tab);
            tab.style.flexGrow = 1f;
            tab.style.marginLeft = 4f;
            tab.style.marginRight = 4f;
            tab.style.minHeight = 68f;
        }

        private void UpdateStatus(AppState state)
        {
            if (statusLabel == null) return;
            bool invitationAlreadyShown = (state.page == AppPage.Home && !state.accountOpen &&
                homeInvitation != null && statusLabel == screenStatusLabel) ||
                (state.page == AppPage.Stick && !state.accountOpen);
            string message = PaperFlow.StatusMessage(state, invitationAlreadyShown);
            bool settingsRequired = state.locationSettingsRequired;
            if (settingsRequired && string.IsNullOrWhiteSpace(message))
                message = "Check location access in Settings, then try again.";
            statusLabel.text = message ?? "";
            bool error = !string.IsNullOrWhiteSpace(state.error);
            statusNotice.style.display = string.IsNullOrWhiteSpace(message) ? DisplayStyle.None : DisplayStyle.Flex;
            statusNotice.EnableInClassList("error", error);
            statusNotice.EnableInClassList("success", !error && !string.IsNullOrEmpty(message) &&
                (message.Contains("published") || message.Contains("book") || message.Contains("received")));
            statusSymbol.Clear();
            statusSymbol.Add(new PaperIcon(error ? "warning" : "info", 18));
            statusLabel.style.color = error ? (Color)new Color32(125, 39, 31, 255) : Ink;
            if (statusSettingsButton != null)
            {
                statusSettingsButton.style.display = settingsRequired ? DisplayStyle.Flex : DisplayStyle.None;
                SetDisabled(statusSettingsButton, state.busy);
            }
        }

        private Label AddStatus(VisualElement parent, AppState state)
        {
            statusNotice = Column(parent);
            statusNotice.AddToClassList("paper-notice");
            statusNotice.style.alignItems = Align.Stretch;
            statusNotice.style.marginTop = 10f;
            statusNotice.style.marginBottom = 8f;
            VisualElement messageRow = Row(statusNotice);
            messageRow.style.alignItems = Align.Center;
            statusSymbol = Row(messageRow);
            statusLabel = Text(messageRow, "", 14, false, Ink);
            statusLabel.AddToClassList("notice-copy");
            statusSettingsButton = Action(statusNotice, "Open Settings", () => Application.OpenURL("app-settings:"), false);
            statusSettingsButton.name = "Open location settings";
            statusSettingsButton.style.alignSelf = Align.FlexStart;
            statusSettingsButton.style.marginLeft = 26f;
            statusSettingsButton.style.marginTop = 7f;
            if (overlayHost == null || !overlayHost.Contains(parent))
            {
                screenStatusLabel = statusLabel;
                screenStatusNotice = statusNotice;
                screenStatusSymbol = statusSymbol;
                screenStatusSettingsButton = statusSettingsButton;
            }
            UpdateStatus(state);
            return statusLabel;
        }

        private static bool SignedIn(AppState state)
        {
            return state.user != null && !string.IsNullOrEmpty(state.user.uid);
        }

        private static bool HasLocation(AppState state)
        {
            return state.location != null && state.location.accuracyMeters > 0f;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string Date(long unixSeconds)
        {
            if (unixSeconds <= 0) return "Date unavailable";
            try { return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("d MMM yyyy"); }
            catch (ArgumentOutOfRangeException) { return "Date unavailable"; }
        }

        private void OpenSignIn()
        {
            if (sheet == Sheet.Note || sheet == Sheet.Picker || sheet == Sheet.Creator || sheet == Sheet.Report || sheet == Sheet.Block)
            {
                returnAfterSignIn = sheet;
                returnStickerId = sheetStickerId;
                returnAuthorId = sheetAuthorId;
                returnFieldName = focusedField?.name ?? lastDraftFieldName;
                returnCursor = focusedField?.cursorIndex ?? lastDraftCursor;
                returnSelection = focusedField?.selectIndex ?? lastDraftSelection;
            }
            accountScreen = AccountScreen.SignIn;
            sheet = Sheet.None;
            controller.SetAccountOpen(true);
        }

        private void RestoreSignInSheet()
        {
            sheet = returnAfterSignIn;
            sheetStickerId = returnStickerId;
            sheetAuthorId = returnAuthorId;
            returnAfterSignIn = Sheet.None;
            returnStickerId = null;
            returnAuthorId = null;
        }

        private void AbandonSignInReturn()
        {
            returnAfterSignIn = Sheet.None;
            returnStickerId = null;
            returnAuthorId = null;
            returnFieldName = null;
            returnCursor = returnSelection = 0;
        }

        private static VisualElement Column(VisualElement parent)
        {
            VisualElement element = new VisualElement();
            element.style.flexDirection = FlexDirection.Column;
            parent.Add(element);
            return element;
        }

        private static VisualElement Row(VisualElement parent)
        {
            VisualElement element = new VisualElement();
            element.style.flexDirection = FlexDirection.Row;
            parent.Add(element);
            return element;
        }

        private static ScrollView PaperScroll(VisualElement parent)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList("paper-scroll");
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;
            parent.Add(scroll);
            return scroll;
        }

        private Label Text(VisualElement parent, string value, int size, bool bold = false, Color? color = null)
        {
            Label label = new Label(value);
            PaperTextRole role = PaperTypography.Role(size, bold);
            int pointSize = PaperTypography.PointSize(size, role);
            label.userData = pointSize;
            label.style.unityFont = role == PaperTextRole.Display ? DisplayFont :
                role == PaperTextRole.Heading ? HeadingFont :
                role == PaperTextRole.Emphasis ? SemiboldFont : BodyFont;
            label.style.fontSize = Mathf.RoundToInt(pointSize * textScale);
            label.style.color = color ?? Ink;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1f;
            parent.Add(label);
            return label;
        }

        private Button Action(VisualElement parent, string title, Action callback, bool filled = true, bool quiet = false)
        {
            Button button = new PaperButton(title, callback, filled ? PaperButtonKind.Primary :
                quiet ? PaperButtonKind.Quiet : PaperButtonKind.Secondary);
            button.userData = 15;
            button.style.minHeight = 44f;
            button.style.paddingLeft = 15f;
            button.style.paddingRight = 15f;
            button.style.fontSize = Mathf.RoundToInt(15f * textScale);
            button.style.unityFont = SemiboldFont;
            button.style.unityFontStyleAndWeight = FontStyle.Normal;
            parent.Add(button);
            return button;
        }

        private void ApplyTextScale()
        {
            if (root == null) return;
            root.style.fontSize = Mathf.RoundToInt(16f * textScale);
            root.Query<TextElement>().ForEach(element =>
            {
                if (element.userData is int baseSize) element.style.fontSize = Mathf.RoundToInt(baseSize * textScale);
            });
            root.Query<PaperField>().ForEach(field => field.ApplyScale(textScale));
            root.Query<Label>(className: "sheet-title").ForEach(label => label.style.fontSize =
                Mathf.RoundToInt(PaperTypography.PointSize(22, PaperTextRole.Heading) * textScale));
            root.Query<Label>(className: "nav-label").ForEach(label => label.style.fontSize = Mathf.RoundToInt(12f * textScale));
        }

        private static void SetDisabled(Button button, bool disabled)
        {
            button.SetEnabled(!disabled);
        }

        private static void Divider(VisualElement parent)
        {
            VisualElement line = new VisualElement();
            line.style.height = 1f;
            line.style.backgroundColor = Line;
            line.style.marginTop = 14f;
            line.style.marginBottom = 14f;
            parent.Add(line);
        }

        private sealed class ArtworkRequest
        {
            public readonly string presetId, designId, artworkUrl, thumbnailUrl;
            public readonly bool thumbnail;
            public ArtworkRequest(string presetId, string designId, string artworkUrl, string thumbnailUrl, bool thumbnail)
            {
                this.presetId = presetId;
                this.designId = designId;
                this.artworkUrl = artworkUrl;
                this.thumbnailUrl = thumbnailUrl;
                this.thumbnail = thumbnail;
            }
        }

        private sealed class ArtworkNotice
        {
            public readonly string designId;
            public readonly bool thumbnail;
            public readonly Image art;
            public readonly Label status;
            public readonly Button retry;
            public ArtworkNotice(string designId, bool thumbnail, Image art, Label status, Button retry)
            {
                this.designId = designId;
                this.thumbnail = thumbnail;
                this.art = art;
                this.status = status;
                this.retry = retry;
            }
        }

        private void AddArtworkNotice(VisualElement parent, Image art, string designId, bool thumbnail)
        {
            if (string.IsNullOrEmpty(designId)) return;
            artworkNotices.RemoveAll(notice => notice.status.panel == null);
            Label status = Text(parent, "", 13, false, Muted);
            status.style.marginTop = 4f;
            Button retry = Action(parent, "Retry artwork", controller.RefreshArtwork, false);
            retry.style.alignSelf = Align.FlexStart;
            retry.style.marginTop = 4f;
            artworkNotices.Add(new ArtworkNotice(designId, thumbnail, art, status, retry));
            RefreshArtworkNotices();
        }

        private void RefreshArtworkNotices()
        {
            artworkNotices.RemoveAll(notice => notice.status.panel == null);
            foreach (ArtworkNotice notice in artworkNotices)
            {
                bool loaded = notice.art.image != null;
                bool failed = !loaded && StickerArtwork.HasFailed(notice.designId, notice.thumbnail);
                bool loading = !loaded && StickerArtwork.IsLoading(notice.designId, notice.thumbnail);
                notice.status.text = loaded ? "" : failed ? "Artwork could not load." :
                    loading ? "Loading artwork…" : "Artwork is unavailable.";
                notice.status.style.display = loaded ? DisplayStyle.None : DisplayStyle.Flex;
                notice.retry.style.display = !loaded && !loading ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnArtworkChanged()
        {
            if (root == null) return;
            mapDirty = true;
            root.Query<Image>().ForEach(art =>
            {
                if (art.userData is ArtworkRequest request)
                {
                    art.image = StickerArtwork.Get(request.presetId, request.designId,
                        request.artworkUrl, request.thumbnailUrl, request.thumbnail);
                    if (!string.IsNullOrEmpty(request.designId))
                    {
                        art.style.backgroundColor = art.image == null ? Soft : Color.clear;
                        art.tooltip = art.image == null ? "Sticker artwork not loaded" : "Sticker artwork";
                    }
                }
            });
            RefreshArtworkNotices();
            TryRevealCelebration();
        }

        private Image Art(VisualElement parent, StickerSummary sticker, float size, bool thumbnail = true)
        {
            return Art(parent, sticker.presetId, sticker.designId, sticker.artworkUrl,
                sticker.thumbnailUrl, size, thumbnail, sticker.artworkWidth, sticker.artworkHeight);
        }

        private Image Art(VisualElement parent, StickerDesign design, float size, bool thumbnail = true)
        {
            return Art(parent, null, design.id, design.artworkUrl, design.thumbnailUrl,
                size, thumbnail, design.width, design.height);
        }

        private Image Art(VisualElement parent, string presetId, float size)
        {
            return Art(parent, presetId, null, null, null, size, true, 0, 0);
        }

        private Image Art(VisualElement parent, string presetId, string designId,
            string artworkUrl, string thumbnailUrl, float size, bool thumbnail, int imageWidth, int imageHeight)
        {
            Image art = new Image();
            art.name = string.IsNullOrEmpty(designId) ? "Taggi " + presetId : "Sticker artwork " + designId;
            art.userData = new ArtworkRequest(presetId, designId, artworkUrl, thumbnailUrl, thumbnail);
            art.image = StickerArtwork.Get(presetId, designId, artworkUrl, thumbnailUrl, thumbnail);
            if (art.image == null && !string.IsNullOrEmpty(presetId))
                art.image = Resources.Load<Texture2D>("Tagtag/Presets/" + presetId);
            if (!string.IsNullOrEmpty(designId))
            {
                art.style.backgroundColor = art.image == null ? Soft : Color.clear;
                art.tooltip = art.image == null ? "Sticker artwork not loaded" : "Sticker artwork";
            }
            art.scaleMode = ScaleMode.ScaleToFit;
            float width = size, height = size;
            if (!string.IsNullOrEmpty(designId) && imageWidth > 0 && imageHeight > 0)
            {
                float aspect = (float)imageWidth / imageHeight;
                if (aspect > 1f) height = size / aspect;
                else width = size * aspect;
            }
            art.style.width = width;
            art.style.height = height;
            art.style.alignSelf = Align.Center;
            parent.Add(art);
            return art;
        }
    }
}
