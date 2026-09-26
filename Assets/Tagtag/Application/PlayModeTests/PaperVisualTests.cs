using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tagtag.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tagtag.Tests
{
    // Deterministic UI captures. Native imagery, the software keyboard and AR are verified on device.
    public sealed class PaperVisualTests
    {
        private GameObject host;
        private ReviewController controller;
        private UIDocument document;
        private RenderTexture target;
        private float oldScale;
        private int oldMotion;

        [UnityTest]
        public IEnumerator LocationSettingsRecoveryPreservesTheNoteAndShowsPublishProgress()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            controller.State.user = new UserSession { uid = "review", displayName = "Aki" };
            controller.SelectPreset("taggi-1");
            controller.Camera.HasPlacementPreview = true;
            controller.Navigate(AppPage.Stick);
            host = new GameObject("Location settings review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(390, 844, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            yield return new WaitForSecondsRealtime(.4f);
            Submit("STICK Write note");
            yield return new WaitForSecondsRealtime(.4f);
            var note = document.rootVisualElement.Q<TextField>("Your note");
            note.value = "A quiet spot beside the river.";
            controller.State.error = "Turn on Precise Location for tagtag in Settings, then try again. Your draft is safe.";
            controller.State.locationSettingsRequired = true;
            controller.Notify();
            yield return Capture("publish-precise-location-settings");
            var settings = document.rootVisualElement.Q<PaperSheet>().Q<Button>("Open location settings");
            Assert.That(settings, Is.Not.Null, "Precision permission failure needs an actionable Settings control.");
            Assert.That(settings.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(settings.enabledInHierarchy, Is.True);
            Assert.That(document.rootVisualElement.Q<TextField>("Your note"), Is.SameAs(note));
            Assert.That(note.value, Is.EqualTo("A quiet spot beside the river."));
            controller.State.locationSettingsRequired = false;
            controller.State.error = "";
            controller.State.status = "Uploading sticker…";
            note.Focus();
            note.SelectRange(2, 8);
            yield return null;
            controller.State.busy = true;
            controller.Notify();
            yield return Capture("publish-upload-progress");
            Assert.That(settings.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(document.rootVisualElement.Q<PaperSheet>().Query<Label>().ToList().Any(label => label.text == "Uploading sticker…"), Is.True);
            Assert.That(document.rootVisualElement.Q<Button>("Action Publish sticker").enabledInHierarchy, Is.False);
            Assert.That(note.enabledInHierarchy, Is.False, "An in-flight publication must retain its submitted draft.");
            controller.State.busy = false;
            controller.Notify();
            yield return null;
            yield return null;
            Assert.That(note.enabledInHierarchy, Is.True);
            Assert.That(note.value, Is.EqualTo("A quiet spot beside the river."));
            Assert.That(note.cursorIndex, Is.EqualTo(2));
            Assert.That(note.selectIndex, Is.EqualTo(8));
        }

        [UnityTest]
        public IEnumerator ButtonPointerStatesKeepBordersAndContentsStationary()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            host = new GameObject("Button state review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(390, 844, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color32(255, 254, 250, 255);
            yield return null;
            var app = document.rootVisualElement;
            Assert.That(app.ClassListContains("paper-app"), Is.True);
            var sampleHost = new VisualElement { name = "Button state samples" };
            sampleHost.style.position = Position.Absolute;
            sampleHost.style.left = 20f;
            sampleHost.style.right = 20f;
            sampleHost.style.top = 70f;
            sampleHost.style.backgroundColor = Color.white;
            app.Add(sampleHost);
            var samples = new List<Button>
            {
                new PaperButton("", null), new PaperButton("", null, PaperButtonKind.Primary),
                new PaperButton("", null, PaperButtonKind.Quiet),
                new PaperButton("", null, PaperButtonKind.Destructive),
                new PaperSelection("", true, null), new PaperButton("", null)
            };
            samples[5].AddToClassList("auth-provider-apple");
            for (int i = 0; i < samples.Count; i++)
            {
                Button button = samples[i];
                button.name = "Button state sample " + i;
                button.style.height = 64f;
                button.style.flexDirection = FlexDirection.Row;
                button.style.alignItems = Align.Center;
                button.style.justifyContent = Justify.Center;
                button.Add(new Image { name = "Sample artwork", image = Resources.Load<Texture2D>("Tagtag/Presets/taggi-1"),
                    style = { width = 30f, height = 30f } });
                button.Add(new Label("Stable label") { name = "Sample caption", style = { marginLeft = 8f } });
                sampleHost.Add(button);
            }
            yield return Capture("buttons-default");
            var pseudo = typeof(VisualElement).GetProperty("pseudoStates", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pseudo, Is.Not.Null, "Fixture must exercise the actual USS pseudo states.");
            foreach (Button button in samples)
            {
                Rect art = button.Q<Image>("Sample artwork").worldBound;
                Rect caption = button.Q<Label>("Sample caption").worldBound;
                float border = button.resolvedStyle.borderLeftWidth;
                Assert.That(border, Is.Zero, button.name + " should be borderless.");
                object original = pseudo.GetValue(button);
                foreach (string state in new[] { "Hover", "Hover, Active", "Hover, Active, Focus", "Focus" })
                {
                    long flags = Convert.ToInt64(original) | Convert.ToInt64(Enum.Parse(pseudo.PropertyType, state));
                    pseudo.SetValue(button, Enum.ToObject(pseudo.PropertyType, flags));
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.That(button.resolvedStyle.borderLeftWidth, Is.EqualTo(border), button.name + " " + state + " adds an outline");
                    Assert.That(button.Q<Image>("Sample artwork").worldBound, Is.EqualTo(art), button.name + " " + state + " moves artwork");
                    Assert.That(button.Q<Label>("Sample caption").worldBound, Is.EqualTo(caption), button.name + " " + state + " moves caption");
                }
                pseudo.SetValue(button, original);
            }
            foreach (string state in new[] { "Hover", "Hover, Active, Focus", "Focus" })
            {
                foreach (Button button in samples) pseudo.SetValue(button, Enum.Parse(pseudo.PropertyType, state));
                yield return Capture(state == "Hover" ? "buttons-hover" : state == "Focus" ? "buttons-focused" : "buttons-pressed");
            }
        }

        [UnityTest]
        public IEnumerator CameraInventoryFlowUsesFullScreenAndExplicitPlacement()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            controller.Navigate(AppPage.Explore);
            host = new GameObject("Camera inventory review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(390, 844, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color32(218, 225, 222, 255);
            yield return null;
            controller.Navigate(AppPage.Stick);
            yield return Capture("camera-fullscreen-preparing");
            Assert.That(document.rootVisualElement.Q<Button>("Tab Home"), Is.Null, "Camera mode must remove the bottom navigation.");
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label => label.text == "tagtag"), Is.False,
                "Camera mode must remove the generic app header.");
            Assert.That(document.rootVisualElement.Q<Button>("STICK Close"), Is.Not.Null);
            var close = document.rootVisualElement.Q<Button>("STICK Close");
            Assert.That(close.layout.width, Is.EqualTo(48f).Within(.1f));
            Assert.That(close.layout.height, Is.EqualTo(48f).Within(.1f));
            Assert.That(close.tooltip, Is.EqualTo("Close camera"));
            Assert.That(controller.Camera.InteractionBlocked, Is.True);
            controller.Camera.CameraPresentation = CameraPresentationState.Live;
            controller.Camera.IsTracking = true;
            controller.Notify();
            yield return Capture("camera-fullscreen-idle");
            var inventory = document.rootVisualElement.Q<Button>("STICK Inventory");
            Assert.That(inventory, Is.Not.Null);
            Assert.That(inventory.layout.width, Is.GreaterThanOrEqualTo(88f));
            Assert.That(inventory.layout.height, Is.GreaterThanOrEqualTo(88f));
            Submit("STICK Inventory");
            yield return Capture("camera-inventory");
            Assert.That(controller.Camera.InteractionBlocked, Is.True, "The inventory must block all camera input.");
            Assert.That(document.rootVisualElement.Query<Button>().ToList().Count(button =>
                button.name != null && button.name.StartsWith("Inventory Taggi pose ")), Is.EqualTo(4));
            foreach (var choice in document.rootVisualElement.Query<Button>().ToList().Where(button =>
                button.name != null && button.name.StartsWith("Inventory Taggi pose ")))
            {
                var artwork = choice.Q<Image>();
                var caption = choice.Query<Label>().ToList().FirstOrDefault(label => label.text.StartsWith("Taggi pose "));
                Assert.That(caption, Is.Not.Null, "Inventory captions must occupy their own layout below the art.");
                Assert.That(caption.worldBound.yMin, Is.GreaterThanOrEqualTo(artwork.worldBound.yMax - 1f));
            }
            Submit("Inventory Taggi pose 2");
            yield return Capture("camera-finding-surface");
            Assert.That(controller.State.selectedPreset, Is.EqualTo("taggi-2"));
            Assert.That(controller.Camera.PlaceCalls, Is.Zero, "Selecting inventory art must not place it automatically.");
            var write = document.rootVisualElement.Q<Button>("STICK Write note");
            Assert.That(write == null || !write.enabledSelf, Is.True, "Place a preview before writing its note.");
            controller.Camera.HasPlacementSurface = true;
            controller.Notify();
            yield return Capture("camera-surface-ready");
            Submit("STICK Inventory");
            yield return new WaitForSecondsRealtime(.4f);
            yield return TapCameraSurface();
            Assert.That(controller.Camera.PlaceCalls, Is.Zero, "Touches behind the inventory must not place a sticker.");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            yield return TapCameraSurface();
            controller.Notify();
            yield return Capture("camera-adjusting-preview");
            Assert.That(controller.Camera.PlaceCalls, Is.EqualTo(1));
            Assert.That(controller.Camera.CanPublish, Is.False, "Fixture keeps mapping incomplete to test independent note access.");
            Assert.That(document.rootVisualElement.Q("STICK Adjustments"), Is.Null);
            var card = document.rootVisualElement.Q("STICK title sticker");
            Assert.That(card.Query<Label>().ToList().Select(label => label.text), Is.EqualTo(new[] { "Place Sticker" }));
            Assert.That(card.Query<Image>().ToList(), Is.Empty);
            Assert.That(document.rootVisualElement.Q("STICK Placement Guidance").worldBound.yMin,
                Is.GreaterThanOrEqualTo(card.worldBound.yMax));
            Submit("STICK Write note");
            yield return Capture("camera-note-after-placement");
            Assert.That(document.rootVisualElement.Q<TextField>("Your note"), Is.Not.Null);
            Assert.That(controller.Camera.InteractionBlocked, Is.True);
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("STICK Close");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Explore), "Close returns to the camera entry destination.");
            Assert.That(document.rootVisualElement.Q<Button>("Tab Explore"), Is.Not.Null);
        }

        private IEnumerator TapCameraSurface()
        {
            var surface = document.rootVisualElement.Q("STICK Camera Surface");
            Assert.That(surface, Is.Not.Null);
            var center = surface.worldBound.center;
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = center, button = 0 }))
            { down.target = surface; surface.SendEvent(down); }
            yield return null;
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = center, button = 0 }))
            { up.target = surface; surface.SendEvent(up); }
        }

        [UnityTest]
        public IEnumerator CapturesPaperScreensAndRecoveryStates()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 0);
            controller = new ReviewController();
            host = new GameObject("Paper visual review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(390, 844, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color32(218, 225, 222, 255);
            yield return Capture("home-empty");
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label => label.text == "tagtag"), Is.False);
            Submit("Home Profile");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.Q<Button>("Action Continue with Apple"), Is.Not.Null);
            Submit("Continue exploring");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label =>
                label.text == "Find your places, Collect your moments"), Is.True);
            foreach (string caption in new[] { "Previous", "Next", "Explore nearby" })
            {
                var button = document.rootVisualElement.Query<Button>().ToList().First(item => item.text == caption);
                var textSize = button.MeasureTextSize(caption, 0, VisualElement.MeasureMode.Undefined,
                    0, VisualElement.MeasureMode.Undefined);
                Assert.That(button.contentRect.width + 1f, Is.GreaterThanOrEqualTo(textSize.x),
                    caption + " must have room for a readable label.");
            }
            var collectedNumber = document.rootVisualElement.Q<Label>("home-collected-number");
            var collectedCaption = document.rootVisualElement.Q<Label>("home-collected-label");
            Assert.That(collectedNumber, Is.Not.Null);
            Assert.That(collectedNumber.text, Is.EqualTo("0"));
            Assert.That(collectedNumber.resolvedStyle.fontSize, Is.GreaterThan(collectedCaption.resolvedStyle.fontSize * 1.5f));
            var brand = document.rootVisualElement.Query<Label>().ToList().First(label => label.text == "Your sticker book");
            Assert.That(brand.resolvedStyle.unityFont, Is.SameAs(Resources.Load<Font>("Tagtag/Fonts/ShadowsIntoLight")));
            foreach (var label in document.rootVisualElement.Query<Label>(className: "nav-label").ToList())
                Assert.That(label.resolvedStyle.unityFont, Is.SameAs(Resources.Load<Font>("Tagtag/Fonts/InstrumentSemibold")));
            foreach (var title in new[] { "Home", "STICK", "Explore" })
            {
                var tab = document.rootVisualElement.Q<Button>("Tab " + title);
                Assert.That(tab.Q<Image>().image, Is.Not.Null, "Taggi artwork must be included in the player.");
                Assert.That(tab.layout.height, Is.GreaterThanOrEqualTo(44f));
            }

            controller.State.user = new UserSession { uid = "review", displayName = "Aki" };
            for (int i = 0; i < 21; i++) controller.State.collection.Add(Sticker(i));
            controller.Notify();
            yield return Capture("home-populated");
            Assert.That(document.rootVisualElement.Q<Label>("home-collected-number"), Is.SameAs(collectedNumber));
            Assert.That(collectedNumber.text, Is.EqualTo("21"));

            var bookCell = document.rootVisualElement.Query<VisualElement>().ToList().First(e => e.userData as string == "review-0");
            bookCell.Focus();
            using (var key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return }))
            { key.target = bookCell; bookCell.SendEvent(key); }
            yield return Capture("collected-long-note");
            controller.State.detail = Sticker(0);
            controller.State.detail.unavailable = true;
            controller.State.detail.note = "";
            controller.State.collection[0] = controller.State.detail;
            controller.Notify();
            yield return Capture("collected-revoked-after-sync");
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label => label.text == "This note is no longer available."), Is.True);
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label => label.text == Sticker(0).note), Is.False);
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            var refreshedBookCell = document.rootVisualElement.Query<VisualElement>().ToList().First(e => e.userData as string == "review-0");
            Assert.That(refreshedBookCell, Is.Not.SameAs(bookCell), "Sync replaces the book cell when availability changes.");
            Assert.That(document.rootVisualElement.panel.focusController.focusedElement, Is.SameAs(refreshedBookCell));
            controller.State.accountOpen = true;
            controller.Notify();
            yield return Capture("account");
            Submit("Manage your stickers");
            yield return Capture("authored-empty");
            controller.State.authored.Add(Sticker(0));
            controller.Notify();
            yield return Capture("authored-populated");
            Submit("Withdraw this sticker");
            yield return Capture("withdraw-confirmation");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Back");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Delete account and stickers");
            yield return Capture("delete-disabled");
            var deleteField = document.rootVisualElement.Q<TextField>("Delete confirmation");
            deleteField.value = "DELETE";
            deleteField.Focus();
            controller.State.error = "Please try again. Your account has not been deleted.";
            controller.Notify();
            yield return Capture("delete-error-focused");
            Assert.That(document.rootVisualElement.Q<TextField>("Delete confirmation"), Is.SameAs(deleteField));
            Submit("Keep my account");
            controller.State.error = "";
            controller.State.user = null;
            controller.Notify();
            yield return Capture("sign-in");

            controller.State.accountOpen = false;
            controller.Navigate(AppPage.Stick);
            yield return Capture("camera-preparing");
            controller.Camera.CameraPresentation = CameraPresentationState.PermissionDenied;
            controller.Notify();
            yield return Capture("camera-denied");
            controller.Camera.CameraPresentation = CameraPresentationState.Interrupted;
            controller.Notify();
            yield return Capture("camera-interrupted");
            controller.Camera.CameraPresentation = CameraPresentationState.Unavailable;
            controller.Notify();
            yield return Capture("camera-unavailable");
            controller.Camera.CameraPresentation = CameraPresentationState.Live;
            controller.Notify();
            yield return Capture("camera-live-guidance");
            controller.State.selected = Sticker(0);
            controller.Notify();
            yield return Capture("camera-discovery");
            StringAssert.Contains("Look beside the little red bridge.", document.rootVisualElement.Q<Label>("STICK Placement Guidance").text);
            controller.State.selected = null;
            controller.Notify();
            Assert.That(document.rootVisualElement.Q("Opaque camera cover").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Submit("STICK Inventory");
            yield return Capture("pose-picker");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            controller.SelectPreset("taggi-2");
            controller.Camera.HasPlacementPreview = true;
            controller.Notify();
            for (int frame = 0; frame < 5; frame++) yield return null;
            Submit("Write note");
            yield return Capture("note-empty-disabled");
            var fields = document.rootVisualElement.Query<TextField>().ToList();
            Assert.That(fields.Count, Is.EqualTo(3));
            fields[0].value = "川沿いの小さなベンチ";
            fields[1].value = "Look beside the little red bridge.";
            fields[2].value = Sticker(0).note;
            fields[2].Focus();
            fields[2].SelectRange(5, 16);
            yield return Capture("note-long-focused");
            var noteFocus = document.rootVisualElement.panel.focusController.focusedElement;
            var noteScroll = document.rootVisualElement.Q<PaperSheet>().Scroll.scrollOffset;
            int cursor = fields[2].cursorIndex, selection = fields[2].selectIndex;
            controller.State.busy = true;
            controller.State.error = "The connection was interrupted. Your draft is still here.";
            controller.Notify();
            yield return Capture("note-busy-error");
            Assert.That(document.rootVisualElement.Query<TextField>().ToList()[2], Is.SameAs(fields[2]), "Status updates must keep the mounted field.");
            Assert.That(fields[2].value, Is.EqualTo(Sticker(0).note));
            Assert.That(fields[2].enabledInHierarchy, Is.False, "Publication locks editing without replacing the draft field.");
            // Unity clears active selection when disabled; verify its restoration after the operation.
            Assert.That(document.rootVisualElement.Q<PaperSheet>().Scroll.scrollOffset, Is.EqualTo(noteScroll));
            controller.State.busy = false;
            controller.State.error = "";
            controller.Notify();
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(fields[2].enabledInHierarchy, Is.True);
            Assert.That(document.rootVisualElement.panel.focusController.focusedElement, Is.SameAs(noteFocus));
            Assert.That(fields[2].cursorIndex, Is.EqualTo(cursor));
            Assert.That(fields[2].selectIndex, Is.EqualTo(selection));
            Submit("Sign in to publish");
            yield return Capture("note-sign-in-return");
            Assert.That(document.rootVisualElement.Q<TextField>("Your note"), Is.Null,
                "Sign-in must leave the note sheet before returning to the draft.");
            controller.SignIn("apple");
            yield return Capture("note-signed-in-restored");
            Assert.That(document.rootVisualElement.Q<TextField>("Your note").value, Is.EqualTo(Sticker(0).note));
            controller.Camera.IsTracking = true;
            controller.Camera.HasPlacementPreview = true;
            controller.Camera.HasTrackedPlacement = true;
            controller.Camera.CanPublish = false;
            controller.Notify();
            yield return null;
            var publishButton = document.rootVisualElement.Q<Button>("Action Publish sticker");
            var publishReadiness = document.rootVisualElement.Q<Label>("Publish readiness");
            Assert.That(publishButton.enabledInHierarchy, Is.False);
            Assert.That(publishReadiness.text,
                Is.EqualTo("Scan around Taggi from more angles until the spatial map is ready."));
            var noteSheet = document.rootVisualElement.Q<PaperSheet>();
            noteSheet.Scroll.ScrollTo(publishReadiness);
            yield return null;
            yield return null;
            Assert.That(publishReadiness.worldBound.yMin, Is.GreaterThanOrEqualTo(noteSheet.Scroll.worldBound.yMin));
            Assert.That(publishReadiness.worldBound.yMax, Is.LessThanOrEqualTo(noteSheet.Scroll.worldBound.yMax + 1f));
            yield return Capture("publish-readiness-map-blocked");
            controller.Camera.CanPublish = true;
            controller.Notify();
            yield return null;
            yield return null;
            Assert.That(publishButton.enabledInHierarchy, Is.True,
                "A completed draft with a publishable AR placement must enable Publish sticker.");
            Assert.That(publishReadiness.text,
                Is.EqualTo("Ready to publish. Location is checked after you tap."));
            Submit("Publish sticker");
            Submit("Publish sticker");
            Assert.That(controller.PublishCount, Is.EqualTo(1), "Repeated activation in one frame must publish once.");
            controller.State.selectedPreset = "";
            controller.State.draftPlace = controller.State.draftTeaser = controller.State.draftNote = "";
            controller.Notify();
            yield return new WaitForSecondsRealtime(.4f);
            controller.SelectPreset("taggi-3");
            controller.Camera.HasPlacementPreview = true;
            controller.Notify();
            yield return new WaitForSecondsRealtime(.4f);
            if (document.rootVisualElement.Q<PaperSheet>() == null) Submit("Write note");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.Q<TextField>("Your note").value, Is.Empty,
                "A successful publication must clear the next placement draft.");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.panel.focusController.focusedElement,
                Is.SameAs(document.rootVisualElement.Q<Button>("STICK Write note")), "Dismissal returns focus to the trigger.");

            controller.SignOut();
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Write note");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Sign in to publish");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Continue exploring");
            yield return new WaitForSecondsRealtime(.4f);
            controller.Navigate(AppPage.Home);
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Home Profile");
            yield return new WaitForSecondsRealtime(.4f);
            controller.SignIn("apple");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.Q<PaperSheet>(), Is.Null,
                "An abandoned publish sign-in must not reopen the old sheet during a later sign-in.");
            Submit("Back");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Home Profile");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.Q<ScrollView>("Account scroll"), Is.Not.Null);
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label => label.text == "Aki"), Is.True);
            Submit("Back");

            controller.Navigate(AppPage.Explore);
            yield return Capture("explore-empty");
            controller.State.nearbyLoading = true;
            controller.Notify();
            yield return Capture("explore-location-loading");
            Submit("Tab Home");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home), "Nearby loading must leave navigation reachable.");
            controller.State.nearbyLoading = false;
            controller.Navigate(AppPage.Explore);
            controller.State.selected = Sticker(0);
            controller.State.nearby.Add(controller.State.selected);
            controller.Notify();
            yield return Capture("explore-teaser");
            var discoveryButton = document.rootVisualElement.Query<Button>().ToList().First(button => button.text == "Find in AR");
            controller.State.busy = true;
            controller.Notify();
            yield return null;
            yield return null;
            Assert.That(discoveryButton.enabledSelf, Is.False, "Foreground operations must visibly disable rejected discovery actions.");
            controller.State.busy = false;
            controller.State.nearbyLoading = true;
            controller.Notify();
            yield return null;
            yield return null;
            Assert.That(discoveryButton.enabledSelf, Is.True, "Nearby reads must not disable discovery.");
            controller.State.nearbyLoading = false;
            controller.Notify();
            Submit("Report");
            yield return Capture("report-sheet");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Block author");
            yield return Capture("block-sheet");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            controller.State.selected = null;
            controller.State.error = "Nearby stickers could not load. Check your connection and try again.";
            controller.Notify();
            yield return Capture("explore-error");

            // Real preferences are applied by a fresh view, as on app restart.
            document.panelSettings.targetTexture = null;
            UnityEngine.Object.Destroy(host);
            yield return null;
            PlayerPrefs.SetFloat("tagtag.textScale", 1.4f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller.State.error = "";
            controller.State.user = new UserSession { uid = "review", displayName = "Aki" };
            controller.Navigate(AppPage.Home);
            host = new GameObject("Compact paper visual review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            UnityEngine.Object.Destroy(target);
            target = new RenderTexture(320, 568, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color32(218, 225, 222, 255);
            yield return Capture("home-compact-largest-reduced-motion");
            controller.State.accountOpen = true;
            controller.Notify();
            yield return Capture("account-compact-largest-reduced-motion");
            controller.State.accountOpen = false;
            controller.State.selected = Sticker(0);
            controller.State.selected.teaser = new string('W', 180);
            controller.Navigate(AppPage.Explore);
            yield return Capture("explore-compact-largest-long-clue");
            var teaserScroll = document.rootVisualElement.Q<ScrollView>("Explore teaser scroll");
            Assert.That(teaserScroll, Is.Not.Null);
            foreach (var element in new VisualElement[] { document.rootVisualElement, teaserScroll.parent.parent.parent,
                teaserScroll.parent.parent, document.rootVisualElement.Q<Button>("Tab Explore").parent.parent,
                teaserScroll.parent, document.rootVisualElement.Q("Native MapKit region"), teaserScroll,
                document.rootVisualElement.Q<Button>("Tab Explore").parent, document.rootVisualElement.Q<Button>("Tab Explore") })
                Debug.Log("Compact geometry " + element.name + " " + string.Join(",", element.GetClasses()) + " bounds=" + element.worldBound);
            foreach (var label in document.rootVisualElement.Query<Label>(className: "nav-label").ToList())
                Assert.That(label.worldBound.yMax, Is.LessThanOrEqualTo(document.rootVisualElement.worldBound.yMax), "Navigation labels remain inside the compact viewport.");
            Assert.That(teaserScroll.worldBound.yMax, Is.LessThanOrEqualTo(document.rootVisualElement.Q<Button>("Tab Explore").worldBound.yMin + 1f));
            var longClue = document.rootVisualElement.Query<Label>().ToList().First(label => label.text == controller.State.selected.teaser);
            var findButton = document.rootVisualElement.Query<Button>().ToList().First(button => button.text == "Find in AR");
            Assert.That(findButton.worldBound.yMin, Is.GreaterThanOrEqualTo(longClue.worldBound.yMax),
                "A long clue must scroll above the action instead of painting through it.");
            controller.Navigate(AppPage.Stick);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Submit("Write note");
            yield return Capture("note-compact-largest-reduced-motion");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            controller.Camera.CameraPresentation = CameraPresentationState.Live;
            controller.Camera.IsTracking = true;
            controller.Notify();
            yield return Capture("camera-compact-largest-reduced-motion");
            var compactInventory = document.rootVisualElement.Q<Button>("STICK Inventory");
            Assert.That(compactInventory.worldBound.yMax, Is.LessThanOrEqualTo(document.rootVisualElement.worldBound.yMax));
            Assert.That(document.rootVisualElement.Q("STICK Adjustments"), Is.Null);
            Assert.That(document.rootVisualElement.Q("STICK camera dock").worldBound.yMin,
                Is.GreaterThanOrEqualTo(document.rootVisualElement.Q("STICK camera header").worldBound.yMax));
            Submit("STICK Inventory");
            yield return Capture("inventory-compact-largest-reduced-motion");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("STICK Write note");
            controller.State.error = "Turn on Precise Location for tagtag in Settings, then try again. Your draft is safe.";
            controller.State.locationSettingsRequired = true;
            controller.Notify();
            yield return Capture("publish-settings-compact-largest");
            var settingsSheet = document.rootVisualElement.Q<PaperSheet>();
            var compactSettings = settingsSheet.Q<Button>("Open location settings");
            settingsSheet.Scroll.ScrollTo(compactSettings);
            yield return Capture("publish-settings-compact-scrolled");
            Assert.That(compactSettings.worldBound.yMax, Is.LessThanOrEqualTo(document.rootVisualElement.worldBound.yMax));
            Assert.That(compactSettings.worldBound.yMin, Is.GreaterThanOrEqualTo(settingsSheet.worldBound.yMin));
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            controller.State.error = "";
            controller.State.locationSettingsRequired = false;
            controller.State.selectedPreset = null;
            controller.State.selected = Sticker(0);
            controller.Notify();
            yield return Capture("camera-discovery-compact-largest");
            foreach (var presentation in new[] { CameraPresentationState.Preparing,
                CameraPresentationState.PermissionDenied, CameraPresentationState.Interrupted,
                CameraPresentationState.Unavailable })
            {
                controller.Camera.CameraPresentation = presentation;
                controller.Notify();
                yield return Capture("camera-recovery-compact-" + presentation);
                Assert.That(controller.Camera.InteractionBlocked, Is.True);
                var recovery = document.rootVisualElement.Q<ScrollView>("Camera recovery scroll");
                Assert.That(recovery, Is.Not.Null, "Recovery must scroll between camera controls at large text sizes.");
                Assert.That(recovery.worldBound.yMin, Is.GreaterThanOrEqualTo(document.rootVisualElement.Q("STICK camera header").worldBound.yMax));
                Assert.That(recovery.worldBound.yMax, Is.LessThanOrEqualTo(document.rootVisualElement.Q("STICK camera dock").worldBound.yMin));
                var recoveryAction = recovery.Q<Button>();
                if (recoveryAction.resolvedStyle.display != DisplayStyle.None)
                {
                    recovery.ScrollTo(recoveryAction);
                    yield return null;
                    yield return null;
                    Assert.That(recoveryAction.worldBound.yMin, Is.GreaterThanOrEqualTo(recovery.worldBound.yMin));
                    Assert.That(recoveryAction.worldBound.yMax, Is.LessThanOrEqualTo(recovery.worldBound.yMax + 1f));
                    yield return Capture("camera-recovery-compact-" + presentation + "-action");
                }
            }
        }

        private void Submit(string title)
        {
            VisualElement scope = document.rootVisualElement.Q<PaperSheet>() ?? document.rootVisualElement;
            var buttons = scope.Query<Button>().ToList();
            var button = buttons.FirstOrDefault(b => b.name == title) ?? buttons.FirstOrDefault(b => b.text == title);
            Assert.That(button, Is.Not.Null, "Missing control: " + title);
            using (var submit = NavigationSubmitEvent.GetPooled())
            { button.Focus(); submit.target = button; button.SendEvent(submit); }
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Count, Is.GreaterThan(0));
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            var directory = Path.Combine(Application.temporaryCachePath, "tagtag-paper-review");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            Debug.Log("Paper UI capture: " + Path.Combine(directory, name + ".png"));
            UnityEngine.Object.Destroy(pixels);
        }

        [TearDown]
        public void Cleanup()
        {
            if (document != null) document.panelSettings.targetTexture = null;
            if (host != null) UnityEngine.Object.Destroy(host);
            if (target != null) UnityEngine.Object.Destroy(target);
            PlayerPrefs.SetFloat("tagtag.textScale", oldScale);
            PlayerPrefs.SetInt("tagtag.reducedMotion", oldMotion);
        }

        private static CollectedSticker Sticker(int i) => new CollectedSticker
        {
            id = "review-" + i, presetId = "taggi-" + (i % 4 + 1), authorId = "author", authorName = "Hana",
            place = "The quiet corner by the river", teaser = "Look beside the little red bridge.",
            note = string.Join("\n\n", Enumerable.Repeat("川のそばで、ひと休み。 Café by the river. Take a moment here. The river is quiet in the morning, and the bakery around the corner has a lovely window seat.", 8)),
            createdAt = 1780000000, collectedAt = 1780000000 + i, latitude = 35.68, longitude = 139.76
        };

        private sealed class ReviewCamera : IArExperience
        {
            public event Action Changed { add { } remove { } }
            public event Action<string> StickerTapped { add { } remove { } }
            public CameraPresentationState CameraPresentation { get; set; } = CameraPresentationState.Preparing;
            public bool IsTracking { get; set; }
            public bool CanPublish { get; set; }
            public bool CanCollect => false;
            public bool HasPlacementSurface { get; set; }
            public bool HasPlacementPreview { get; set; }
            public bool HasTrackedPlacement { get; set; }
            public bool PlacementBusy { get; set; }
            public float PlacementWidthMeters { get; private set; } = .2f;
            public float PlacementRotationDegrees { get; private set; }
            public bool InteractionBlocked { get; private set; }
            public Rect CameraRect { get; private set; }
            public int PlaceCalls { get; private set; }
            public string Status => "Move slowly to scan the surroundings.";
            public void Enter() { CameraPresentation = CameraPresentationState.Preparing; }
            public void Exit() { CameraPresentation = CameraPresentationState.Inactive; }
            public void SelectPreset(string presetId) { HasPlacementPreview = false; }
            public void CancelPlacement() { HasPlacementPreview = false; }
            public void SetCameraInteraction(Rect rect, bool blocked) { CameraRect = rect; InteractionBlocked = blocked; }
            public void Place(Vector2 point) { PlaceCalls++; HasPlacementPreview = true; }
            public void AdjustPlacement(float width, float rotation, Vector2? point = null)
            { PlacementWidthMeters = width; PlacementRotationDegrees = rotation; }
            public void Capture(Action<SpatialSnapshot> success, Action<string> failure) { failure("Review fixture"); }
            public void Recover(RecoveryData recovery) { }
        }

        private sealed class ReviewController : ITagtagController
        {
            public readonly ReviewCamera Camera = new ReviewCamera();
            public AppState State { get; } = new AppState { servicesConfigured = true,
                location = new LocationFix { latitude = 35.68, longitude = 139.76, accuracyMeters = 5 } };
            public IArExperience Ar => Camera;
            public IMapExperience Map => null;
            public event Action Changed;
            public void Notify() { Changed?.Invoke(); }
            public void Navigate(AppPage page) { State.page = page; if (page == AppPage.Stick) Camera.Enter(); Notify(); }
            public void SetAccountOpen(bool open) { State.accountOpen = open; Notify(); }
            public void SignIn(string provider) { State.user = new UserSession { uid = "review", displayName = "Aki" }; Notify(); }
            public void SignOut() { State.user = null; Notify(); }
            public void RefreshNearby() { }
            public void SelectSticker(string id) { State.selected = State.nearby.Find(s => s.id == id); Notify(); }
            public void StartDiscovery() { Navigate(AppPage.Stick); }
            public void SelectPreset(string id) { State.selectedPreset = id; Camera.SelectPreset(id); Notify(); }
            public void SetDraft(string place, string teaser, string note) { State.draftPlace = place; State.draftTeaser = teaser; State.draftNote = note; Notify(); }
            public int PublishCount { get; private set; }
            public void Publish() { PublishCount++; }
            public void CancelPlacement() { State.selectedPreset = ""; Notify(); }
            public void OpenCollected(string id) { State.detail = State.collection.Find(s => s.id == id); Notify(); }
            public void CloseDetail() { State.detail = null; Notify(); }
            public void Report(string id, string reason) { }
            public void Block(string authorId) { }
            public void Withdraw(string id) { }
            public void DeleteAccount() { }
        }
    }
}
