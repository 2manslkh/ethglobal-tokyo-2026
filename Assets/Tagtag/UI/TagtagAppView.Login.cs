using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private LoginBackdrop loginPlayback;
        private VisualElement loginBackdrop;
        private Label loginMessage;
        private Font appleFont;

        private void BuildLogin(AppState state)
        {
            loginBackdrop = new VisualElement { name = "Login video background", pickingMode = PickingMode.Ignore };
            loginBackdrop.AddToClassList("login-backdrop");
            root.Insert(0, loginBackdrop);
            loginPlayback = gameObject.AddComponent<LoginBackdrop>();
            loginPlayback.Mount(loginBackdrop, reducedMotion);

            var header = Row(screenHost);
            header.name = "Login header";
            header.AddToClassList("login-header");
            Text(header, "Tagtag", 29, true);

            var scroll = PaperScroll(screenHost);
            scroll.name = "Login scroll";
            scroll.AddToClassList("login-scroll");
            scroll.contentContainer.AddToClassList("login-content");
            // A viewport minimum makes short content bottom-aligned, while large text can scroll.
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(evt =>
                scroll.contentContainer.style.minHeight = evt.newRect.height);
            var footer = Column(scroll.contentContainer);
            footer.name = "Login actions";
            footer.AddToClassList("login-actions");
            var fade = new VisualElement { pickingMode = PickingMode.Ignore };
            fade.AddToClassList("login-fade");
            fade.generateVisualContent += context => DrawLoginFade(context, fade.contentRect);
            footer.Add(fade);
            Text(footer, "Keep the stickers you find", 29, true).AddToClassList("login-title");
            Text(footer, "Sign in to leave a sticker or add one to your book. Your collection follows this account.", 16)
                .AddToClassList("login-explanation");
            appleSignInButton = LoginProvider(footer, "apple", "Continue with Apple");
            googleSignInButton = LoginProvider(footer, "google", "Continue with Google");
            loginMessage = Text(footer, "", 14);
            loginMessage.name = "Login status";
            loginMessage.AddToClassList("login-status");
            RefreshLogin(state);
        }

        private Button LoginProvider(VisualElement parent, string provider, string caption)
        {
            var button = new Button(() => controller.SignIn(provider))
                { name = "Action " + caption, tooltip = caption };
            button.AddToClassList("login-provider");
            button.AddToClassList("auth-provider-" + provider);
            var logo = new Image { image = Resources.Load<Texture2D>("Tagtag/Login/" + (provider == "apple" ? "Apple" : "Google")),
                scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            logo.AddToClassList("login-provider-logo");
            button.Add(logo);
            var label = new Label(caption) { userData = 16, pickingMode = PickingMode.Ignore };
            label.AddToClassList("login-provider-label");
            if (provider == "apple")
            {
                if (appleFont == null) appleFont = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Helvetica", "Arial" }, 16);
                label.style.unityFont = appleFont;
            }
            else label.style.unityFont = Resources.Load<Font>("Tagtag/Fonts/GoogleSansMedium");
            button.Add(label);
            parent.Add(button);
            return button;
        }

        private void RefreshLogin(AppState state)
        {
            SetDisabled(appleSignInButton, state.busy || !state.servicesConfigured);
            SetDisabled(googleSignInButton, state.busy || !state.servicesConfigured);
            string message = !state.servicesConfigured ? "Sign-in is unavailable until the service is configured." :
                !string.IsNullOrEmpty(state.error) ? state.error : state.busy ?
                (string.IsNullOrEmpty(state.status) ? "Signing in…" : state.status) : "";
            loginMessage.text = message;
            loginMessage.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void DisposeLogin()
        {
            if (loginPlayback != null) { loginPlayback.enabled = false; Destroy(loginPlayback); loginPlayback = null; }
            loginBackdrop?.RemoveFromHierarchy();
            loginBackdrop = null;
            loginMessage = null;
        }

        private static void DrawLoginFade(MeshGenerationContext context, Rect rect)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            var mesh = context.Allocate(6, 12);
            for (int row = 0; row < 3; row++)
            {
                float y = row == 0 ? 0 : row == 1 ? 100 : rect.height;
                Color32 tint = new Color(1f, 254f / 255f, 250f / 255f, row == 0 ? 0 : .97f);
                mesh.SetNextVertex(new Vertex { position = new Vector3(0, y, Vertex.nearZ), tint = tint });
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.width, y, Vertex.nearZ), tint = tint });
            }
            foreach (ushort index in new ushort[] { 0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4 }) mesh.SetNextIndex(index);
        }
    }
}
