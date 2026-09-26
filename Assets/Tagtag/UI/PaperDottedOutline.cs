using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    // Decoration only: the existing button owns focus, picking and activation.
    public sealed class PaperDottedOutline : VisualElement
    {
        private readonly bool circular;
        private readonly float cornerRadius;
        private readonly float spacing;

        public PaperDottedOutline(bool circular) : this(circular, 0f, 5f) { }

        public PaperDottedOutline(bool circular, float cornerRadius, float spacing = 5f)
        {
            this.circular = circular;
            this.cornerRadius = cornerRadius;
            this.spacing = Mathf.Max(3f, spacing);
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
            float corner = Mathf.Clamp(cornerRadius - inset, 0f, Mathf.Min(horizontal, vertical) * .5f);
            float perimeter = circular ? 2f * Mathf.PI * (radius - inset) : 2f * (horizontal + vertical - 4f * corner) + 2f * Mathf.PI * corner;
            int count = Mathf.Max(4, Mathf.RoundToInt(perimeter / spacing));
            for (int index = 0; index < count; index++)
            {
                float distance = perimeter * index / count;
                Vector2 point;
                if (circular)
                {
                    float angle = distance / (radius - inset);
                    point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius - inset);
                }
                else point = RoundedPoint(distance, horizontal, vertical, corner) + new Vector2(inset, inset);
                Dot(painter, point, .85f, ink);
            }
        }

        // Walk straight edges and quarter-circle corners at uniform arc-length spacing.
        private static Vector2 RoundedPoint(float distance, float width, float height, float radius)
        {
            float arc = Mathf.PI * radius * .5f;
            for (int side = 0; side < 4; side++)
            {
                float edge = (side % 2 == 0 ? width : height) - 2f * radius;
                if (distance <= edge)
                {
                    switch (side)
                    {
                        case 0: return new Vector2(radius + distance, 0f);
                        case 1: return new Vector2(width, radius + distance);
                        case 2: return new Vector2(width - radius - distance, height);
                        default: return new Vector2(0f, height - radius - distance);
                    }
                }
                distance -= edge;
                if (radius > 0f && distance <= arc)
                {
                    Vector2 center = new Vector2(side == 0 || side == 1 ? width - radius : radius,
                        side == 1 || side == 2 ? height - radius : radius);
                    float angle = (side - 1) * Mathf.PI * .5f + distance / radius;
                    return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                distance -= arc;
            }
            return new Vector2(radius, 0f);
        }

        public static void Decorate(VisualElement element, bool capsule = false, bool container = false)
        {
            element.AddToClassList("paper-die-cut");
            if (capsule)
            {
                element.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    float radius = Mathf.Min(element.layout.width, element.layout.height) * .5f;
                    if (float.IsNaN(radius) || float.IsInfinity(radius)) return;
                    element.style.borderTopLeftRadius = element.style.borderTopRightRadius = radius;
                    element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
                });
            }
            element.Insert(0, new PaperDottedOutline(false, capsule ? 1000f : 14f, container ? 7f : 5f));
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
