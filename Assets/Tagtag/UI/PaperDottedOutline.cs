using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    // Decoration only: the existing button owns focus, picking and activation.
    public sealed class PaperDottedOutline : VisualElement
    {
        private readonly bool circular;

        public PaperDottedOutline(bool circular)
        {
            this.circular = circular;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0f;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = contentRect.width, height = contentRect.height;
            if (width <= 12f || height <= 12f) return;
            Painter2D painter = context.painter2D;
            Vector2 center = new Vector2(width, height) * .5f;
            float radius = Mathf.Min(width, height) * .5f;
            if (circular)
            {
                // Layered translucent disks give the paper a soft, downward contact shadow.
                for (int layer = 4; layer >= 1; layer--)
                    Dot(painter, center + new Vector2(0f, 2f), radius + layer * .6f,
                        new Color(0f, 0f, 0f, .025f));
                Dot(painter, center, radius, parent.resolvedStyle.backgroundColor);
            }

            Color ink = new Color32(32, 32, 30, 255);
            const float inset = 6f;
            float horizontal = width - inset * 2f, vertical = height - inset * 2f;
            float perimeter = circular ? 2f * Mathf.PI * (radius - inset) : 2f * (horizontal + vertical);
            int count = Mathf.Max(4, Mathf.RoundToInt(perimeter / 5f));
            for (int index = 0; index < count; index++)
            {
                float distance = perimeter * index / count;
                Vector2 point;
                if (circular)
                {
                    float angle = distance / (radius - inset);
                    point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius - inset);
                }
                else if (distance < horizontal) point = new Vector2(inset + distance, inset);
                else if (distance < horizontal + vertical) point = new Vector2(width - inset, inset + distance - horizontal);
                else if (distance < 2f * horizontal + vertical) point = new Vector2(width - inset - (distance - horizontal - vertical), height - inset);
                else point = new Vector2(inset, height - inset - (distance - 2f * horizontal - vertical));
                Dot(painter, point, .85f, ink);
            }
        }

        private static void Dot(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.ClosePath();
            painter.Fill();
        }
    }
}
