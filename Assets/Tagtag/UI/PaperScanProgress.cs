using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    // Stage progress only; the inventory button keeps ownership of input and focus.
    public sealed class PaperScanProgress : VisualElement
    {
        private readonly Color backing;
        private readonly Color track;
        private readonly Color fill;
        public float Progress { get; private set; }

        public PaperScanProgress(Color backing, Color track, Color fill)
        {
            this.backing = backing;
            this.track = track;
            this.fill = fill;
            name = "STICK scan progress";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0f;
            generateVisualContent += Draw;
        }

        public void SetStage(PlacementScanState state)
        {
            float progress = PaperScan.StageIndex(state) / (float)(PaperScan.StageCount - 1);
            if (Mathf.Approximately(Progress, progress)) return;
            Progress = progress;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float radius = Mathf.Min(contentRect.width, contentRect.height) * .5f - 5f;
            if (radius <= 0f) return;
            Painter2D painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            Stroke(backing, 9f, 1f);
            Stroke(track, 5f, 1f);
            if (Progress > 0f) Stroke(fill, 5f, Progress);

            void Stroke(Color color, float width, float fraction)
            {
                painter.strokeColor = color;
                painter.lineWidth = width;
                painter.BeginPath();
                painter.Arc(contentRect.center, radius, -90f, -90f + 360f * fraction);
                painter.Stroke();
            }
        }
    }
}
