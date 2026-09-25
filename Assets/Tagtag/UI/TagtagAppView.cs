using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    [DisallowMultipleComponent]
    public sealed partial class TagtagAppView : MonoBehaviour
    {
        private enum AccountScreen { Overview, SignIn, Authored, DeleteConfirmation }
        private enum Sheet { None, Picker, Note, Collected, Report, Block, Withdraw }

        private static readonly Color Paper = new Color32(255, 254, 250, 255);
        private static readonly Color Ink = new Color32(32, 32, 30, 255);
        private static readonly Color Muted = new Color32(92, 91, 85, 255);
        private static readonly Color Line = new Color32(226, 224, 215, 255);
        private static readonly Color Soft = new Color32(246, 245, 239, 255);
        private static readonly Color Yellow = new Color32(255, 225, 90, 255);
        private Font bodyFont, semiboldFont, headingFont, displayFont;
        private Font BodyFont => bodyFont ?? (bodyFont = Resources.Load<Font>("Tagtag/Fonts/InstrumentRegular"));
        private Font SemiboldFont => semiboldFont ?? (semiboldFont = Resources.Load<Font>("Tagtag/Fonts/InstrumentSemibold"));
        private Font HeadingFont => headingFont ?? (headingFont = Resources.Load<Font>("Tagtag/Fonts/BricolageBold"));
        private Font DisplayFont => displayFont ?? (displayFont = Resources.Load<Font>("Tagtag/Fonts/BricolageExtraBold"));
        private static readonly string[] Presets = { "taggi-1", "taggi-2", "taggi-3", "taggi-4" };
        private static readonly string[] ReportReasons = { "Harassment or hate", "Unsafe place", "Private information", "Spam or misleading", "Something else" };

        private ITagtagController controller;
        private UIDocument document;
        private PanelSettings ownedPanelSettings;
        private VisualElement root;
        private VisualElement safeRoot;
        private VisualElement topHost;
        private VisualElement screenHost;
        private VisualElement navHost;
        private VisualElement overlayHost;
        private VisualElement cameraCover;
        private Label cameraTrackingLabel;
        private Button publishButton;
        private Button topProfileButton;
        private PaperNavigationMotion navigationMotion = new PaperNavigationMotion();
        private string renderedIdentity;
        private string renderedSheetSignature;
        private VisualElement mapRegion;
        private ScrollView activeDraftScroll;
        private Label statusLabel;
        private VisualElement statusNotice;
        private VisualElement statusSymbol;
        private Label screenStatusLabel;
        private VisualElement screenStatusNotice;
        private VisualElement screenStatusSymbol;
        private TextField focusedField;
        private Rect lastSafeArea;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private float lastKeyboardHeight;
        private int bookPage;
        private AccountScreen accountScreen;
        private Sheet sheet;
        private string sheetStickerId;
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
        private float textScale = 1f;
        private bool reducedMotion;
        private bool renderQueued;
        private bool mapDirty;
        private bool bookPressed;
        private Vector2 bookStart;
        private string pressedStickerId;
        private string lastPresentedDiscoveryId;
        private string pendingCollectionCommitId;
        private bool publicationRequested;
        private AppPage renderedPage;
        private bool renderedAccountOpen;

        public void Initialize(ITagtagController tagtagController)
        {
            if (controller != null)
            {
                controller.Changed -= OnControllerChanged;
            }

            controller = tagtagController ?? throw new ArgumentNullException(nameof(tagtagController));
            controller.Changed += OnControllerChanged;
            LoadPreferences();
            EnsureDocument();
            SyncDraftFromState();
            QueueRender();
        }

        private void Awake()
        {
            LoadPreferences();
            EnsureDocument();
        }

        private void OnEnable()
        {
            PaperMotion.SetPaused(false);
            if (controller != null)
            {
                QueueRender();
            }
        }

        private void OnDisable()
        {
            PaperMotion.SetPaused(true);
            controller?.Map?.Hide();
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.Changed -= OnControllerChanged;
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

            if (mapDirty && MapPresentation.ShouldShow(controller.State, sheet != Sheet.None))
            {
                UpdateMapLayout();
            }
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
                topHost = Column(safeRoot);
                screenHost = Column(safeRoot);
                screenHost.style.flexGrow = 1f;
                screenHost.style.minHeight = 0f;
                navHost = Column(safeRoot);
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

        private void LoadPreferences()
        {
            textScale = Mathf.Clamp(PlayerPrefs.GetFloat("tagtag.textScale", 1f), 1f, 1.4f);
            reducedMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0) != 0;
        }

        private void OnControllerChanged()
        {
            AppState state = controller?.State;
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
            if (PaperFlow.ShouldAnimateCollection(renderedPage, state, lastPresentedDiscoveryId))
            {
                lastPresentedDiscoveryId = state.detail.id;
                pendingCollectionCommitId = state.detail.id;
                sheet = Sheet.Collected;
                sheetStickerId = state.detail.id;
            }
            QueueRender();
        }

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
            if (state.accountOpen && !SignedIn(state) && accountScreen != AccountScreen.SignIn)
            {
                accountScreen = AccountScreen.SignIn;
            }
            else if (state.accountOpen && SignedIn(state) && accountScreen == AccountScreen.SignIn &&
                returnAfterSignIn == Sheet.None)
            {
                accountScreen = AccountScreen.Overview;
            }
            MapPresentation.SyncVisibility(state, sheet != Sheet.None, controller.Map);
            string identity = state.accountOpen ? "Account:" + (accountScreen == AccountScreen.SignIn && SignedIn(state) ? AccountScreen.Overview : accountScreen) : state.page.ToString();
            bool destinationChanged = identity != renderedIdentity;
            if (destinationChanged)
            {
                mapRegion = null;
                activeDraftScroll = null;
                cameraCover = null;
                cameraTrackingLabel = null;
                publishButton = null;
                topProfileButton = null;
                homeBook = null;
                homeFooter = null;
                homeInvitation = null;
                explorePreview = null;
                exploreLocationNotice = null;
                exploreMapMessage = null;
                exploreFindButton = null;
                stickGuidance = null;
                stickActions = null;
                accountCollectionCount = null;
                accountAuthoredCount = null;
                accountMotionSwitch = null;
                authoredListHost = null;
                authoredWithdrawButtons.Clear();
                appleSignInButton = null;
                googleSignInButton = null;
                textSizeChoices.Clear();
                deleteButton = null;
                deleteField = null;
                screenStatusLabel = null;
                screenStatusNotice = null;
                screenStatusSymbol = null;
                focusedField = null;
                SyncDraftFromState();
                topHost.Clear();
                screenHost.Clear();
                navHost.Clear();
                root.style.backgroundColor = Paper;
                if (state.accountOpen) BuildAccount(state);
                else
                {
                    BuildTopBar(state);
                    if (state.page == AppPage.Home) BuildHome(state);
                    else if (state.page == AppPage.Stick) BuildStick(state);
                    else BuildExplore(state);
                    BuildTabBar(state);
                }
                var reason = navigationMotion.Observe(identity, !state.accountOpen);
                PaperNavigationMotion.Enter(screenHost, reason);
                renderedIdentity = identity;
            }
            else
            {
                RefreshMounted(state);
            }

            string sheetSignature = sheet + ":" + sheetStickerId + ":" + sheetAuthorId;
            if (sheetSignature != renderedSheetSignature)
            {
                overlayHost.Clear();
                if (sheet != Sheet.None) BuildSheet(state);
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
                }
                renderedSheetSignature = sheetSignature;
            }
            else RefreshSheet(state);

            if (MapPresentation.ShouldShow(state, sheet != Sheet.None))
            {
                mapDirty = true;
            }
            ApplyTextScale();
            renderedPage = state.page;
            renderedAccountOpen = state.accountOpen;
        }

        private void RefreshMounted(AppState state)
        {
            UpdateStatus(state);
            if (topProfileButton != null) topProfileButton.text = SignedIn(state) ? "Account" : "Sign in";
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
            safeRoot.style.paddingTop = Mathf.Max(0f, (Screen.height - lastSafeArea.yMax) * scaleY);
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

        private void BuildTopBar(AppState state)
        {
            VisualElement bar = Row(topHost);
            bar.style.height = 62f;
            bar.style.paddingLeft = 24f;
            bar.style.paddingRight = 20f;
            bar.style.alignItems = Align.Center;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.backgroundColor = Paper;
            Label brand = Text(bar, "tagtag", 26, true, Ink);
            brand.style.letterSpacing = -1f;
            Button profile = Action(bar, SignedIn(state) ? "Account" : "Sign in", () =>
            {
                accountScreen = SignedIn(controller.State) ? AccountScreen.Overview : AccountScreen.SignIn;
                controller.SetAccountOpen(true);
            }, false);
            topProfileButton = profile;
            profile.style.minWidth = 72f;
        }

        private void BuildTabBar(AppState state)
        {
            VisualElement bar = Row(navHost);
            bar.AddToClassList("paper-nav");
            bar.style.height = 70f;
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
            tab.tooltip = title;
            tab.name = "Tab " + title;
            tab.AddToClassList("paper-nav-item");
            tab.AddToClassList(selected ? "nav-selected" : "nav-unselected");
            if (page == AppPage.Stick) tab.AddToClassList("nav-stick");
            tab.Add(new PaperIcon(page == AppPage.Home ? "home" : page == AppPage.Stick ? "stick" : "explore", 22));
            var label = new Label(title);
            label.AddToClassList("nav-label");
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
            tab.style.minHeight = 48f;
        }

        private void UpdateStatus(AppState state)
        {
            if (statusLabel == null) return;
            bool invitationAlreadyShown = state.page == AppPage.Home && !state.accountOpen &&
                homeInvitation != null && statusLabel == screenStatusLabel;
            string message = PaperFlow.StatusMessage(state, invitationAlreadyShown);
            statusLabel.text = message ?? "";
            bool error = !string.IsNullOrWhiteSpace(state.error);
            statusNotice.style.display = string.IsNullOrWhiteSpace(message) ? DisplayStyle.None : DisplayStyle.Flex;
            statusNotice.EnableInClassList("error", error);
            statusNotice.EnableInClassList("success", !error && !string.IsNullOrEmpty(message) &&
                (message.Contains("published") || message.Contains("book") || message.Contains("received")));
            statusSymbol.Clear();
            statusSymbol.Add(new PaperIcon(error ? "warning" : "info", 18));
            statusLabel.style.color = error ? (Color)new Color32(125, 39, 31, 255) : Ink;
        }

        private Label AddStatus(VisualElement parent, AppState state)
        {
            statusNotice = Row(parent);
            statusNotice.AddToClassList("paper-notice");
            statusNotice.style.marginTop = 10f;
            statusNotice.style.marginBottom = 8f;
            statusSymbol = Row(statusNotice);
            statusLabel = Text(statusNotice, "", 14, false, Ink);
            statusLabel.AddToClassList("notice-copy");
            if (overlayHost == null || !overlayHost.Contains(parent))
            {
                screenStatusLabel = statusLabel;
                screenStatusNotice = statusNotice;
                screenStatusSymbol = statusSymbol;
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
            if (sheet == Sheet.Note || sheet == Sheet.Report || sheet == Sheet.Block)
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
            label.userData = size;
            label.style.unityFont = size >= 30 && bold ? DisplayFont : size >= 20 && bold ? HeadingFont : bold ? SemiboldFont : BodyFont;
            label.style.fontSize = Mathf.RoundToInt(size * textScale);
            label.style.color = color ?? Ink;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1f;
            parent.Add(label);
            return label;
        }

        private Button Action(VisualElement parent, string title, Action callback, bool filled = true)
        {
            Button button = new PaperButton(title, callback, filled ? PaperButtonKind.Primary : PaperButtonKind.Quiet);
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
            root.Query<Label>(className: "sheet-title").ForEach(label => label.style.fontSize = Mathf.RoundToInt(22f * textScale));
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

        private Image Art(VisualElement parent, string presetId, float size)
        {
            Image art = new Image();
            art.name = "Taggi " + presetId;
            art.image = Resources.Load<Texture2D>("Tagtag/Presets/" + presetId);
            art.scaleMode = ScaleMode.ScaleToFit;
            art.style.width = size;
            art.style.height = size;
            art.style.alignSelf = Align.Center;
            parent.Add(art);
            return art;
        }
    }
}
