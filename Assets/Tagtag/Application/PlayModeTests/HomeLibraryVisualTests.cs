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
    public sealed class HomeLibraryVisualTests
    {
        private GameObject host;
        private ReviewController controller;
        private UIDocument document;
        private RenderTexture target;
        private float oldScale;
        private int oldMotion;
        private VisualElement Root => document.rootVisualElement;

        [SetUp]
        public void SetUp()
        {
            oldScale = PlayerPrefs.GetFloat("tagtag.textScale", 1f);
            oldMotion = PlayerPrefs.GetInt("tagtag.reducedMotion", 0);
            PlayerPrefs.SetFloat("tagtag.textScale", 1f);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 1);
            controller = new ReviewController();
            controller.State.user = new UserSession { uid = "home-owner", displayName = "Aki" };
        }

        private IEnumerator Mount(float scale = 1f, int width = 390, int height = 844)
        {
            PlayerPrefs.SetFloat("tagtag.textScale", scale);
            StickerArtwork.SetAccount(controller.State.user?.uid);
            foreach (var design in controller.State.designs)
                if (design.ownerId == controller.State.user?.uid)
                {
                    StickerArtwork.Authorize(design.id);
                    StickerArtwork.Store(design.id, File.ReadAllBytes(Path.Combine(Application.dataPath, "Resources/Tagtag/Presets/taggi-1.png")));
                }
            host = new GameObject("Home library review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(width, height, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = Color.white;
            yield return Settle();
        }

        [UnityTest]
        public IEnumerator EmptyBookHasIllustratedActionBesideCountAndChangesOnCollection()
        {
            yield return Mount();
            var make = Root.Q<Button>("Home Make sticker");
            var count = Root.Q<Label>("home-collected-number");
            Assert.That(make.worldBound.xMin, Is.GreaterThan(count.worldBound.xMax));
            Assert.That(make.worldBound.center.y, Is.EqualTo(count.worldBound.center.y).Within(40f));
            Assert.That(make.layout.width, Is.GreaterThanOrEqualTo(44));
            Assert.That(make.layout.height, Is.GreaterThanOrEqualTo(44));
            Assert.That(Root.Q<Image>("Home Make sticker art")?.image, Is.Not.Null);
            Assert.That(Root.Q<Image>("Home Empty Taggi")?.image, Is.Not.Null);
            Assert.That(Root.Q<Button>("Home Collected"), Is.Not.Null);
            Assert.That(Root.Q<Button>("Home My designs"), Is.Not.Null);
            yield return Capture("home-collected-empty");
            controller.State.collection.Add(new CollectedSticker { id = "collected-one", presetId = "taggi-1" });
            controller.Notify();
            yield return Settle();
            Assert.That(Root.Q<Image>("Home Empty Taggi"), Is.Null);
            Assert.That(Root.Q<Label>("home-collected-number").text, Is.EqualTo("1"));
            Submit("Home Make sticker");
            yield return Settle();
            Assert.That(controller.State.creationOpen, Is.True);
            Assert.That(Root.Q<PaperSheet>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator OwnSavedDesignsAreSortedAndRemainVisibleDuringRefresh()
        {
            controller.State.designs.Add(Design("old", 1));
            controller.State.designs.Add(Design("foreign", 9, "other-owner"));
            controller.State.designs.Add(Design("new", 2));
            yield return Mount();
            Submit("Home My designs");
            yield return Settle();
            Assert.That(controller.RefreshCalls, Is.EqualTo(1));
            Assert.That(Root.Q<Button>("Home Design foreign"), Is.Null);
            var cards = Root.Query<Button>().ToList().Where(b => b.name != null && b.name.StartsWith("Home Design ")).ToList();
            Assert.That(cards.Select(b => b.name), Is.EqualTo(new[] { "Home Design new", "Home Design old" }));
            Assert.That(cards[1].worldBound.xMin, Is.GreaterThan(cards[0].worldBound.xMin));
            Assert.That(cards[1].worldBound.yMin, Is.EqualTo(cards[0].worldBound.yMin).Within(1f));
            Assert.That(IsVisible(Root.Q<Image>("Home Empty Taggi")), Is.False);
            controller.State.designsLoading = true; controller.Notify();
            yield return Settle();
            Assert.That(Root.Q<Button>("Home Design new"), Is.Not.Null, "Cached cards must remain during refresh.");
            controller.State.designsLoading = false; controller.State.error = "Could not refresh designs."; controller.Notify();
            yield return Settle();
            Submit("Home Refresh designs");
            Assert.That(controller.RefreshCalls, Is.EqualTo(2));
            yield return Capture("home-my-designs-refresh-error");
            controller.State.error = ""; controller.Notify(); yield return Settle();
            yield return Capture("home-my-designs");
        }

        [UnityTest]
        public IEnumerator PreviewPrecedesPlacementAndRemovalReturnsToGallery()
        {
            controller.State.designs.Add(Design("saved", 1));
            yield return Mount();
            Submit("Home My designs"); yield return Settle();
            Submit("Home Design saved"); yield return Settle();
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Home));
            Assert.That(controller.State.selectedDesign, Is.Empty);
            Assert.That(Root.Q<Image>("Home Design preview art")?.image, Is.Not.Null);
            yield return Capture("home-design-preview");
            Submit("Home Remove design"); yield return Settle();
            Submit("Cancel"); yield return Settle();
            Assert.That(Root.Q<Button>("Home Place design"), Is.Not.Null);
            Submit("Home Place design"); yield return Settle();
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Stick));
            Assert.That(controller.State.selectedDesign, Is.EqualTo("saved"));
            controller.Navigate(AppPage.Home); yield return Settle();
            Assert.That(Root.Q<Button>("Home Design saved"), Is.Not.Null, "Returning Home should preserve My designs.");
            Submit("Home Design saved"); yield return Settle();
            Submit("Home Remove design"); yield return Settle();
            Submit("Remove design"); yield return Settle();
            Assert.That(controller.DeletedId, Is.EqualTo("saved"));
            Assert.That(Root.Q<Button>("Home Design saved"), Is.Null);
            Assert.That(Root.Q<PaperSheet>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator AccountChangeClearsPrivatePreviewAndSignedOutDesigns()
        {
            controller.State.designs.Add(Design("private", 1));
            yield return Mount();
            Submit("Home My designs"); yield return Settle();
            Submit("Home Design private"); yield return Settle();
            controller.State.user = new UserSession { uid = "another-owner" }; controller.Notify();
            yield return Settle();
            Assert.That(Root.Q<PaperSheet>(), Is.Null);
            Assert.That(Root.Q<Button>("Home Design private"), Is.Null);
            controller.State.user = null; controller.Notify(); yield return Settle();
            Submit("Home My designs"); yield return Settle();
            Assert.That(Root.Q<Button>("Home Design private"), Is.Null);
            Assert.That(Root.Query<Label>().ToList().Any(l => l.text != null && l.text.Contains("Sign in")), Is.True);
            yield return Capture("home-designs-signed-out");
        }

        [UnityTest]
        public IEnumerator CompactLargeTextKeepsHeaderAndGalleryWithinScreen()
        {
            controller.State.designs.Add(Design("long", 1));
            controller.State.designs[0].name = "A very long sticker name for my favorite quiet corner";
            yield return Mount(1.4f, 320, 568);
            var make = Root.Q<Button>("Home Make sticker");
            var count = Root.Q<Label>("home-collected-number");
            Assert.That(make.worldBound.xMin, Is.GreaterThan(count.worldBound.xMax));
            Assert.That(make.worldBound.xMax, Is.LessThanOrEqualTo(Root.worldBound.xMax));
            yield return Capture("home-compact-empty");
            Submit("Home My designs"); yield return Settle();
            var card = Root.Q<Button>("Home Design long");
            Assert.That(card, Is.Not.Null);
            Assert.That(card.worldBound.xMax, Is.LessThanOrEqualTo(Root.worldBound.xMax));
            yield return Capture("home-compact-designs");
        }

        [UnityTest]
        public IEnumerator BookPageAndDesignScrollSurviveSwitchingAndNavigation()
        {
            for (int i = 0; i < 21; i++) controller.State.collection.Add(new CollectedSticker
                { id = "collected-" + i, presetId = "taggi-1", collectedAt = i });
            for (int i = 0; i < 12; i++) controller.State.designs.Add(Design("scroll-" + i, i));
            yield return Mount();
            Submit("Next"); yield return Settle();
            Assert.That(Root.Query<Label>().ToList().Any(l => l.text == "Page 2 of 2"), Is.True);
            Submit("Home My designs"); yield return Settle();
            var scroll = Root.Q<ScrollView>("Home scroll");
            scroll.scrollOffset = new Vector2(0, 180);
            yield return Settle();
            float saved = scroll.scrollOffset.y;
            Assert.That(saved, Is.GreaterThan(0));
            Submit("Home Collected"); yield return Settle();
            Assert.That(Root.Query<Label>().ToList().Any(l => l.text == "Page 2 of 2"), Is.True);
            Submit("Home My designs"); yield return Settle();
            Assert.That(Root.Q<ScrollView>("Home scroll").scrollOffset.y, Is.EqualTo(saved).Within(1));
            controller.Navigate(AppPage.Explore); yield return Settle();
            controller.Navigate(AppPage.Home); yield return Settle();
            Assert.That(Root.Q<Button>("Home Design scroll-0"), Is.Not.Null);
            Assert.That(Root.Q<ScrollView>("Home scroll").scrollOffset.y, Is.EqualTo(saved).Within(1));
        }

        [UnityTest]
        public IEnumerator FailedRemovalKeepsDesignAndAllowsRetry()
        {
            controller.State.designs.Add(Design("saved", 1));
            controller.DeferDeletion = true;
            yield return Mount();
            Submit("Home My designs"); yield return Settle();
            Submit("Home Design saved"); yield return Settle();
            Submit("Home Remove design"); yield return Settle();
            Submit("Remove design"); yield return Settle();
            Assert.That(controller.State.designs.Count, Is.EqualTo(1));
            Assert.That(Root.Q<PaperSheet>(), Is.Not.Null, "Removal should remain pending until the controller confirms success.");
            Submit("Close"); yield return Settle();
            Assert.That(Root.Q<PaperSheet>(), Is.Not.Null, "Close must not bypass pending deletion.");
            controller.State.busy = false;
            controller.State.error = "Could not remove your design. Please retry.";
            controller.Notify(); yield return Settle();
            Assert.That(Root.Q<Button>("Home Design saved"), Is.Not.Null);
            var retry = Root.Q<PaperSheet>().Query<Button>().ToList().FirstOrDefault(b => b.text == "Remove design");
            Assert.That(retry, Is.Not.Null);
            Assert.That(retry.enabledSelf, Is.True);
            controller.DeferDeletion = false;
            Submit("Remove design"); yield return Settle();
            Assert.That(Root.Q<PaperSheet>(), Is.Null);
            Assert.That(Root.Q<Button>("Home Design saved"), Is.Null);
        }

        [UnityTest]
        public IEnumerator GalleryArtworkNoticesStillUpdateAfterPreviewCloses()
        {
            controller.State.designs.Add(Design("saved", 1));
            yield return Mount();
            Submit("Home My designs"); yield return Settle();
            Submit("Home Design saved"); yield return Settle();
            Submit("Close"); yield return Settle();
            Assert.That(Root.Q<PaperSheet>(), Is.Null);
            StickerArtwork.Forget("saved"); StickerArtwork.Retry(); yield return Settle();
            var notice = Root.Q<VisualElement>("Home Designs").Query<Label>().ToList()
                .FirstOrDefault(l => l.text == "Artwork is unavailable.");
            Assert.That(notice, Is.Not.Null, "Gallery artwork notices must survive sheet rebuilds.");
            Assert.That(IsVisible(notice), Is.True);
            StickerArtwork.Authorize("saved");
            StickerArtwork.Store("saved", File.ReadAllBytes(Path.Combine(Application.dataPath, "Resources/Tagtag/Presets/taggi-1.png")));
            yield return Settle();
            Assert.That(IsVisible(notice), Is.False, "Restored artwork must clear its stale unavailable notice.");
        }

        [UnityTest]
        public IEnumerator PendingPlacementKeepsPreviewUntilSuccessOrRetry()
        {
            controller.State.designs.Add(Design("saved", 1));
            controller.DeferPlacement = true;
            yield return Mount();
            Submit("Home My designs"); yield return Settle();
            Submit("Home Design saved"); yield return Settle();
            Submit("Home Place design"); yield return Settle();
            Submit("Close"); yield return Settle();
            Assert.That(Root.Q<PaperSheet>(), Is.Not.Null, "A pending handoff should keep its preview visible.");
            controller.State.busy = false; controller.State.error = "Artwork could not load. Try again.";
            controller.Notify(); yield return Settle();
            Assert.That(Root.Q<Button>("Home Place design").enabledSelf, Is.True);
            controller.DeferPlacement = false;
            Submit("Home Place design"); yield return Settle();
            Assert.That(controller.State.page, Is.EqualTo(AppPage.Stick));
            Assert.That(Root.Q<PaperSheet>(), Is.Null);
        }

        private static StickerDesign Design(string id, long createdAt, string owner = "home-owner") => new StickerDesign
        { id = id, name = "Taggi " + id, ownerId = owner, createdAt = createdAt, kind = "import", width = 1024, height = 1024 };

        private static bool IsVisible(VisualElement element)
        {
            if (element == null) return false;
            for (var current = element; current != null; current = current.parent)
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        private void Submit(string name)
        {
            VisualElement scope = Root.Q<PaperSheet>() ?? Root;
            var button = scope.Query<Button>().ToList().FirstOrDefault(b => b.name == name || b.text == name);
            Assert.That(button, Is.Not.Null, "Missing control: " + name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { button.Focus(); evt.target = button; button.SendEvent(evt); }
        }

        private static IEnumerator Settle() { yield return new WaitForSecondsRealtime(.35f); }
        private IEnumerator Capture(string name)
        {
            yield return Settle();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            RenderTexture.active = previous;
            string directory = Path.Combine(Application.temporaryCachePath, "tagtag-home-review");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            UnityEngine.Object.Destroy(pixels);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var id in new[] { "old", "new", "saved", "private", "long" }) StickerArtwork.Forget(id);
            for (int i = 0; i < 12; i++) StickerArtwork.Forget("scroll-" + i);
            if (document != null) document.panelSettings.targetTexture = null;
            if (host != null) UnityEngine.Object.Destroy(host);
            if (target != null) UnityEngine.Object.Destroy(target);
            PlayerPrefs.SetFloat("tagtag.textScale", oldScale); PlayerPrefs.SetInt("tagtag.reducedMotion", oldMotion);
        }
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
            public void OpenCreation() { State.creationOpen = true; Notify(); }
            public void CloseCreation() { State.creationOpen = false; Notify(); }
            public void CreateSticker(string source) { }
            public int RefreshCalls;
            public void RefreshDesigns() { RefreshCalls++; }
            public bool DeferPlacement;
            public void SelectDesign(string id)
            {
                if (DeferPlacement) { State.busy = true; State.error = ""; Notify(); return; }
                State.selectedDesign = id; State.selectedPreset = "";
                State.creationOpen = false; State.page = AppPage.Stick; Notify();
            }
            public string DeletedId;
            public bool DeferDeletion;
            public void DeleteDesign(string id)
            {
                DeletedId = id;
                if (DeferDeletion) { State.busy = true; State.error = ""; }
                else State.designs.RemoveAll(d => d.id == id);
                Notify();
            }
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

        }
    }
}
