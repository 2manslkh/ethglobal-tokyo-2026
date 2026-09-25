using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public enum AppNavigationReason { Automatic, Refresh, Initial, TopLevel, Forward, Back, Overlay }

    // Tracks destinations, never render counts, selected filters, loading or save state.
    // It owns presentation history only; application navigation remains authoritative.
    public sealed class PaperNavigationMotion
    {
        private readonly List<string> path = new List<string>();
        private string identity;
        private bool wasTopLevel;
        public AppNavigationReason Observe(string next, bool topLevel, AppNavigationReason reason = AppNavigationReason.Automatic)
        {
            if (identity == next) return AppNavigationReason.Refresh;
            if (identity == null)
            {
                identity = next; wasTopLevel = topLevel; path.Add(next);
                return AppNavigationReason.Initial;
            }
            int previous = path.LastIndexOf(next);
            var result = reason;
            if (reason == AppNavigationReason.Automatic)
                result = topLevel && wasTopLevel ? AppNavigationReason.TopLevel :
                    previous >= 0 ? AppNavigationReason.Back : AppNavigationReason.Forward;
            if (result == AppNavigationReason.TopLevel) path.Clear();
            else if (previous >= 0) path.RemoveRange(previous, path.Count - previous);
            // Bound presentation history even in long sessions or review sweeps.
            if (path.Count == 32) path.RemoveAt(0);
            path.Add(next); identity = next; wasTopLevel = topLevel;
            return result;
        }

        public static void Enter(VisualElement element, AppNavigationReason reason)
        {
            if (element == null || reason == AppNavigationReason.Refresh || reason == AppNavigationReason.Automatic || reason == AppNavigationReason.Overlay) return;
            float distance = reason == AppNavigationReason.Forward ? 24 : reason == AppNavigationReason.Back ? -18 : 0;
            float duration = reason == AppNavigationReason.Forward ? .22f : reason == AppNavigationReason.Back ? .18f : .15f;
            PaperMotion.Tween(element, "navigation", duration, t =>
            {
                float eased = PaperMotion.EaseOut(t);
                element.style.opacity = Mathf.Lerp(.55f, 1, eased);
                element.style.translate = new Translate(distance * (1 - eased), 0);
            });
        }

        public static void StaggerCollection(VisualElement body, AppNavigationReason reason)
        {
            if (reason == AppNavigationReason.Refresh || reason == AppNavigationReason.Automatic || reason == AppNavigationReason.Overlay || PaperMotion.Reduced(body)) return;
            // Only the first authored collection, at most six siblings. Never animate
            // arbitrary sections, nested book pages, a camera drawer or scrolled-in rows.
            var candidates = body.Query<VisualElement>().ToList();
            VisualElement collection = null;
            bool rowsOnly = false;
            foreach (var candidate in candidates)
            {
                if (candidate.GetFirstAncestorOfType<PaperSheet>() != null) continue;
                if (candidate.ClassListContains("home-tray") || candidate.ClassListContains("sticker-grid"))
                { collection = candidate; break; }
                if (candidate.ClassListContains("sticker-post-row") || candidate.ClassListContains("memoir-row"))
                { collection = candidate.parent; rowsOnly = true; break; }
            }
            if (collection == null) return;
            int count = 0;
            foreach (var child in collection.Children())
            {
                if (!(child is Button) || child.resolvedStyle.display == DisplayStyle.None) continue;
                if (rowsOnly && !child.ClassListContains("sticker-post-row") && !child.ClassListContains("memoir-row")) continue;
                float delay = count * .018f;
                PaperMotion.Tween(child, "navigation-collection", .16f + delay, t =>
                {
                    float progress = PaperMotion.EaseOut(Mathf.Clamp01((t * (.16f + delay) - delay) / .16f));
                    child.style.opacity = Mathf.Lerp(.65f, 1, progress);
                });
                if (++count == 6) break;
            }
        }

        // Stores values and semantic keys only, so a refresh never keeps detached UI,
        // event handlers, native accessibility nodes or artwork/camera resources alive.
        public sealed class RebuildState
        {
            private readonly Dictionary<string, Vector2> scrolls = new Dictionary<string, Vector2>();
            private string focusKey;
            private int cursor, selection;
            private bool textFocus;

            public static RebuildState Capture(VisualElement root)
            {
                var state = new RebuildState();
                if (root == null) return state;
                var focused = root.focusController?.focusedElement as VisualElement;
                var field = focused as TextField ?? focused?.GetFirstAncestorOfType<TextField>();
                if (field != null) focused = field;
                var counts = new Dictionary<string, int>();
                foreach (var element in root.Query<VisualElement>().ToList())
                {
                    string key = Key(element, counts);
                    if (element is ScrollView scroll) state.scrolls[key] = scroll.scrollOffset;
                    if (element != focused) continue;
                    state.focusKey = key;
                    if (field != null) { state.textFocus = true; state.cursor = field.cursorIndex; state.selection = field.selectIndex; }
                }
                return state;
            }

            private static string Key(VisualElement element, Dictionary<string, int> counts)
            {
                string label = !string.IsNullOrEmpty(element.name) ? element.name :
                    element is TextField field ? field.label :
                    !string.IsNullOrEmpty(element.tooltip) ? element.tooltip :
                    element is Button button ? button.text : string.Join(" ", element.GetClasses());
                string key = element.GetType().FullName + ":" + label;
                counts.TryGetValue(key, out int count); counts[key] = count + 1;
                return key + ":" + count;
            }

            public void Restore(VisualElement root, Func<bool> current)
            {
                if (!current()) return;
                var positions = new List<(ScrollView scroll, Vector2 offset)>();
                VisualElement focus = null;
                var counts = new Dictionary<string, int>();
                foreach (var element in root.Query<VisualElement>().ToList())
                {
                    string key = Key(element, counts);
                    if (element is ScrollView scroll && scrolls.TryGetValue(key, out var offset)) positions.Add((scroll, offset));
                    if (key == focusKey) focus = element;
                }
                // Focus immediately after the synchronous replacement, without forcibly
                // dismissing the OS keyboard first. Hidden/deleted inputs stay unfocused.
                if (current() && CanFocus(focus, root))
                {
                    focus.Focus();
                    RestoreSelection(focus);
                }
                int frames = 0, stable = 0;
                Vector2 previous = new Vector2(-1, -1);
                IVisualElementScheduledItem pending = null;
                EventCallback<PointerDownEvent> pointer = null;
                EventCallback<WheelEvent> wheel = null;
                EventCallback<NavigationMoveEvent> navigation = null;
                EventCallback<KeyDownEvent> keyDown = null;
                EventCallback<DetachFromPanelEvent> detach = null;
                Action stop = () =>
                {
                    pending?.Pause(); root.UnregisterCallback(pointer, TrickleDown.TrickleDown);
                    root.UnregisterCallback(wheel, TrickleDown.TrickleDown); root.UnregisterCallback(navigation, TrickleDown.TrickleDown);
                    root.UnregisterCallback(keyDown, TrickleDown.TrickleDown); root.UnregisterCallback(detach);
                };
                pointer = _ => stop(); wheel = _ => stop(); navigation = _ => stop(); keyDown = _ => stop(); detach = _ => stop();
                root.RegisterCallback(pointer, TrickleDown.TrickleDown); root.RegisterCallback(wheel, TrickleDown.TrickleDown);
                root.RegisterCallback(navigation, TrickleDown.TrickleDown); root.RegisterCallback(keyDown, TrickleDown.TrickleDown); root.RegisterCallback(detach);
                pending = root.schedule.Execute(() =>
                {
                    if (!current() || root.panel == null) { stop(); return; }
                    var focused = root.focusController?.focusedElement as VisualElement;
                    if (frames == 0 && focus != null && (focused == focus || (focused != null && focus.Contains(focused)))) RestoreSelection(focus);
                    Vector2 geometry = Vector2.zero;
                    bool ready = true;
                    foreach (var item in positions)
                    {
                        geometry += item.scroll.contentContainer.layout.size + item.scroll.contentViewport.layout.size;
                        ready &= item.scroll.contentViewport.layout.height > 0;
                        item.scroll.scrollOffset = item.offset;
                        ready &= Vector2.Distance(item.scroll.scrollOffset,item.offset)<.5f;
                    }
                    stable = ready && Vector2.Distance(previous, geometry) < .1f ? stable + 1 : 0;
                    previous = geometry;
                    if (++frames >= 12 || stable >= 2) stop();
                }).Every(16);
            }

            private void RestoreSelection(VisualElement focus)
            {
                if (textFocus && focus is TextField field)
                    field.SelectRange(Mathf.Clamp(cursor, 0, field.value?.Length ?? 0), Mathf.Clamp(selection, 0, field.value?.Length ?? 0));
            }
            private static bool CanFocus(VisualElement element, VisualElement root)
            {
                if (element == null || !element.enabledInHierarchy || !element.focusable) return false;
                for (var node = element; node != null; node = node.parent)
                {
                    if (node.style.display == DisplayStyle.None || node.resolvedStyle.display == DisplayStyle.None) return false;
                    if (node == root) return true;
                }
                return false;
            }
        }
    }
}
