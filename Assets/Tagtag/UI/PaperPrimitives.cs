using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public enum PaperButtonKind { Primary, Secondary, Quiet, Destructive }
    public enum PaperTextRole { Body, Emphasis, Heading, Display }

    public static class PaperTypography
    {
        public static PaperTextRole Role(int size, bool bold)
        {
            if (!bold) return PaperTextRole.Body;
            if (size >= 30) return PaperTextRole.Display;
            return size >= 20 ? PaperTextRole.Heading : PaperTextRole.Emphasis;
        }

        public static int PointSize(int size, PaperTextRole role)
        {
            return role == PaperTextRole.Heading || role == PaperTextRole.Display ?
                Mathf.RoundToInt(size * 1.12f) : size;
        }
    }

    // Adapted from sticker-app PrimitiveControls: native Button activation and focus
    // remain in charge, while a one-frame latch prevents duplicate submissions.
    public class PaperButton : Button
    {
        private readonly PaperActivation activation = new PaperActivation();
        private bool dieCutRegistered;
        private bool dieCutCircular;
        private bool dieCutCapsule;
        private float dieCutSpacing;
        public PaperButton(string label, Action activate, PaperButtonKind kind = PaperButtonKind.Secondary)
        {
            text = label;
            tooltip = label;
            name = "Action " + label;
            AddToClassList("paper-button");
            AddToClassList(kind == PaperButtonKind.Primary ? "primary" :
                kind == PaperButtonKind.Destructive ? "danger" : kind == PaperButtonKind.Quiet ? "quiet" : "secondary");
            if (kind == PaperButtonKind.Primary || kind == PaperButtonKind.Secondary)
                PaperDottedOutline.Decorate(this);
            if (activate != null) clicked += () =>
            {
                if (!enabledInHierarchy || !activation.TryBegin()) return;
                try { activate(); }
                finally { schedule.Execute(activation.End); }
            };
        }

        internal void SetDieCut(bool circular, bool capsule, float spacing)
        {
            dieCutCircular = circular;
            dieCutSpacing = spacing;
            if (capsule && !dieCutCapsule)
            {
                dieCutCapsule = true;
                RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    float radius = Mathf.Min(layout.width, layout.height) * .5f;
                    if (float.IsNaN(radius) || float.IsInfinity(radius)) return;
                    style.borderTopLeftRadius = style.borderTopRightRadius = radius;
                    style.borderBottomLeftRadius = style.borderBottomRightRadius = radius;
                });
            }
            if (!dieCutRegistered)
            {
                dieCutRegistered = true;
                generateVisualContent += context => PaperDottedOutline.DrawOn(context, this,
                    dieCutCircular, dieCutCapsule ? 1000f : 14f, dieCutSpacing);
            }
            AddToClassList("paper-die-cut");
            MarkDirtyRepaint();
        }
    }

    public sealed class PaperIconButton : PaperButton
    {
        public PaperIconButton(string label, string icon, Action activate, PaperButtonKind kind = PaperButtonKind.Quiet)
            : base("", activate, kind)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("An icon button needs a label.", nameof(label));
            name = "Action " + label;
            tooltip = label;
            AddToClassList("icon-button");
            Add(new PaperIcon(icon, 22));
        }
    }

    public sealed class PaperCollectionCount : VisualElement
    {
        private readonly Label number;
        private readonly Label caption;

        public Label Number => number;
        public Label Caption => caption;

        public PaperCollectionCount()
        {
            number = new Label { name = "home-collected-number" };
            caption = new Label { name = "home-collected-label" };
            Add(number);
            Add(caption);
            SetCount(0);
        }

        public void SetCount(int count)
        {
            number.text = count.ToString();
            caption.text = count == 1 ? "sticker collected" : "stickers collected";
        }
    }

    public sealed class PaperSelection : PaperButton
    {
        private readonly PaperIcon marker;
        private bool initialized;
        public bool IsSelected { get; private set; }

        public PaperSelection(string label, bool selected, Action activate)
            : base(label, activate, PaperButtonKind.Secondary)
        {
            AddToClassList("paper-selection");
            marker = new PaperIcon("check", 16);
            marker.AddToClassList("selection-check");
            Add(marker);
            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            if (initialized && IsSelected == selected) return;
            bool animate = initialized && panel != null && !PaperMotion.Reduced(this);
            initialized = true;
            IsSelected = selected;
            EnableInClassList("selected", selected);
            if (!animate)
            {
                PaperMotion.Cancel(marker, "selection");
                marker.style.opacity = selected ? 1 : 0;
                marker.style.visibility = selected ? Visibility.Visible : Visibility.Hidden;
                marker.style.scale = StyleKeyword.Null;
                return;
            }
            marker.style.visibility = Visibility.Visible;
            float start = marker.resolvedStyle.opacity;
            PaperMotion.Tween(marker, "selection", selected ? PaperMotion.State : PaperMotion.Feedback, t =>
            {
                float progress = PaperMotion.EaseOut(t);
                marker.style.opacity = Mathf.Lerp(start, selected ? 1 : 0, progress);
                float size = Mathf.LerpUnclamped(.82f, 1f, selected ? PaperMotion.Spring(t) : progress);
                marker.style.scale = new Scale(new Vector3(size, size, 1));
                if (t >= 1 && !selected) marker.style.visibility = Visibility.Hidden;
            });
        }
    }

    public sealed class PaperSwitch : Toggle
    {
        private readonly VisualElement thumb;
        public PaperSwitch(string title, bool initial, string description = null)
        {
            text = title;
            tooltip = title;
            AddToClassList("paper-switch");
            var input = this.Q(className: "unity-toggle__input");
            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("switch-copy");
            copy.Add(new Label(title));
            if (!string.IsNullOrWhiteSpace(description)) copy.Add(new Label(description));
            input.Add(copy);
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("switch-track");
            input.Add(track);
            thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList("switch-thumb");
            track.Add(thumb);
            SetValueWithoutNotify(initial);
            this.RegisterValueChangedCallback(e =>
            {
                if (e.target != this) return;
                float from = e.previousValue ? 20 : 0;
                float to = e.newValue ? 20 : 0;
                PaperMotion.Tween(thumb, "switch", PaperMotion.State, t =>
                {
                    thumb.style.translate = new Translate(Mathf.LerpUnclamped(from, to, PaperMotion.Spring(t)), 0);
                });
            });
        }

        public override void SetValueWithoutNotify(bool newValue)
        {
            base.SetValueWithoutNotify(newValue);
            if (thumb != null) thumb.style.translate = new Translate(newValue ? 20 : 0, 0);
        }
    }

    public sealed class PaperField : TextField
    {
        private readonly Label helper;
        private readonly Label error;
        private readonly Label counter;

        public PaperField(string label, string initialValue, int limit, bool multiline = false,
            string help = null, bool showCounter = false) : base(label)
        {
            maxLength = limit;
            this.multiline = multiline;
            AddToClassList("paper-field");
            if (multiline) AddToClassList("multiline");
            var support = new VisualElement();
            support.AddToClassList("field-support");
            Add(support);
            var messages = new VisualElement();
            messages.AddToClassList("field-messages");
            support.Add(messages);
            helper = new Label(help ?? "") { pickingMode = PickingMode.Ignore };
            helper.AddToClassList("field-helper");
            messages.Add(helper);
            error = new Label { name = "field-error", pickingMode = PickingMode.Ignore };
            error.AddToClassList("field-error");
            error.style.display = DisplayStyle.None;
            messages.Add(error);
            if (showCounter)
            {
                counter = new Label { name = "field-counter", pickingMode = PickingMode.Ignore };
                counter.AddToClassList("field-counter");
                support.Add(counter);
            }
            SetValueWithoutNotify(initialValue ?? "");
            this.RegisterValueChangedCallback(e => { if (e.target == this) UpdateCount(); });
            RegisterCallback<FocusInEvent>(_ => AddToClassList("is-focused"));
            RegisterCallback<FocusOutEvent>(_ => RemoveFromClassList("is-focused"));
        }

        public override void SetValueWithoutNotify(string newValue)
        {
            base.SetValueWithoutNotify(newValue);
            UpdateCount();
        }

        private void UpdateCount()
        {
            if (counter != null) counter.text = (value?.Length ?? 0) + " / " + maxLength;
        }

        public void ApplyScale(float scale)
        {
            float heading = Mathf.RoundToInt(16f * scale);
            float support = Mathf.RoundToInt(13f * scale);
            var title = this.Q<Label>(className: "unity-base-field__label");
            if (title != null) title.style.fontSize = heading;
            var input = this.Q<VisualElement>(className: "unity-base-text-field__input");
            if (input != null) input.style.fontSize = heading;
            helper.style.fontSize = support;
            error.style.fontSize = support;
            if (counter != null) counter.style.fontSize = support;
        }

        public void PresentError(string message)
        {
            bool invalid = !string.IsNullOrWhiteSpace(message);
            EnableInClassList("invalid", invalid);
            error.text = message ?? "";
            error.style.display = invalid ? DisplayStyle.Flex : DisplayStyle.None;
            helper.style.display = invalid ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
