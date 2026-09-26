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
        public IEnumerator OriginalSpotPhotoStaysVisibleWhileArRelocalizes()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            controller.State.selected = Sticker(0);
            controller.Navigate(AppPage.Stick);
            var photo = new Texture2D(16, 24);
            photo.SetPixels(Enumerable.Repeat(Color.magenta, 16 * 24).ToArray());
            photo.Apply();
            controller.State.discoveryLoading = true;
            controller.Camera.CameraPresentation = CameraPresentationState.Preparing;
            host = new GameObject("Original spot photo review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(390, 844, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            try
            {
                yield return Capture("reference-photo-loading");
                var preview = document.rootVisualElement.Q<Button>("Original spot preview");
                Assert.That(preview.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(preview.Query<Label>().ToList().Any(label => label.text == "Loading photo…"), Is.True);
                Assert.That(preview.enabledSelf, Is.False);
                controller.State.discoveryLoading = false;
                controller.State.error = "Turn on Precise Location for tagtag in Settings, then try again.";
                controller.State.locationSettingsRequired = true;
                controller.Notify();
                yield return Capture("reference-photo-not-loaded");
                Assert.That(preview.Query<Label>().ToList().Any(label => label.text == "Photo not loaded"), Is.True,
                    "A failed recovery must not claim the sticker has no saved photo.");
                Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label => label.text == controller.State.error), Is.True);
                Assert.That(document.rootVisualElement.Q<Button>("Open location settings").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(document.rootVisualElement.Q("Discovery status").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));
                controller.State.error = "";
                controller.State.locationSettingsRequired = false;
                controller.Camera.ReferencePhoto = photo;
                controller.Camera.PhotoState = ReferencePhotoState.Ready;
                controller.Camera.CameraPresentation = CameraPresentationState.Live;
                controller.Notify();
                yield return Capture("reference-photo-live");
                Assert.That(preview.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                var image = preview.Q<Image>();
                Assert.That(image.image, Is.SameAs(photo));
                Assert.That(image.worldBound.height, Is.GreaterThan(50f));
                Assert.That(image.worldBound.yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(image.worldBound.yMax, Is.LessThan(document.rootVisualElement.worldBound.yMax));
                foreach (var state in new[] { CameraPresentationState.Preparing, CameraPresentationState.Interrupted })
                {
                    controller.Camera.CameraPresentation = state;
                    controller.Notify();
                    yield return Capture("reference-photo-" + state);
                    Assert.That(preview.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                        "The saved photo must remain available while the camera restarts or relocalizes.");
                }
                Submit("Original spot preview");
                yield return Capture("reference-photo-enlarged");
                Assert.That(document.rootVisualElement.Q<Image>("Original spot full photo").image, Is.SameAs(photo));
            }
            finally { UnityEngine.Object.Destroy(photo); }
        }

        [UnityTest]
        public IEnumerator NftStatusUpdatesWithoutReplacingPrivateNotes()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            controller.State.user = new UserSession { uid = "nft-review", displayName = "Aki" };
            controller.State.nftEnabled = true;
            controller.State.walletStatus = "ready";
            controller.State.walletAddress = "0x1111111111111111111111111111111111111111";
            var sticker = Sticker(0);
            sticker.nft = new NftStatus { status = "pending", chainId = 11155111 };
            controller.State.collection.Add(sticker);
            host = new GameObject("NFT visual review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(390, 844, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color32(218, 225, 222, 255);
            yield return Capture("nft-home");
            var bookCell = document.rootVisualElement.Query<VisualElement>().ToList().First(element => element.userData as string == sticker.id);
            bookCell.Focus();
            using (var key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return }))
            { key.target = bookCell; bookCell.SendEvent(key); }
            yield return Capture("nft-pending");
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label => label.text == NftPresentation.Status(sticker.nft)), Is.True);
            sticker.nft.status = "confirmed";
            sticker.nft.transactionHash = "0x" + new string('a', 64);
            controller.Notify();
            yield return Capture("nft-confirmed");
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label => label.text == sticker.note), Is.True);
            Assert.That(document.rootVisualElement.Query<Button>().ToList().Any(button => button.text == "View NFT transaction"), Is.True);
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            controller.SetAccountOpen(true);
            yield return Capture("nft-wallet");
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label => label.text == controller.State.walletAddress), Is.True);
            Submit("Delete account and stickers");
            yield return Capture("nft-transfer-out");
            document.rootVisualElement.Q<TextField>("Delete confirmation").value = "DELETE";
            var delete = document.rootVisualElement.Query<Button>().ToList().First(button => button.text == "Permanently delete account");
            Assert.IsFalse(delete.enabledSelf, "Typing DELETE alone must not bypass NFT access-loss acknowledgement.");
            document.rootVisualElement.Q<TextField>("NFT recipient").value = "0x2222222222222222222222222222222222222222";
            Submit("Send this NFT");
            yield return Capture("nft-transfer-pending");
            Assert.AreEqual("pending", controller.State.nftTransfers[0].status);
            document.rootVisualElement.Q<Toggle>("Acknowledge NFT access loss").value = true;
            yield return null;
            Assert.IsTrue(delete.enabledSelf, "An informed fallback remains available even while a transfer is pending.");
            document.rootVisualElement.Q<ScrollView>("Account scroll").scrollOffset = new Vector2(0, 2000);
            yield return Capture("nft-deletion-fallback");
            var acknowledgementTrack = document.rootVisualElement.Q<Toggle>("Acknowledge NFT access loss").Q(className: "switch-track");
            Assert.GreaterOrEqual(acknowledgementTrack.resolvedStyle.width, 48f,
                "Long acknowledgement copy must not compress the switch or clip its thumb.");
            Assert.LessOrEqual(acknowledgementTrack.worldBound.xMax, document.rootVisualElement.worldBound.xMax,
                "The access-loss acknowledgement control must remain visible inside the screen.");
        }

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
            AssertCenteredStickControls();
            Assert.That(document.rootVisualElement.Q("STICK scan progress").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Submit("STICK Inventory");
            yield return Capture("camera-inventory");
            Assert.That(controller.Camera.InteractionBlocked, Is.True, "The inventory must block all camera input.");
            var inventoryGrid = document.rootVisualElement.Q<VisualElement>("Sticker inventory grid");
            Assert.That(inventoryGrid, Is.Not.Null);
            Assert.That(inventoryGrid.childCount, Is.EqualTo(5), "Four Taggi stickers and Add Sticker should form the initial inventory.");
            Assert.That(inventoryGrid.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row));
            Assert.That(inventoryGrid.childCount % 3, Is.EqualTo(2), "The final inventory row includes the Add Sticker tile.");
            Assert.That(inventoryGrid[0].worldBound.yMin, Is.EqualTo(inventoryGrid[1].worldBound.yMin).Within(1f));
            Assert.That(inventoryGrid[1].worldBound.yMin, Is.EqualTo(inventoryGrid[2].worldBound.yMin).Within(1f));
            Assert.That(inventoryGrid[1].worldBound.xMin, Is.GreaterThan(inventoryGrid[0].worldBound.xMin));
            Assert.That(inventoryGrid[2].worldBound.xMin, Is.GreaterThan(inventoryGrid[1].worldBound.xMin));
            Assert.That(inventoryGrid[3].worldBound.yMin, Is.GreaterThan(inventoryGrid[0].worldBound.yMin),
                "The fourth sticker wraps to the next row, confirming three columns.");
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label =>
                label.text.StartsWith("Taggi pose ") || label.text.StartsWith("After choosing")), Is.False,
                "Inventory artwork has no names or placement instructions.");
            Assert.That(inventoryGrid[4].name, Is.EqualTo("Add Sticker"), "Add Sticker must follow the last sticker.");
            foreach (var choice in document.rootVisualElement.Query<Button>().ToList().Where(button =>
                button.name != null && button.name.StartsWith("Inventory Taggi pose ")))
            {
                Assert.That(choice.Q<Image>(), Is.Not.Null, "Each inventory tile must show sticker artwork.");
                Assert.That(choice.Query<Label>().ToList().Any(label => label.text.StartsWith("Taggi pose ")), Is.False);
            }
            Submit("Add Sticker");
            yield return Capture("camera-create-sticker");
            foreach (string source in new[] { "Upload", "Photo", "Imagine" })
            {
                var tile = document.rootVisualElement.Q<Button>("Creator " + source);
                Assert.That(tile, Is.Not.Null);
                Assert.That(tile.Q<Image>()?.image, Is.Not.Null, source + " uses its dedicated illustration.");
                Assert.That(tile.Query<Label>().ToList().Any(label => label.text == source), Is.True);
            }
            Assert.That(document.rootVisualElement.Query<Label>().ToList().Any(label =>
                label.text != null && label.text.StartsWith("Choose a photo library image")), Is.False);
            Submit("Back to stickers");
            yield return Capture("camera-inventory-return");
            Submit("Inventory Taggi pose 2");
            yield return Capture("camera-finding-surface");
            Assert.That(controller.State.selectedPreset, Is.EqualTo("taggi-2"));
            AssertCenteredStickControls();
            Assert.That(document.rootVisualElement.Q<Label>("STICK scan label").text, Is.EqualTo("Find surface"));
            Assert.That(document.rootVisualElement.Q<PaperScanProgress>("STICK scan progress").Progress, Is.EqualTo(0f).Within(.001f));
            Assert.That(document.rootVisualElement.Q("STICK scan progress").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            var scanRecovery = document.rootVisualElement.Q<Label>("STICK scan recovery");
            Assert.That(scanRecovery.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            controller.Camera.IsTracking = false;
            controller.Notify();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(scanRecovery.text, Is.EqualTo("Tracking paused. Move slowly to resume."));
            Assert.That(scanRecovery.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            controller.Camera.IsTracking = true;
            controller.Notify();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(scanRecovery.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(controller.Camera.PlaceCalls, Is.Zero, "Selecting inventory art must not place it automatically.");
            var write = document.rootVisualElement.Q<Button>("STICK Write note");
            Assert.That(write == null || !write.enabledSelf, Is.True, "Place a preview before writing its note.");
            controller.Camera.HasPlacementSurface = true;
            controller.Camera.ScanState = PlacementScanState.SurfaceReady;
            controller.Notify();
            yield return Capture("camera-surface-ready");
            Assert.That(document.rootVisualElement.Q<Label>("STICK scan label").text, Is.EqualTo("Place sticker"));
            Assert.That(document.rootVisualElement.Q<PaperScanProgress>("STICK scan progress").Progress, Is.EqualTo(1f / 3f).Within(.001f));
            AssertCenteredStickControls();
            Submit("STICK Inventory");
            yield return new WaitForSecondsRealtime(.4f);
            yield return TapCameraSurface();
            Assert.That(controller.Camera.PlaceCalls, Is.Zero, "Touches behind the inventory must not place a sticker.");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            yield return TapCameraSurface();
            controller.Camera.ScanState = PlacementScanState.Placed;
            controller.Notify();
            yield return Capture("camera-adjusting-preview");
            Assert.That(document.rootVisualElement.Q<Label>("STICK scan label").text, Is.EqualTo("Scan surroundings"));
            Assert.That(document.rootVisualElement.Q<PaperScanProgress>("STICK scan progress").Progress, Is.EqualTo(2f / 3f).Within(.001f));
            AssertCenteredStickControls();
            Assert.That(controller.Camera.PlaceCalls, Is.EqualTo(1));
            Assert.That(controller.Camera.CanPublish, Is.False, "Fixture keeps mapping incomplete to test independent note access.");
            Assert.That(document.rootVisualElement.Q("STICK Adjustments"), Is.Null);
            var card = document.rootVisualElement.Q("STICK title sticker");
            Assert.That(card.Query<Label>().ToList().Select(label => label.text), Is.EqualTo(new[] { "Place Sticker" }));
            Assert.That(card.Query<Image>().ToList(), Is.Empty);
            Assert.That(document.rootVisualElement.Q("STICK guidance scroll"), Is.Null);
            Assert.That(document.rootVisualElement.Q("STICK Placement Guidance"), Is.Null);
            controller.Camera.ScanState = PlacementScanState.Ready;
            controller.Notify();
            yield return Capture("camera-scan-ready");
            Assert.That(document.rootVisualElement.Q<Label>("STICK scan label").text, Is.EqualTo("Scan ready"));
            Assert.That(document.rootVisualElement.Q<PaperScanProgress>("STICK scan progress").Progress, Is.EqualTo(1f).Within(.001f));
            AssertCenteredStickControls();
            Submit("STICK Write note");
            yield return Capture("camera-note-after-placement");
            Assert.That(document.rootVisualElement.Q<TextField>("Your note"), Is.Not.Null);
            Assert.That(controller.Camera.InteractionBlocked, Is.True);
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Cancel placement");
            yield return Capture("camera-placement-cancelled");
            AssertCenteredStickControls();
            Assert.That(document.rootVisualElement.Q("STICK scan progress").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Submit("STICK Close");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Explore), "Close returns to the camera entry destination.");
            Assert.That(document.rootVisualElement.Q<Button>("Tab Explore"), Is.Not.Null);
        }

        private void AssertCenteredStickControls()
        {
            var root = document.rootVisualElement;
            var button = root.Q<Button>("STICK Inventory");
            var dock = root.Q("STICK camera dock");
            Assert.That(button.worldBound.center.x, Is.EqualTo(root.Q("STICK Camera Surface").worldBound.center.x).Within(1f),
                "STICK stays centered as placement actions appear and disappear.");
            Assert.That(dock.resolvedStyle.backgroundColor.a, Is.Zero, "The camera dock has no rectangular backing.");
            Assert.That(dock.Children().OfType<PaperDottedOutline>(), Is.Empty);
            var ring = root.Q("STICK scan progress");
            Assert.That(ring.pickingMode, Is.EqualTo(PickingMode.Ignore));
            if (ring.resolvedStyle.display != DisplayStyle.None)
            {
                Assert.That(ring.worldBound.width, Is.EqualTo(ring.worldBound.height).Within(1f));
                Assert.That(ring.worldBound.center.x, Is.EqualTo(button.worldBound.center.x).Within(1f));
                Assert.That(ring.worldBound.center.y, Is.EqualTo(button.worldBound.center.y).Within(1f));
                Assert.That(ring.worldBound.width, Is.GreaterThan(button.worldBound.width));
                Assert.That(root.Q<Label>("STICK scan label").worldBound.yMax, Is.LessThanOrEqualTo(ring.worldBound.yMin));
            }
            foreach (var action in new[] { "STICK Write note", "STICK Retry AR search", "Cancel placement" })
            {
                var control = root.Q<Button>(action);
                if (control != null && control.resolvedStyle.display != DisplayStyle.None)
                    Assert.That(control.worldBound.yMax, Is.LessThanOrEqualTo(button.worldBound.yMin));
            }
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

            controller.SignIn("apple");
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
            Assert.That(document.rootVisualElement.Q("STICK guidance scroll"), Is.Null);
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
            Submit("Your Note");
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
            if (document.rootVisualElement.Q<PaperSheet>() == null) Submit("Your Note");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.Q<TextField>("Your note").value, Is.Empty,
                "A successful publication must clear the next placement draft.");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.panel.focusController.focusedElement,
                Is.SameAs(document.rootVisualElement.Q<Button>("STICK Write note")), "Dismissal returns focus to the trigger.");

            controller.SignOut();
            yield return Capture("sign-out-login");
            Assert.That(document.rootVisualElement.Q<Button>("Action Continue with Apple"), Is.Not.Null);
            Assert.That(document.rootVisualElement.Q<PaperSheet>(), Is.Null);
            controller.SignIn("apple");
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
            foreach (string title in new[] { "Report", "Block author" })
            {
                Button moderationButton = document.rootVisualElement.Query<Button>().ToList().First(button => button.text == title);
                Assert.That(moderationButton.enabledSelf, Is.False, title + " must remain disabled.");
            }
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
            Submit("Your Note");
            yield return Capture("note-compact-largest-reduced-motion");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            controller.Camera.CameraPresentation = CameraPresentationState.Live;
            controller.Camera.IsTracking = true;
            controller.Notify();
            yield return Capture("camera-compact-largest-reduced-motion");
            AssertCenteredStickControls();
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

        [UnityTest]
        public IEnumerator CompactLargeTextCreatorTilesKeepLabelsAndArtworkInsideTheirBounds()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1.4f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            controller.State.creationCapabilities = 15;
            host = new GameObject("Compact creator review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(320, 568, 24); target.Create();
            document.panelSettings.targetTexture = target;
            controller.OpenCreation();
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Add Sticker");
            yield return Capture("creator-compact-largest");
            var sheet = document.rootVisualElement.Q<PaperSheet>();
            Assert.That(sheet.worldBound.yMax, Is.LessThanOrEqualTo(document.rootVisualElement.worldBound.yMax + 1f));
            foreach (string source in new[] { "Upload", "Photo", "Imagine" })
            {
                var tile = sheet.Q<Button>("Creator " + source);
                var art = tile.Q<Image>();
                var label = tile.Q<Label>();
                Assert.That(art.image, Is.Not.Null);
                Assert.That(label.text, Is.EqualTo(source));
                Assert.That(label.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(19f));
                Assert.That(tile.worldBound.xMin, Is.GreaterThanOrEqualTo(sheet.Scroll.worldBound.xMin - 1f));
                Assert.That(tile.worldBound.xMax, Is.LessThanOrEqualTo(sheet.Scroll.worldBound.xMax + 1f));
                Assert.That(art.worldBound.xMin, Is.GreaterThanOrEqualTo(tile.worldBound.xMin - 1f));
                Assert.That(art.worldBound.xMax, Is.LessThanOrEqualTo(tile.worldBound.xMax + 1f));
                Assert.That(label.worldBound.xMin, Is.GreaterThanOrEqualTo(tile.worldBound.xMin - 1f));
                Assert.That(label.worldBound.xMax, Is.LessThanOrEqualTo(tile.worldBound.xMax + 1f));
                Assert.That(label.worldBound.yMax, Is.LessThanOrEqualTo(tile.worldBound.yMax + 1f));
            }
        }

        [UnityTest]
        public IEnumerator MyStickersShowsSourcesAndSavedArtwork()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            controller.State.user = new UserSession { uid = "creation-review", displayName = "Aki" };
            controller.State.creationCapabilities = 15;
            StickerArtwork.SetAccount("creation-review");
            var texture = new Texture2D(2, 3, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.yellow, Color.yellow, Color.cyan, Color.cyan });
            texture.Apply();
            StickerArtwork.Store("creation-review-image", texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            controller.State.designs.Add(new StickerDesign { id = "creation-review-image", ownerId = "creation-review",
                name = "An afternoon in Tokyo", kind = "polaroid", width = 2, height = 3 });
            host = new GameObject("Sticker creation review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(390, 844, 24); target.Create();
            document.panelSettings.targetTexture = target;
            controller.OpenCreation();
            yield return new WaitForSecondsRealtime(.4f);
            var inventorySheet = document.rootVisualElement.Q<PaperSheet>();
            var inventoryGrid = inventorySheet.Q<VisualElement>("Sticker inventory grid");
            Assert.That(inventoryGrid, Is.Not.Null);
            Assert.That(inventoryGrid.childCount, Is.EqualTo(6), "Saved designs, four originals, and Add Sticker appear in the inventory.");
            Assert.That(inventoryGrid[0].name, Is.EqualTo("Inventory Design creation-review-image"));
            Assert.That(inventoryGrid[5].name, Is.EqualTo("Add Sticker"));
            Assert.That(inventorySheet.Query<Label>().ToList().Any(label => label.text == "An afternoon in Tokyo"), Is.False,
                "Sticker names are hidden from the placement inventory.");
            Submit("Add Sticker");
            yield return new WaitForSecondsRealtime(.4f);
            var creationSheet = document.rootVisualElement.Q<PaperSheet>();
            Assert.That(creationSheet.Q<Label>(className: "sheet-title").worldBound.yMin, Is.GreaterThanOrEqualTo(0f),
                "root=" + document.rootVisualElement.worldBound + " sheet=" + creationSheet.worldBound +
                " styleHeight=" + creationSheet.style.height + " resolvedHeight=" + creationSheet.resolvedStyle.height +
                " scroll=" + creationSheet.Scroll.worldBound + " screen=" + Screen.width + "x" + Screen.height +
                " enum=" + typeof(TagtagAppView).GetField("sheet", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(host.GetComponent<TagtagAppView>()) +
                " same=" + ReferenceEquals(creationSheet, typeof(TagtagAppView).GetField("sheetView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(host.GetComponent<TagtagAppView>())));
            Assert.That(creationSheet.worldBound.yMax, Is.LessThanOrEqualTo(document.rootVisualElement.worldBound.yMax + 1f),
                "The complete sheet must stay within the viewport so every design is reachable by scrolling.");
            creationSheet.Scroll.scrollOffset = Vector2.zero;
            yield return Capture("my-stickers-sources");
            Assert.That(creationSheet.Q<Button>("Back to stickers"), Is.Not.Null);
            Assert.That(creationSheet.Q<Button>("Creator Photo"), Is.Not.Null);
            Assert.That(creationSheet.Q<Button>("Creator Imagine"), Is.Not.Null);
            Assert.That(creationSheet.Q<Button>("Creator My designs"), Is.Not.Null);
            Assert.That(creationSheet.Q<Label>("Creator design error"), Is.Not.Null);
            foreach (var source in new[] { ("Upload", "import"), ("Photo", "polaroid"), ("Imagine", "ai") })
            {
                Submit("Creator " + source.Item1);
                Assert.That(controller.CreatedSource, Is.EqualTo(source.Item2));
            }
            Assert.That(creationSheet.Query<Label>().ToList().Any(label => label.text == "An afternoon in Tokyo"), Is.False,
                "Saved-design management lives in Home.");
            controller.State.designError = "Saving failed. Try again."; controller.Notify();
            yield return Capture("my-stickers-save-error");
            Assert.That(creationSheet.Q<Label>("Creator design error").text, Is.EqualTo("Saving failed. Try again."));
            controller.State.hasPendingDesign = true; controller.Notify();
            creationSheet.Scroll.scrollOffset = Vector2.zero;
            yield return Capture("my-stickers-pending-save");
            Assert.That(document.rootVisualElement.Query<Button>().ToList().Any(button => button.text == "Retry saving sticker"), Is.True);
            controller.State.designError = ""; controller.Notify();
            controller.State.busy = true;
            controller.State.status = "Saving your sticker…";
            controller.Notify();
            yield return new WaitForSecondsRealtime(.4f);
            controller.State.busy = false;
            controller.State.hasPendingDesign = false;
            controller.State.status = "Saved to My Stickers. Choose it whenever you're ready to place.";
            controller.Notify();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(creationSheet.Q<Label>("Creator save notice").text, Is.EqualTo("Saved to My designs."));
            controller.State.status = "Nearby stickers updated";
            controller.Notify();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(creationSheet.Q<Label>("Creator save notice").text, Is.Empty,
                "Unrelated status must not appear in the creator notice.");
            Submit("Creator My designs");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
            Assert.That(controller.State.creationOpen, Is.False);
            Assert.That(document.rootVisualElement.Q<PaperSheet>(), Is.Null);
            Assert.That(document.rootVisualElement.Q<Button>("Home My designs"), Is.Not.Null);
            controller.OpenCreation();
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Add Sticker");
            yield return new WaitForSecondsRealtime(.4f);
            Submit("Back to stickers");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.Q<VisualElement>("Sticker inventory grid"), Is.Not.Null,
                "Back returns to the inventory without closing the creation session.");
            StickerArtwork.Forget("creation-review-image");
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

        [UnityTest]
        public IEnumerator CameraCloseRemainsAvailableWhileDiscoveryLoads()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            controller.State.user = new UserSession { uid = "review" };
            controller.Navigate(AppPage.Explore);
            controller.State.selected = Sticker(0);
            host = new GameObject("Discovery close review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            yield return null; yield return null;
            controller.StartDiscovery();
            controller.State.busy = true;
            controller.Notify();
            yield return null; yield return null;
            var close = document.rootVisualElement.Q<Button>("STICK Close");
            Assert.That(close.enabledInHierarchy, Is.False, "Other foreground actions keep their existing protection.");
            controller.State.discoveryLoading = true;
            controller.Notify();
            yield return null; yield return null;
            Assert.That(close.enabledInHierarchy, Is.True, "X must be available immediately during discovery loading.");
            Submit("STICK Close");
            yield return null; yield return null;
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Explore));
        }

        [UnityTest]
        public IEnumerator LoginSupportsRetryCompactTextAndReducedMotion()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1.4f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            controller.SignOut();
            host = new GameObject("Login review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(320, 568, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            yield return Capture("login-compact-large-text");
            var root = document.rootVisualElement;
            var apple = root.Q<Button>("Action Continue with Apple");
            var google = root.Q<Button>("Action Continue with Google");
            Assert.That(apple, Is.Not.Null);
            Assert.That(google, Is.Not.Null);
            Assert.That(root.Q<Button>("Action Continue exploring"), Is.Null);
            Assert.That(root.Q<Button>("Action Back"), Is.Null);
            Assert.That(root.Q<Button>("Tab Home"), Is.Null);
            Assert.That(root.Q(className: "paper-notice"), Is.Null);
            Assert.That(root.Query<ScrollView>().ToList(), Is.Empty, "Sign-in must fit without scrolling.");
            Assert.That(root.Q("Login header").Query<Label>().ToList().Any(label => label.text == "Tagtag"), Is.True);
            Assert.That(root.Q<Label>("Login tagline").text, Is.EqualTo("Find your places,\nCollect your moments"));
            Assert.That(root.Q("Login header").resolvedStyle.backgroundColor.a, Is.Zero);
            Assert.That(root.Q("Login actions").resolvedStyle.backgroundColor.a, Is.Zero);
            Assert.That(root.Q(className: "login-fade"), Is.Null);
            foreach (var button in root.Query<Button>().ToList())
            {
                Assert.That(button.worldBound.yMin, Is.GreaterThanOrEqualTo(root.worldBound.yMin));
                Assert.That(button.worldBound.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax));
            }
            foreach (string title in new[] { "Privacy Policy", "Terms & Conditions" })
            {
                var link = root.Q<Button>("Login " + title);
                Assert.That(link, Is.Not.Null);
                Assert.That(link.worldBound.yMin, Is.GreaterThanOrEqualTo(google.worldBound.yMax));
                Submit("Login " + title);
                yield return null; yield return null;
                var legalSheet = root.Q<PaperSheet>();
                Assert.That(legalSheet, Is.Not.Null, "Legal information must open before sign-in.");
                Assert.That(legalSheet.Q<Label>(className: "sheet-title").text, Is.EqualTo(title));
                Assert.That(legalSheet.Scroll.Query<Label>().ToList().Count, Is.GreaterThan(5));
                yield return Capture(title == "Privacy Policy" ? "login-privacy" : "login-terms");
                Submit("Close");
                yield return null; yield return null;
                Assert.That(root.Q<PaperSheet>(), Is.Null);
                Assert.That(root.Query<ScrollView>().ToList(), Is.Empty);
            }
            Assert.That(host.GetComponent<UnityEngine.Video.VideoPlayer>(), Is.Null, "Reduced motion must not decode video.");
            Assert.That(apple.worldBound.width, Is.EqualTo(google.worldBound.width).Within(1f));
            Assert.That(apple.resolvedStyle.height, Is.GreaterThanOrEqualTo(52));
            Assert.That(google.worldBound.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax));
            controller.State.busy = true;
            controller.State.status = "Opening Apple…";
            controller.Notify();
            yield return null; yield return null;
            Assert.That(apple.enabledInHierarchy, Is.False);
            Assert.That(root.Q<Label>("Login status").resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "Opening the sign-in provider must not add small progress text.");
            controller.State.busy = false;
            controller.State.error = "Sign-in cancelled. Try again.";
            controller.Notify();
            yield return Capture("login-retry");
            Assert.That(apple.enabledInHierarchy, Is.True);
            Assert.That(root.Q<Label>("Login status").text, Does.Contain("cancelled"));
            controller.State.error = "";
            controller.State.servicesConfigured = false;
            controller.Notify();
            yield return null; yield return null;
            Assert.That(google.enabledInHierarchy, Is.False);
            Assert.That(root.Q<Label>("Login status").text, Does.Contain("configured"));
            controller.State.servicesConfigured = true;
            controller.SignIn("apple");
            yield return null; yield return null;
            Assert.That(root.Q("Login video background"), Is.Null);
            Assert.That(root.Q<Button>("Tab Home"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LoginVideoLoopsPausesFallsBackAndReleasesResources()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 0);
            controller = new ReviewController();
            controller.SignOut();
            host = new GameObject("Login video review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(390, 844, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            yield return null; yield return null;
            var player = host.GetComponent<UnityEngine.Video.VideoPlayer>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.audioOutputMode, Is.EqualTo(UnityEngine.Video.VideoAudioOutputMode.None));
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((!player.isPlaying || player.frame < 1) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.isPlaying, Is.True);
            bool looped = false;
            player.loopPointReached += _ => looped = true;
            player.time = player.length - .25;
            deadline = Time.realtimeSinceStartup + 5f;
            while (!looped && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(looped, Is.True);
            var playback = host.GetComponent<LoginBackdrop>();
            var repaintField = typeof(LoginBackdrop).GetField("repaint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var schedule = repaintField.GetValue(playback);
            playback.SendMessage("OnApplicationPause", true);
            Assert.That(player.isPlaying, Is.False);
            playback.SendMessage("OnApplicationPause", false);
            yield return null;
            Assert.That(player.isPlaying, Is.True);
            Assert.That(repaintField.GetValue(playback), Is.SameAs(schedule), "Resuming must reuse the repaint schedule.");
            yield return Capture("login-video");
            LogAssert.Expect(LogType.Warning, "Login background unavailable; using poster. Simulated decoder failure");
            typeof(LoginBackdrop).GetMethod("PlaybackError", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(playback, new object[] { player, "Simulated decoder failure" });
            Assert.That(player.isPlaying, Is.False);
            Assert.That(document.rootVisualElement.Q("Login video background").style.backgroundImage.value.texture, Is.Not.Null);
            var frame = player.targetTexture;
            controller.SignIn("google");
            yield return null; yield return null; yield return null;
            Assert.That(host.GetComponent<LoginBackdrop>(), Is.Null);
            Assert.That(host.GetComponent<UnityEngine.Video.VideoPlayer>(), Is.Null);
            Assert.That(frame == null, Is.True, "Leaving login must release its render texture.");
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

        private sealed class ReviewCamera : IArExperience, IReferencePhotoAr
        {
            public Texture2D ReferencePhoto { get; set; }
            public ReferencePhotoState PhotoState { get; set; }
            public event Action Changed { add { } remove { } }
            public event Action<string> StickerTapped { add { } remove { } }
            public CameraPresentationState CameraPresentation { get; set; } = CameraPresentationState.Preparing;
            public bool IsTracking { get; set; }
            public bool CanPublish { get; set; }
            public bool CanCollect => false;
            public bool HasPlacementSurface { get; set; }
            public PlacementScanState ScanState { get; set; } = PlacementScanState.FindingSurface;
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

        private sealed class ReviewController : ITagtagController, INftTransferController
        {
            public readonly ReviewCamera Camera = new ReviewCamera();
            public AppState State { get; } = new AppState { servicesConfigured = true, user = new UserSession { uid = "review", displayName = "Aki" },
                location = new LocationFix { latitude = 35.68, longitude = 139.76, accuracyMeters = 5 } };
            public IArExperience Ar => Camera;
            public IMapExperience Map => null;
            public event Action Changed;
            public void Notify() { Changed?.Invoke(); }
            public void Navigate(AppPage page) { State.page = page; if (page == AppPage.Stick) Camera.Enter(); Notify(); }
            public void SetAccountOpen(bool open) { State.accountOpen = open; Notify(); }
            public void SignIn(string provider) { State.user = new UserSession { uid = "review", displayName = "Aki" }; State.accountOpen = false; State.page = AppPage.Home; Notify(); }
            public void SignOut() { State.user = null; State.accountOpen = true; State.page = AppPage.Home; Notify(); }
            public void RefreshNearby() { }
            public void SelectSticker(string id) { State.selected = State.nearby.Find(s => s.id == id); Notify(); }
            public void StartDiscovery() { Navigate(AppPage.Stick); }
            public void OpenCreation() { State.creationOpen = true; Notify(); }
            public void CloseCreation() { State.creationOpen = false; Notify(); }
            public string CreatedSource { get; private set; }
            public void CreateSticker(string source) { CreatedSource = source; }
            public void RefreshDesigns() { }
            public void SelectDesign(string id) { State.selectedDesign = id; State.selectedPreset = ""; State.creationOpen = false; State.page = AppPage.Stick; Notify(); }
            public void DeleteDesign(string id) { }
            public void RetryDesignSave() { }
            public void RefreshArtwork() { StickerArtwork.Retry(); }
            public void SelectPreset(string id) { State.selectedPreset = id; State.selectedDesign = ""; State.creationOpen = false; State.page = AppPage.Stick; Camera.SelectPreset(id); Notify(); }
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
            public void AcknowledgeNftLoss(bool acknowledged) { State.nftDeletionAcknowledged = acknowledged; Notify(); }
            public void RefreshNftTransfers() { }
            public void TransferNft(string id, string recipient)
            {
                State.nftTransfers.Add(new NftTransfer { stickerId = id, recipient = recipient, status = "pending",
                    transactionHash = "0x" + new string('b', 64), message = "Transfer submitted. Waiting for Sepolia confirmation." });
                Notify();
            }
        }
    }
}
