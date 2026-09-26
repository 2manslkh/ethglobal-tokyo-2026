using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private VisualElement celebrationHost;
        private string celebrationToken;
        private Image celebrationArtwork;
        private CelebrationStars celebrationStars;
        private bool CelebrationActive => controller?.State.celebrations.Pending != null &&
            SignedIn(controller.State);

        private void RenderCelebration(AppState state)
        {
            bool active = CelebrationActive;
            screenHost.style.display = active ? DisplayStyle.None : DisplayStyle.Flex;
            navHost.style.display = active ? DisplayStyle.None : DisplayStyle.Flex;
            overlayHost.style.display = active ? DisplayStyle.None : DisplayStyle.Flex;
            if (!active)
            {
                celebrationHost?.RemoveFromHierarchy();
                celebrationHost = null;
                celebrationToken = null;
                celebrationArtwork = null;
                celebrationStars = null;
                return;
            }

            root.style.backgroundColor = Paper;
            controller.Map?.Hide();
            var reward = state.celebrations.Pending;
            if (celebrationHost != null && celebrationToken == reward.Token) return;
            celebrationHost?.RemoveFromHierarchy();
            celebrationToken = reward.Token;
            celebrationHost = Column(safeRoot);
            celebrationHost.name = "Sticker celebration";
            celebrationHost.style.flexGrow = 1;
            celebrationHost.style.minHeight = 0;
            celebrationHost.style.backgroundColor = Paper;
            celebrationHost.style.paddingLeft = celebrationHost.style.paddingRight = 24;
            celebrationHost.style.paddingTop = 12;
            celebrationHost.style.paddingBottom = 20;

            bool found = reward.Kind == StickerCelebrationKind.Found;
            var scroll = PaperScroll(celebrationHost);
            scroll.contentContainer.style.flexGrow = 1;
            scroll.contentContainer.style.justifyContent = Justify.Center;
            scroll.contentContainer.style.alignItems = Align.Stretch;
            var stage = Column(scroll);
            stage.name = "Celebration stage";
            stage.style.height = 290;
            stage.style.flexShrink = 0;
            stage.style.justifyContent = Justify.Center;
            stage.style.alignItems = Align.Center;
            var stars = new CelebrationStars(reward.Presented, reducedMotion) { name = "Celebration stars", pickingMode = PickingMode.Ignore };
            stars.style.position = Position.Absolute;
            stars.style.left = stars.style.right = stars.style.top = stars.style.bottom = 0;
            stage.Add(stars);
            Image artwork = Art(stage, reward.Sticker, 224, false);
            celebrationArtwork = artwork;
            celebrationStars = stars;
            stars.style.visibility = artwork.image == null ? Visibility.Hidden : Visibility.Visible;
            artwork.name = "Celebration artwork";
            artwork.pickingMode = PickingMode.Ignore;
            AddArtworkNotice(scroll, artwork, reward.Sticker.designId, false);
            var heading = Text(scroll, found ? "You found it!" : "You left your mark!", 38, true);
            heading.name = "Celebration heading";
            heading.style.unityTextAlign = TextAnchor.MiddleCenter;
            heading.style.marginTop = 12;
            heading.style.marginBottom = 8;
            var copy = Text(scroll, found ? "A little discovery, now in your book." :
                "Your sticker is ready to be discovered.", 17, false, Muted);
            copy.style.unityTextAlign = TextAnchor.MiddleCenter;
            copy.style.marginBottom = 24;

            var action = Action(celebrationHost, found ? "Read the note" : "Keep exploring",
                () => ContinueCelebration(reward));
            action.name = "Celebration continue";
            action.style.minHeight = 56;
            action.style.flexShrink = 0;
            action.style.marginTop = 16;
            action.style.marginBottom = 0;
            // Focus stays on the only main action; hidden screens cannot receive navigation input.
            action.Focus();

            TryRevealCelebration();
        }

        private void TryRevealCelebration()
        {
            var reward = controller?.State.celebrations.Pending;
            if (!CelebrationActive || reward.Token != celebrationToken || celebrationArtwork?.image == null ||
                celebrationArtwork.panel == null) return;
            celebrationStars.style.visibility = Visibility.Visible;
            if (!reward.TryPresent()) return;
            StickerSuccessHaptics.Play();
            Image artwork = celebrationArtwork;
            PaperMotion.Tween(artwork, "celebration", .48f, t =>
            {
                float scale = Mathf.LerpUnclamped(.78f, 1, PaperMotion.Spring(t));
                artwork.style.scale = new Scale(new Vector3(scale, scale, 1));
            });
            celebrationStars.Play();
        }

        private void ContinueCelebration(StickerCelebration reward)
        {
            if (controller.State.celebrations.Pending != reward) return;
            controller.State.celebrations.Acknowledge(reward.Token);
            if (reward.Kind == StickerCelebrationKind.Found)
            {
                lastPresentedDiscoveryId = reward.Sticker.id;
                homeSection = HomeSection.Collected;
                sheet = Sheet.Collected;
                sheetStickerId = reward.Sticker.id;
                controller.OpenCollected(reward.Sticker.id);
            }
            QueueRender();
        }
    }

    // Small, deliberately irregular ink stars; artwork remains the focal point.
    internal sealed class CelebrationStars : VisualElement
    {
        private float progress = 1;
        private bool staticStars = true;
        private static readonly Vector2[] positions = {
            new Vector2(.12f, .28f), new Vector2(.79f, .12f), new Vector2(.92f, .49f),
            new Vector2(.19f, .80f), new Vector2(.83f, .82f), new Vector2(.39f, .08f)
        };
        private static readonly Vector2[] outline = {
            new Vector2(.49f, 0), new Vector2(.63f, .35f), new Vector2(1, .40f),
            new Vector2(.72f, .62f), new Vector2(.81f, 1), new Vector2(.48f, .79f),
            new Vector2(.14f, .94f), new Vector2(.24f, .58f), new Vector2(0, .32f),
            new Vector2(.37f, .34f)
        };

        public CelebrationStars(bool previouslyPresented, bool reduced)
        {
            // Remount a completed normal-motion burst in its final, faded state.
            staticStars = !previouslyPresented || reduced;
            style.opacity = previouslyPresented && !reduced ? 0 : 1;
            generateVisualContent += Draw;
        }

        public void Play()
        {
            staticStars = PaperMotion.Reduced(this);
            if (staticStars) return;
            PaperMotion.Tween(this, "stars", .7f, t => { progress = t; MarkDirtyRepaint(); });
        }

        private void Draw(MeshGenerationContext context)
        {
            bool still = staticStars || PaperMotion.Reduced(this);
            float alpha = still ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1, progress));
            if (alpha <= 0) return;
            var pen = context.painter2D;
            pen.lineWidth = 1.3f;
            pen.lineJoin = LineJoin.Round;
            pen.strokeColor = new Color(32f / 255, 32f / 255, 30f / 255, alpha);
            pen.fillColor = new Color(1, 225f / 255, 90f / 255, alpha);
            Vector2 center = contentRect.center;
            for (int i = 0; i < positions.Length; i++)
            {
                Vector2 end = new Vector2(positions[i].x * contentRect.width, positions[i].y * contentRect.height);
                Vector2 origin = Vector2.Lerp(center, end, still ? 1 : Mathf.Lerp(.55f, 1, PaperMotion.EaseOut(progress)));
                float size = i % 2 == 0 ? 21 : 15;
                pen.BeginPath();
                for (int j = 0; j < outline.Length; j++)
                {
                    Vector2 point = origin + (outline[j] - Vector2.one * .5f) * size;
                    if (j == 0) pen.MoveTo(point); else pen.LineTo(point);
                }
                pen.ClosePath();
                pen.Fill();
                pen.Stroke();
            }
        }
    }
}
