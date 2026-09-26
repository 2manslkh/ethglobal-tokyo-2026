using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    /// <summary>A layout-stable paper sign with two screen-anchored strings.</summary>
    public sealed class PaperHangingHeader : VisualElement
    {
        public VisualElement Paper { get; }
        public Label Title { get; }
        private readonly VisualElement strings;
        private bool entrancePending;
        private bool entranceStarted;
        private const float HoleY = 13f;
        private const string MotionChannel = "hanging-header";

        public PaperHangingHeader(string title, VisualElement stringHost)
        {
            name = "Hanging header";
            style.flexGrow = 1f;
            style.flexShrink = 1f;
            style.minWidth = 0f;
            style.paddingTop = 25f;
            style.paddingBottom = 9f;
            // The paper itself blocks camera placement, but its strings never do.
            Paper = new VisualElement { name = "Hanging header paper" };
            Paper.style.paddingLeft = Paper.style.paddingRight = 19f;
            Paper.style.paddingTop = 18f;
            Paper.style.paddingBottom = 12f;
            Paper.style.minHeight = 70f;
            Paper.style.justifyContent = Justify.Center;
            Paper.style.transformOrigin = new TransformOrigin(Length.Percent(50), HoleY, 0);
            Paper.generateVisualContent += DrawPaper;
            Add(Paper);
            Title = new Label(title) { name = "Hanging header title", pickingMode = PickingMode.Ignore };
            Title.style.whiteSpace = WhiteSpace.Normal;
            Title.style.unityTextAlign = TextAnchor.MiddleCenter;
            Title.style.marginLeft = Title.style.marginRight = 0;
            Title.style.marginTop = Title.style.marginBottom = 0;
            Title.style.paddingLeft = Title.style.paddingRight = 0;
            Paper.Add(Title);

            strings = new VisualElement { name = "Hanging header strings", pickingMode = PickingMode.Ignore };
            strings.style.position = Position.Absolute;
            strings.style.left = strings.style.right = strings.style.top = strings.style.bottom = 0;
            strings.generateVisualContent += DrawStrings;
            stringHost.Add(strings);
            RegisterCallback<GeometryChangedEvent>(_ => LayoutChanged());
            Paper.RegisterCallback<GeometryChangedEvent>(_ => LayoutChanged());
            strings.RegisterCallback<GeometryChangedEvent>(_ => strings.MarkDirtyRepaint());
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                entrancePending = false;
                Settle();
                strings.RemoveFromHierarchy();
            });
        }

        public void Enter(AppNavigationReason reason)
        {
            if (reason == AppNavigationReason.Refresh || reason == AppNavigationReason.Automatic ||
                reason == AppNavigationReason.Overlay || entranceStarted) return;
            entrancePending = true;
            LayoutChanged();
        }

        private void LayoutChanged()
        {
            strings.MarkDirtyRepaint();
            if (!entrancePending || panel == null || !(Paper.layout.width > 0f)) return;
            entrancePending = false;
            entranceStarted = true;
            PaperMotion.Tween(Paper, MotionChannel, .6f, progress =>
            {
                float remaining = 1f - progress;
                float angle = progress >= 1f ? 0f : 3f * Mathf.Sin(progress * Mathf.PI * 3f) * remaining * remaining;
                Paper.style.translate = new Translate(0f, -24f * (1f - PaperMotion.EaseOut(progress)));
                Paper.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
                strings.MarkDirtyRepaint();
            });
        }

        public void Settle()
        {
            entrancePending = false;
            PaperMotion.Cancel(Paper, MotionChannel);
            Paper.style.translate = new Translate(0, 0);
            Paper.style.rotate = new Rotate(new Angle(0, AngleUnit.Degree));
            strings.MarkDirtyRepaint();
        }

        private void DrawStrings(MeshGenerationContext context)
        {
            if (Paper.panel == null || !(Paper.layout.width > 0f) || !visible) return;
            Painter2D painter = context.painter2D;
            for (int i = 0; i < 2; i++)
            {
                float x = Paper.layout.width * (i == 0 ? .22f : .78f);
                Vector2 hole = Paper.ChangeCoordinatesTo(strings, new Vector2(x, HoleY));
                // Read the untransformed slot for fixed top anchors, the moving paper for the knots.
                float anchorX = this.ChangeCoordinatesTo(strings, new Vector2(Paper.layout.x + x, 0)).x;
                Vector2 top = new Vector2(anchorX, 0f);
                painter.lineCap = LineCap.Round;
                painter.lineWidth = 3.2f;
                painter.strokeColor = new Color(1f, .99f, .96f, .8f);
                painter.BeginPath(); painter.MoveTo(top); painter.LineTo(hole); painter.Stroke();
                painter.lineWidth = 1.25f;
                painter.strokeColor = new Color32(109, 100, 84, 255);
                painter.BeginPath(); painter.MoveTo(top); painter.LineTo(hole); painter.Stroke();
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(hole + new Vector2(-2, -1));
                painter.LineTo(hole + new Vector2(2, 1));
                painter.MoveTo(hole);
                painter.LineTo(hole + new Vector2(i == 0 ? 2 : -2, 5));
                painter.Stroke();
            }
        }

        private void DrawPaper(MeshGenerationContext context)
        {
            float width = Paper.layout.width, height = Paper.layout.height;
            if (width <= 0 || height <= 0) return;
            Painter2D painter = context.painter2D;
            for (int layer = 4; layer >= 1; layer--)
            {
                Outline(painter, width, height, new Vector2(0, 1.5f + layer * .7f));
                painter.fillColor = new Color(.22f, .19f, .13f, .025f);
                painter.Fill();
            }
            Outline(painter, width, height, Vector2.zero);
            painter.fillColor = new Color32(255, 253, 246, 255);
            painter.Fill();
            painter.lineWidth = .8f;
            painter.strokeColor = new Color32(161, 153, 132, 255);
            painter.Stroke();
            for (int i = 0; i < 2; i++)
            {
                Vector2 hole = new Vector2(width * (i == 0 ? .22f : .78f), HoleY);
                painter.BeginPath(); painter.Arc(hole, 3.4f, 0, 360);
                painter.fillColor = new Color32(233, 226, 209, 255); painter.Fill();
                painter.lineWidth = .75f; painter.strokeColor = new Color32(178, 163, 136, 255); painter.Stroke();
            }
        }

        // Small asymmetries in the cut keep the silhouette handmade, with no texture asset.
        private static void Outline(Painter2D painter, float width, float height, Vector2 offset)
        {
            Vector2 P(float x, float y) => new Vector2(x, y) + offset;
            painter.BeginPath();
            painter.MoveTo(P(12, 3));
            painter.BezierCurveTo(P(width * .3f, 1), P(width * .64f, 5), P(width - 13, 2));
            painter.BezierCurveTo(P(width - 5, 1), P(width - 2, 7), P(width - 3, 15));
            painter.LineTo(P(width - 2, height - 14));
            painter.BezierCurveTo(P(width - 1, height - 5), P(width - 7, height - 2), P(width - 15, height - 3));
            painter.BezierCurveTo(P(width * .62f, height - 5), P(width * .3f, height - 1), P(13, height - 3));
            painter.BezierCurveTo(P(4, height - 2), P(2, height - 8), P(3, height - 15));
            painter.LineTo(P(2, 15));
            painter.BezierCurveTo(P(1, 7), P(5, 3), P(12, 3));
            painter.ClosePath();
        }
    }
}
