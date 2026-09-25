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
        private enum Sheet { None, Collected, Report, Block, Withdraw }

        private static readonly Color Paper = new Color32(255, 254, 250, 255);
        private static readonly Color Ink = new Color32(32, 32, 30, 255);
        private static readonly Color Muted = new Color32(92, 91, 85, 255);
        private static readonly Color Line = new Color32(226, 224, 215, 255);
        private static readonly Color Soft = new Color32(246, 245, 239, 255);
        private static readonly Color Yellow = new Color32(255, 225, 90, 255);
        private static readonly string[] Presets = { "taggi-1", "taggi-2", "taggi-3", "taggi-4" };
        private static readonly string[] ReportReasons = { "Harassment or hate", "Unsafe place", "Private information", "Spam or misleading", "Something else" };

        private ITagtagController controller;
        private UIDocument document;
        private PanelSettings ownedPanelSettings;
        private VisualElement root;
        private VisualElement safeRoot;
        private VisualElement mapRegion;
        private ScrollView activeDraftScroll;
        private Label statusLabel;
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
            if (controller != null)
            {
                QueueRender();
            }
        }

        private void OnDisable()
        {
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
                ownedPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                ownedPanelSettings.name = "Tagtag runtime panel";
                ownedPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ownedPanelSettings.referenceResolution = new Vector2Int(390, 844);
                ownedPanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                ownedPanelSettings.match = 0f;
                document.panelSettings = ownedPanelSettings;
            }

            root = document.rootVisualElement;
            if (root == null) return;
            root.style.flexGrow = 1f;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);
            root.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                ApplySafeArea();
                mapDirty = true;
            });
        }

        private void LoadPreferences()
        {
            textScale = Mathf.Clamp(PlayerPrefs.GetFloat("tagtag.textScale", 1f), 1f, 1.4f);
            reducedMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0) != 0;
        }

        private void OnControllerChanged()
        {
            AppState state = controller?.State;
            if (state != null && (state.page == AppPage.Stick || renderedPage == AppPage.Stick) && state.detail != null &&
                !string.IsNullOrEmpty(state.detail.id) && state.detail.id != lastPresentedDiscoveryId)
            {
                lastPresentedDiscoveryId = state.detail.id;
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
            if (accountScreen == AccountScreen.DeleteConfirmation && !SignedIn(state))
            {
                accountScreen = AccountScreen.SignIn;
            }
            if (focusedField != null && state.page == renderedPage && state.accountOpen == renderedAccountOpen)
            {
                UpdateStatus(state);
                return;
            }

            focusedField = null;
            SyncDraftFromState();
            if (!MapPresentation.ShouldShow(state, sheet != Sheet.None)) controller.Map?.Hide();
            mapRegion = null;
            activeDraftScroll = null;
            root.Clear();
            root.style.backgroundColor = state.page == AppPage.Stick && !state.accountOpen ? Color.clear : Paper;
            safeRoot = Column(root);
            safeRoot.style.flexGrow = 1f;
            safeRoot.style.minHeight = 0f;
            ApplySafeArea();

            if (state.accountOpen)
            {
                BuildAccount(state);
            }
            else
            {
                BuildTopBar(state);
                if (state.page == AppPage.Home) BuildHome(state);
                else if (state.page == AppPage.Stick) BuildStick(state);
                else BuildExplore(state);
                BuildTabBar(state);
            }

            if (sheet != Sheet.None)
            {
                BuildSheet(state);
            }

            if (MapPresentation.ShouldShow(state, sheet != Sheet.None))
            {
                mapDirty = true;
            }
            renderedPage = state.page;
            renderedAccountOpen = state.accountOpen;
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
            VisualElement bar = Row(safeRoot);
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
            profile.style.minWidth = 72f;
        }

        private void BuildTabBar(AppState state)
        {
            VisualElement bar = Row(safeRoot);
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
            Button tab = Action(bar, title, () =>
            {
                sheet = Sheet.None;
                controller.Navigate(page);
            }, selected);
            tab.style.flexGrow = 1f;
            tab.style.marginLeft = 4f;
            tab.style.marginRight = 4f;
            tab.style.minHeight = 48f;
            tab.style.backgroundColor = selected ? Yellow : Color.clear;
            tab.style.borderTopLeftRadius = 14f;
            tab.style.borderTopRightRadius = 14f;
            tab.style.borderBottomLeftRadius = 14f;
            tab.style.borderBottomRightRadius = 14f;
        }

        private void UpdateStatus(AppState state)
        {
            if (statusLabel == null) return;
            string message = !string.IsNullOrWhiteSpace(state.error) ? state.error : state.status;
            statusLabel.text = message ?? "";
            statusLabel.style.display = string.IsNullOrWhiteSpace(message) ? DisplayStyle.None : DisplayStyle.Flex;
            statusLabel.style.color = !string.IsNullOrWhiteSpace(state.error) ? (Color)new Color32(125, 39, 31, 255) : Muted;
        }

        private Label AddStatus(VisualElement parent, AppState state)
        {
            statusLabel = Text(parent, "", 14, false, Muted);
            statusLabel.style.marginTop = 10f;
            statusLabel.style.marginBottom = 8f;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
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
            accountScreen = AccountScreen.SignIn;
            sheet = Sheet.None;
            controller.SetAccountOpen(true);
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

        private Label Text(VisualElement parent, string value, int size, bool bold = false, Color? color = null)
        {
            Label label = new Label(value);
            label.style.fontSize = Mathf.RoundToInt(size * textScale);
            label.style.color = color ?? Ink;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1f;
            parent.Add(label);
            return label;
        }

        private Button Action(VisualElement parent, string title, Action callback, bool filled = true)
        {
            Button button = new Button(callback) { text = title, tooltip = title, name = "Action " + title };
            button.style.minHeight = 44f;
            button.style.paddingLeft = 15f;
            button.style.paddingRight = 15f;
            button.style.fontSize = Mathf.RoundToInt(15f * textScale);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.color = Ink;
            button.style.backgroundColor = filled ? Yellow : Color.clear;
            button.style.borderTopWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.borderTopLeftRadius = 14f;
            button.style.borderTopRightRadius = 14f;
            button.style.borderBottomLeftRadius = 14f;
            button.style.borderBottomRightRadius = 14f;
            parent.Add(button);
            return button;
        }

        private static void SetDisabled(Button button, bool disabled)
        {
            button.SetEnabled(!disabled);
            button.style.opacity = disabled ? 0.45f : 1f;
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
