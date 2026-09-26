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

            var header = Column(screenHost);
            header.name = "Login header";
            header.AddToClassList("login-header");
            Text(header, "Tagtag", 29, true);

            var tagline = Text(header, "Find your places,\nCollect your moments", 18);
            tagline.name = "Login tagline";
            tagline.AddToClassList("login-tagline");

            var space = new VisualElement { pickingMode = PickingMode.Ignore };
            space.AddToClassList("login-space");
            screenHost.Add(space);
            var footer = Column(screenHost);
            footer.name = "Login actions";
            footer.AddToClassList("login-actions");
            appleSignInButton = LoginProvider(footer, "apple", "Continue with Apple");
            googleSignInButton = LoginProvider(footer, "google", "Continue with Google");
            var legal = Row(footer);
            legal.name = "Login legal links";
            legal.AddToClassList("login-legal");
            LoginLegalLink(legal, "Privacy Policy", Sheet.Privacy);
            LoginLegalLink(legal, "Terms & Conditions", Sheet.Terms);
            loginMessage = Text(footer, "", 14);
            loginMessage.name = "Login status";
            loginMessage.AddToClassList("login-status");
            RefreshLogin(state);
        }

        private bool IsLegalSheet => sheet == Sheet.Privacy || sheet == Sheet.Terms;

        private void LoginLegalLink(VisualElement parent, string caption, Sheet destination)
        {
            var button = new Button(() => { sheet = destination; Render(); })
                { name = "Login " + caption, text = caption, tooltip = caption, userData = 12 };
            button.AddToClassList("login-legal-link");
            parent.Add(button);
        }

        private void BuildLegalContent(VisualElement parent)
        {
            Text(parent, "Last updated 26 September 2026", 13, color: Muted);
            if (sheet == Sheet.Privacy)
            {
                LegalSection(parent, "Your account", "tagtag uses Apple or Google sign-in and Firebase Authentication to identify your account and keep your sticker collection associated with it.");
                LegalSection(parent, "Location and camera", "Your location is sent to the service to find nearby stickers and check placement and collection requests. Published stickers have a location visible to other users. Camera access supports AR placement and discovery. Publishing uploads a saved AR map and a still photo of the spot so others can find it.");
                LegalSection(parent, "What you share", "Sticker artwork, place names and teasers are visible to other users. A sticker’s note is revealed to users who complete discovery and collect it. Do not publish sensitive information or images of people without permission. Photo imports upload the finished sticker artwork, rather than the original source photo.");
                LegalSection(parent, "Storage and service providers", "Account records, stickers, notes, saved maps, artwork and collections are processed using Google Firebase and Google Cloud services. Collections and unfinished drafts may also be cached on your device. Reports and block preferences support moderation.");
                LegalSection(parent, "Your choices", "You can manage camera and location permissions in iOS Settings. Account controls let you withdraw published stickers and delete your account. Withdrawal leaves existing collected copies available; account deletion removes your account’s content through the service’s deletion process.");
            }
            else
            {
                LegalSection(parent, "Using tagtag", "Use tagtag responsibly to leave and discover stickers in places you are allowed to visit. Stay aware of your surroundings while using the camera. Never trespass, enter restricted areas or put yourself or others at risk to reach a sticker.");
                LegalSection(parent, "Your content", "Only share content you own or have permission to use. You allow tagtag to store and display the content you publish so the app can provide placement, discovery and collection. Do not publish unlawful, abusive, harassing or misleading content, or someone else’s private information.");
                LegalSection(parent, "Sharing and moderation", "Published artwork, locations and teasers can be seen by other users, and collected notes can be read by their collectors. Withdrawing a sticker does not remove existing collected copies. Content can be reported, and reported content may be removed through moderation.");
                LegalSection(parent, "Availability", "Location accuracy, camera tracking, network access and recognizable surroundings affect discovery. Sticker visibility and successful recovery are not guaranteed. Features may change as the app develops.");
                LegalSection(parent, "Your account", "Keep access to your sign-in account secure. You can stop using tagtag at any time and request account deletion from the account controls.");
            }
        }

        private void LegalSection(VisualElement parent, string title, string body)
        {
            Text(parent, title, 18, true).style.marginTop = 18f;
            Text(parent, body, 15).style.marginTop = 6f;
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
                !string.IsNullOrEmpty(state.error) ? state.error : "";
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

    }
}
