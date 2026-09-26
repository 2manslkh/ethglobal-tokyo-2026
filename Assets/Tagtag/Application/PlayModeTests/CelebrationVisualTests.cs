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
    public sealed class CelebrationVisualTests
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
            host = new GameObject("Sticker celebration review");
            host.AddComponent<TagtagAppView>().Initialize(controller);
            document = host.GetComponent<UIDocument>();
            target = new RenderTexture(width, height, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = Color.white;
            yield return Settle();
        }

        private void Reward(bool found)
        {
            var sticker = new CollectedSticker { id = "reward", presetId = "taggi-2", place = "A quiet corner of Tokyo", note = "The little garden behind the station is worth a visit." };
            controller.State.page = found ? AppPage.Home : AppPage.Stick;
            controller.Camera.CameraPresentation = CameraPresentationState.Live;
            controller.Camera.IsTracking = true;
            if (found)
            {
                controller.State.collection.Add(sticker);
                controller.State.detail = sticker;
                controller.State.celebrations.Collected("home-owner", "discovery", sticker, false);
            }
            else controller.State.celebrations.Published("home-owner", "publication", sticker);
        }

        [UnityTest]
        public IEnumerator FoundRewardWaitsForUserThenOpensNoteWithoutReplay()
        {
            Reward(true);
            yield return Mount();
            var reward = Root.Q("Sticker celebration");
            Assert.IsNotNull(reward);
            Assert.IsNull(Root.Q<PaperSheet>());
            Assert.IsFalse(IsVisible(Root.Q<Button>("Tab Home")));
            Assert.IsNotNull(Root.Q<Image>("Celebration artwork").image);
            yield return Capture("found");
            controller.State.busy = true;
            controller.Notify();
            yield return Settle();
            Assert.AreSame(reward, Root.Q("Sticker celebration"));
            controller.State.busy = false;
            Submit("Read the note");
            yield return Settle();
            Assert.IsNull(controller.State.celebrations.Pending);
            Assert.IsNull(Root.Q("Sticker celebration"));
            Assert.IsTrue(Root.Query<Label>().ToList().Any(l => l.text == controller.State.detail.note));
            controller.Notify();
            yield return Settle();
            Assert.IsNull(Root.Q("Sticker celebration"));
        }

        [UnityTest]
        public IEnumerator PlacedRewardBlocksCameraAndReturnsToItOnContinue()
        {
            Reward(false);
            yield return Mount();
            Assert.IsTrue(controller.Camera.InteractionBlocked);
            yield return Capture("placed");
            Submit("Keep exploring");
            yield return Settle();
            Assert.AreEqual(AppPage.Stick, controller.State.page);
            Assert.IsFalse(controller.Camera.InteractionBlocked);
            Assert.IsNull(Root.Q("Sticker celebration"));
        }

        [UnityTest]
        public IEnumerator CompactEnlargedRewardsKeepActionVisibleAndReducedArtworkSettled()
        {
            foreach (bool found in new[] { false, true })
            {
                Reward(found);
                if (host == null) yield return Mount(1.4f, 320, 568);
                else { controller.Notify(); yield return Settle(); }
                var art = Root.Q<Image>("Celebration artwork");
                Assert.AreEqual(Vector3.one, art.resolvedStyle.scale.value);
                var action = Root.Q<Button>("Celebration continue");
                Assert.GreaterOrEqual(action.worldBound.height, 44f);
                Assert.LessOrEqual(action.worldBound.yMax, Root.worldBound.yMax);
                Assert.GreaterOrEqual(action.worldBound.xMin, Root.worldBound.xMin);
                Assert.LessOrEqual(action.worldBound.xMax, Root.worldBound.xMax);
                yield return Capture(found ? "found-compact-large-text" : "placed-compact-large-text");
                Submit(found ? "Read the note" : "Keep exploring");
                yield return Settle();
            }
        }

        [UnityTest]
        public IEnumerator AccountChangeRemovesRewardAndFailureDoesNotCreateOne()
        {
            Reward(true);
            yield return Mount();
            controller.State.user = new UserSession { uid = "another-owner" };
            controller.Notify();
            yield return Settle();
            Assert.IsNull(Root.Q("Sticker celebration"));
            Assert.IsNull(controller.State.celebrations.Pending);
            controller.State.error = "Publishing failed. Try again.";
            controller.Notify();
            yield return Settle();
            Assert.IsNull(Root.Q("Sticker celebration"));
        }

        [UnityTest]
        public IEnumerator RemountedRewardKeepsNormalMotionStarsFinished()
        {
            Reward(false);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 0);
            yield return Mount();
            yield return new WaitForSecondsRealtime(.8f);
            var reward = controller.State.celebrations.Pending;
            document.panelSettings.targetTexture = null;
            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(target);
            yield return null;
            yield return Mount();
            Assert.AreSame(reward, controller.State.celebrations.Pending);
            Assert.IsTrue(reward.Presented);
            Assert.AreEqual(0f, Root.Q("Celebration stars").resolvedStyle.opacity,
                "A finished burst must not return as static stars after a view remount.");
        }

        [UnityTest]
        public IEnumerator CustomArtworkRevealsOnlyWhenAvailableAndNeverBlocksContinue()
        {
            Reward(true);
            var sticker = controller.State.detail;
            sticker.presetId = null;
            sticker.designId = "celebration-cold-" + Guid.NewGuid().ToString("N");
            sticker.artworkWidth = 400;
            sticker.artworkHeight = 200;
            yield return Mount();
            var reward = controller.State.celebrations.Pending;
            Assert.IsFalse(reward.Presented, "An empty custom artwork must not consume the reveal.");
            Assert.IsTrue(Root.Q<Button>("Celebration continue").enabledSelf);
            Assert.IsTrue(Root.Query<Label>().ToList().Any(l => l.text == "Artwork could not load."));
            yield return Capture("custom-artwork-unavailable");
            StickerArtwork.Store(sticker.designId, File.ReadAllBytes(Path.Combine(Application.dataPath,
                "Resources/Tagtag/Presets/taggi-3.png")));
            yield return Settle();
            Assert.IsTrue(reward.Presented);
            var art = Root.Q<Image>("Celebration artwork");
            Assert.IsNotNull(art.image);
            Assert.AreEqual(2f, art.resolvedStyle.width / art.resolvedStyle.height, .01f);
            yield return Capture("custom-artwork-ready");
            Submit("Read the note");
            yield return Settle();
            StickerArtwork.Forget(sticker.designId);
        }

        [UnityTest]
        public IEnumerator InterruptionSettlesMotionWithoutDismissingOrReplaying()
        {
            Reward(false);
            PlayerPrefs.SetInt("tagtag.reducedMotion", 0);
            yield return Mount();
            PaperMotion.SetPaused(true);
            var art = Root.Q<Image>("Celebration artwork");
            Assert.AreEqual(Vector3.one, art.resolvedStyle.scale.value);
            Assert.IsNotNull(controller.State.celebrations.Pending);
            PaperMotion.SetPaused(false);
            controller.Notify();
            yield return Settle();
            Assert.AreSame(art, Root.Q<Image>("Celebration artwork"));
            Assert.IsFalse(controller.State.celebrations.Pending.TryPresent());
            Submit("Keep exploring");
            yield return Settle();
            Assert.IsNull(Root.Q("Sticker celebration"));
        }

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
            string directory = Path.Combine(Application.temporaryCachePath, "tagtag-celebration-review",
                new DirectoryInfo(Application.dataPath).Parent.Name);
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

        private sealed class ReviewController : ITagtagController, IPlacedLocationsController
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
            public int PlacementRefreshCalls;
            public int PlacementMoreCalls;
            public StickerSummary OpenedPlacement;
            public void RefreshPlacements() { PlacementRefreshCalls++; }
            public void LoadMorePlacements() { PlacementMoreCalls++; }
            public void OpenPlacedLocation(StickerSummary sticker)
            { OpenedPlacement = sticker; State.selected = sticker; Navigate(AppPage.Explore); }
            public bool DeferPlacement;
            public void SelectDesign(string id)
            {
                if (DeferPlacement) { State.busy = true; State.designError = ""; Notify(); return; }
                State.selectedDesign = id; State.selectedPreset = "";
                State.creationOpen = false; State.page = AppPage.Stick; Notify();
            }
            public string DeletedId;
            public bool DeferDeletion;
            public void DeleteDesign(string id)
            {
                DeletedId = id;
                if (DeferDeletion) { State.busy = true; State.designError = ""; }
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
