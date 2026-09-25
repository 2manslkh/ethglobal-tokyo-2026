using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    // Presentation only: completion callbacks must never commit data or emit haptics.
    // A channel owns one property/sequence; replacing it starts from the caller's
    // current presentation. Detach/background settles visuals without completion.
    public static class PaperMotion
    {
        public const float Feedback = .12f, State = .22f, Arrival = .32f, Exit = .20f;
        private sealed class Owner
        {
            public readonly Dictionary<string, Track> Tracks = new Dictionary<string, Track>();
        }
        private sealed class Track
        {
            public VisualElement Element;
            public Owner Owner;
            public string Channel;
            public Action<float> Update;
            public Action Complete;
            public IVisualElementScheduledItem Schedule;
            public double Started;
            public float Duration;
            public bool Essential, Ended;
        }
        private static readonly ConditionalWeakTable<VisualElement, Owner> owners = new ConditionalWeakTable<VisualElement, Owner>();
        private static readonly HashSet<Track> active = new HashSet<Track>();
        private static bool suspended, paused, focused = true;
        public static bool IsSuspended => suspended;
        public static event Action<bool> SuspensionChanged;

        static PaperMotion()
        {
            Application.focusChanged += SetFocused;
            Application.quitting += () => SetSuspended(true);
        }

        public static bool Reduced(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.ClassListContains("reduced-motion")) return true;
                if (current == element.panel?.visualTree) return false;
            }
            return false;
        }

        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1 - Mathf.Pow(1 - t, 3); }
        // One small overshoot, fully settled at the end; no endless elastic tail.
        public static float Spring(float t)
        {
            t = Mathf.Clamp01(t) - 1;
            return 1 + 2.1f * t * t * t + 1.1f * t * t;
        }

        public static void Tween(VisualElement element, string channel, float duration, Action<float> update,
            Action complete = null, bool essential = false)
        {
            if (element == null || update == null) return;
            if (!owners.TryGetValue(element, out var owner))
            {
                owner = new Owner(); owners.Add(element, owner);
                element.RegisterCallback<DetachFromPanelEvent>(_ => CancelTree(element));
            }
            if (owner.Tracks.TryGetValue(channel, out var previous)) Stop(previous, false, false);
            if (element.panel == null || suspended) { update(1); return; }
            if (duration <= 0 || (!essential && Reduced(element)))
            { update(1); complete?.Invoke(); return; }
            var track = new Track { Element = element, Owner = owner, Channel = channel, Duration = duration,
                Update = update, Complete = complete, Essential = essential, Started = Time.realtimeSinceStartupAsDouble };
            owner.Tracks[channel] = track; active.Add(track);
            update(0);
            if (track.Ended) return;
            track.Schedule = element.schedule.Execute(() => Step(track)).Every(1);
        }

        private static void Step(Track track)
        {
            if (track.Ended) return;
            if (track.Element.panel == null || suspended) { Stop(track, true, false); return; }
            if (!track.Essential && Reduced(track.Element)) { Stop(track, true, true); return; }
            float progress = Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble - track.Started) / track.Duration);
            if (progress >= 1) Stop(track, true, true); else track.Update(progress);
        }
        private static void Stop(Track track, bool settle, bool complete)
        {
            if (track.Ended) return;
            track.Ended = true; track.Schedule?.Pause(); active.Remove(track);
            track.Owner.Tracks.Remove(track.Channel);
            if (settle) track.Update(1);
            if (complete) track.Complete?.Invoke();
        }
        public static void Cancel(VisualElement element, string channel)
        {
            if (element != null && owners.TryGetValue(element, out var owner) && owner.Tracks.TryGetValue(channel, out var track))
                Stop(track, true, false);
        }
        public static void CancelTree(VisualElement root)
        {
            if (root == null || active.Count == 0) return;
            foreach (var track in new List<Track>(active))
                if (track.Element == root || root.Contains(track.Element)) Stop(track, true, false);
        }
        public static void SetSuspended(bool value)
        {
            if(suspended==value)return;
            suspended = value;
            SuspensionChanged?.Invoke(value);
            if (value) foreach (var track in new List<Track>(active)) Stop(track, true, false);
        }
        public static void SetPaused(bool value) { paused=value;SetSuspended(paused||!focused); }
        public static void SetFocused(bool value) { focused=value;SetSuspended(paused||!focused); }

        public static void Reveal(VisualElement element, float distance = 12, float delay = 0)
        {
            if (element == null) return;
            bool reduced = Reduced(element);
            float duration = reduced ? Feedback : Arrival;
            delay = reduced ? 0 : Mathf.Clamp(delay, 0, .12f);
            float wait = delay;
            Tween(element, "reveal", duration + wait, t =>
            {
                float progress = EaseOut(Mathf.Clamp01((t * (duration + wait) - wait) / duration));
                // Already legible during a stagger; never hold content invisible.
                element.style.opacity = Mathf.Lerp(.65f, 1, progress);
                element.style.translate = new Translate(0, reduced || Reduced(element) ? 0 : distance * (1 - progress));
            }, essential: true);
        }
        public static void Commit(VisualElement artwork)
        {
            if (artwork == null) return;
            if(artwork is Button)
            {
                var visual=(VisualElement)artwork.Q<Image>()??artwork.Q<PaperIcon>();
                if(visual==null){Reveal(artwork,0);return;}
                artwork=visual;
            }
            if (Reduced(artwork)) { Reveal(artwork, 0); return; }
            Tween(artwork, "commit", .48f, t =>
            {
                float scale = Mathf.LerpUnclamped(.94f, 1, Spring(t));
                artwork.style.scale = new Scale(new Vector3(scale, scale, 1));
            });
        }
    }
}
