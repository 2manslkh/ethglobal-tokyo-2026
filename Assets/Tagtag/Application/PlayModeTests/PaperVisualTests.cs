using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            controller.State.user = new UserSession { uid = "review", displayName = "Aki" };
            for (int i = 0; i < 21; i++) controller.State.collection.Add(Sticker(i));
            controller.Notify();
            yield return Capture("home-populated");

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
            Assert.That(document.rootVisualElement.Q("Opaque camera cover").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Submit("Leave a sticker");
            yield return Capture("pose-picker");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            controller.SelectPreset("taggi-2");
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
            Assert.That(document.rootVisualElement.panel.focusController.focusedElement, Is.SameAs(noteFocus));
            Assert.That(fields[2].cursorIndex, Is.EqualTo(cursor));
            Assert.That(fields[2].selectIndex, Is.EqualTo(selection));
            Assert.That(document.rootVisualElement.Q<PaperSheet>().Scroll.scrollOffset, Is.EqualTo(noteScroll));
            controller.State.busy = false;
            controller.State.error = "";
            controller.Notify();
            yield return null;
            yield return null;
            Submit("Sign in to publish");
            yield return Capture("note-sign-in-return");
            Assert.That(document.rootVisualElement.Q<TextField>("Your note"), Is.Null,
                "Sign-in must leave the note sheet before returning to the draft.");
            controller.SignIn("apple");
            yield return Capture("note-signed-in-restored");
            Assert.That(document.rootVisualElement.Q<TextField>("Your note").value, Is.EqualTo(Sticker(0).note));
            controller.Camera.CanPublish = true;
            controller.Notify();
            yield return null;
            yield return null;
            Submit("Publish sticker");
            Submit("Publish sticker");
            Assert.That(controller.PublishCount, Is.EqualTo(1), "Repeated activation in one frame must publish once.");
            controller.State.selectedPreset = "";
            controller.State.draftPlace = controller.State.draftTeaser = controller.State.draftNote = "";
            controller.Notify();
            yield return new WaitForSecondsRealtime(.4f);
            controller.SelectPreset("taggi-3");
            yield return new WaitForSecondsRealtime(.4f);
            if (document.rootVisualElement.Q<PaperSheet>() == null) Submit("Write note");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.Q<TextField>("Your note").value, Is.Empty,
                "A successful publication must clear the next placement draft.");
            Submit("Close");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.panel.focusController.focusedElement,
                Is.SameAs(document.rootVisualElement.Q<Button>("Action Write note")), "Dismissal returns focus to the trigger.");

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
            Submit("Sign in");
            yield return new WaitForSecondsRealtime(.4f);
            controller.SignIn("apple");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(document.rootVisualElement.Q<PaperSheet>(), Is.Null,
                "An abandoned publish sign-in must not reopen the old sheet during a later sign-in.");
            controller.SetAccountOpen(false);

            controller.Navigate(AppPage.Explore);
            yield return Capture("explore-empty");
            controller.State.selected = Sticker(0);
            controller.State.nearby.Add(controller.State.selected);
            controller.Notify();
            yield return Capture("explore-teaser");
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
            Assert.That(teaserScroll.worldBound.yMax, Is.LessThanOrEqualTo(document.rootVisualElement.Q<Button>("Tab Explore").worldBound.yMin + 1f));
            var longClue = document.rootVisualElement.Query<Label>().ToList().First(label => label.text == controller.State.selected.teaser);
            var findButton = document.rootVisualElement.Query<Button>().ToList().First(button => button.text == "Find in AR");
            Assert.That(findButton.worldBound.yMin, Is.GreaterThanOrEqualTo(longClue.worldBound.yMax),
                "A long clue must scroll above the action instead of painting through it.");
            controller.Navigate(AppPage.Stick);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Submit("Write note");
            yield return Capture("note-compact-largest-reduced-motion");
        }

        private void Submit(string title)
        {
            var button = document.rootVisualElement.Query<Button>().ToList().FirstOrDefault(b => b.text == title);
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
            public bool IsTracking => false;
            public bool CanPublish { get; set; }
            public bool CanCollect => false;
            public string Status => "Move slowly to scan the surroundings.";
            public void Enter() { CameraPresentation = CameraPresentationState.Preparing; }
            public void Exit() { CameraPresentation = CameraPresentationState.Inactive; }
            public void SelectPreset(string presetId) { }
            public void CancelPlacement() { }
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
            public void SelectPreset(string id) { State.selectedPreset = id; Notify(); }
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
