using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StickerHunt
{
    public sealed class StickerHuntScreen : MonoBehaviour
    {
        private static readonly Color Background = new Color32(15, 21, 37, 255);
        private static readonly Color Card = new Color32(255, 255, 255, 255);
        private static readonly Color Mint = new Color32(130, 243, 195, 255);
        private static readonly Color White = new Color32(245, 249, 255, 255);
        private static readonly Color Muted = new Color32(155, 171, 192, 255);

        private RectTransform safeArea;
        private Rect lastSafeArea;
        private Font font;
        private Text statusText;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(events);
            }

            GameObject canvasObject = new GameObject("tagtag UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0;

            Panel(canvas.transform, "Background", Background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea = Panel(canvas.transform, "Safe Area", Background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ApplySafeArea();
            BuildContent();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            safeArea.anchorMin = lastSafeArea.position / new Vector2(Screen.width, Screen.height);
            safeArea.anchorMax = (lastSafeArea.position + lastSafeArea.size) / new Vector2(Screen.width, Screen.height);
        }

        private void BuildContent()
        {
            Text brand = Label(safeArea, "tagtag", 22, FontStyle.Bold, Mint,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -64), new Vector2(-24, -32));
            brand.alignment = TextAnchor.MiddleLeft;

            Text title = Label(safeArea, "The city is\nfull of stories.", 38, FontStyle.Bold, White,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -186), new Vector2(-24, -84));
            title.alignment = TextAnchor.MiddleLeft;

            Text subtitle = Label(safeArea, "Explore nearby places. Find their stickers. Build a collection that is yours.",
                16, FontStyle.Normal, Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(24, -262), new Vector2(-24, -194));
            subtitle.alignment = TextAnchor.UpperLeft;

            RectTransform feature = Panel(safeArea, "Featured discovery", Card,
                new Vector2(0, 0.31f), new Vector2(1, 0.67f), new Vector2(24, 0), new Vector2(-24, 0));
            GameObject portrait = new GameObject("Taggi portrait", typeof(RectTransform), typeof(RawImage));
            portrait.transform.SetParent(feature, false);
            RectTransform portraitRect = portrait.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 1);
            portraitRect.anchorMax = new Vector2(0.5f, 1);
            portraitRect.sizeDelta = new Vector2(110, 110);
            portraitRect.anchoredPosition = new Vector2(0, -62);
            portrait.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("Taggi");
            Text featureTitle = Label(feature, "Meet Taggi", 21, FontStyle.Bold, Background,
                new Vector2(0, 0.19f), new Vector2(1, 0.37f), new Vector2(16, 0), new Vector2(-16, 0));
            featureTitle.alignment = TextAnchor.MiddleCenter;
            Text featureNote = Label(feature, "Your sticker hunting companion", 14, FontStyle.Normal,
                new Color32(78, 94, 112, 255),
                new Vector2(0, 0.06f), new Vector2(1, 0.19f), new Vector2(12, 0), new Vector2(-12, 0));
            featureNote.alignment = TextAnchor.MiddleCenter;

            RectTransform button = Panel(safeArea, "Explore button", Mint,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 114), new Vector2(-24, 174));
            Button explore = button.gameObject.AddComponent<Button>();
            explore.targetGraphic = button.GetComponent<Image>();
            Text buttonText = Label(button, "Explore nearby", 18, FontStyle.Bold, Background,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            buttonText.alignment = TextAnchor.MiddleCenter;
            explore.onClick.AddListener(() => statusText.text = "Discovery map coming soon");

            statusText = Label(safeArea, "A new adventure starts here", 13, FontStyle.Normal, Muted,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 65), new Vector2(-16, 105));
            statusText.alignment = TextAnchor.MiddleCenter;
        }

        private static RectTransform Panel(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private Text Label(Transform parent, string content, int size, FontStyle style, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject label = new GameObject(content, typeof(RectTransform), typeof(Text));
            label.transform.SetParent(parent, false);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Text text = label.GetComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
